using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xians.Lib.Logging.Models;
using Xians.Lib.Http;
using Xians.Lib.Common;

namespace Xians.Lib.Logging;

/// <summary>
/// Static class providing logging service management and shutdown handling.
/// Manages background log processing and batching for sending logs to the application server.
/// </summary>
public static class LoggingServices
{
    // Global concurrent queue for all logs
    private static readonly ConcurrentQueue<Log> _globalLogQueue = new();
    
    // Lock for controlling access to processing state
    private static readonly object _processingLock = new object();
    
    // Thread for processing logs
    private static Thread? _processingThread;
    
    // Cancellation token for clean shutdown
    private static CancellationTokenSource? _cancellationTokenSource;
    
    // Flag to track initialization
    private static bool _isInitialized = false;
    private static readonly object _initLock = new object();
    
    /// <summary>
    /// Gets a value indicating whether the LoggingServices has been initialized.
    /// </summary>
    public static bool IsInitialized => _isInitialized;
    
    // Track pending upload tasks for proper shutdown
    private static readonly List<Task> _pendingUploadTasks = new();
    private static readonly object _tasksLock = new object();

    // Client for sending logs to API
    private static IHttpClientService? _httpClientService;
    private static readonly string _logApiEndpoint = WorkflowConstants.ApiEndpoints.Logs;
    // Drain throughput. batchSize / processingIntervalMs is a hard ceiling on how fast the queue empties:
    // 500 per 2s = 250 entries/second. The previous 100 per 30s drained at 3.3/second, which any agent under
    // real load exceeds - the queue then grows for as long as the load lasts and keeps draining at 3.3/second
    // afterwards, so worker memory climbs with load and does not come back when it stops.
    //
    // The interval is deliberately not shorter. ProcessLogBatch starts an upload without awaiting it and then
    // sleeps, so an interval below the upload's own latency leaves overlapping requests in flight with nothing
    // bounding their number.
    private static int _batchSize = 500;
    private static int _processingIntervalMs = 2000;

    // Safety valve, not a normal operating point. Even at 250/second the queue is only as bounded as the
    // server's availability: if uploads fail, every batch is requeued and nothing drains at all. Without a cap
    // a long outage grows the queue until the process dies. At roughly 1.4 KB per entry this bounds the
    // backlog at ~135 MB before the oldest entries start being dropped.
    private static int _maxQueueDepth = 100_000;

    // Entries discarded because the queue was at _maxQueueDepth. Surfaced through DroppedLogCount so a caller
    // can tell "logs are missing" from "logs were never written". Deliberately not added to GetLoggingStats,
    // whose tuple shape is public and would break callers that destructure it.
    private static long _droppedLogCount;

    // Rate-limits the drop warning: at overflow every enqueue drops one, and a message per drop would itself
    // become the load problem.
    private static long _lastDropWarningTicks;
    
    // Retry tracking to prevent infinite loops
    private static readonly ConcurrentDictionary<string, int> _logRetryCount = new();
    private const int MAX_RETRIES = 3;
    
    // Diagnostics
    private static bool _verboseDiagnostics = false;

    // Track first log enqueued for diagnostics
    private static bool _firstLogEnqueued = false;
    
    /// <summary>
    /// Enqueues a log to the global queue for processing.
    /// Only enqueues if LoggingServices has been initialized.
    /// </summary>
    /// <param name="log">The log entry to enqueue.</param>
    public static void EnqueueLog(Log log)
    {
        // Only enqueue logs if the service has been initialized
        // This prevents logs from accumulating when server logging is disabled
        if (!_isInitialized)
        {
            if (_verboseDiagnostics)
            {
                Console.WriteLine("[LoggingServices] WARNING: Attempted to enqueue log but service not initialized");
            }
            return;
        }
        
        EnqueueBounded(log);

        if (!_firstLogEnqueued)
        {
            _firstLogEnqueued = true;
            Console.WriteLine($"[LoggingServices] First log enqueued. Logs will be uploaded every {_processingIntervalMs/1000}s");
        }
        else if (_verboseDiagnostics)
        {
            Console.WriteLine($"[LoggingServices] Log enqueued. Queue size: {_globalLogQueue.Count}");
        }
    }

    /// <summary>
    /// Gets the global log queue for direct access.
    /// </summary>
    public static ConcurrentQueue<Log> GlobalLogQueue => _globalLogQueue;

    /// <summary>
    /// Number of log entries discarded because the queue reached its configured depth limit.
    /// Non-zero means the server has been unreachable, or logs are being produced faster than
    /// the configured batch size and interval can upload them.
    /// </summary>
    public static long DroppedLogCount => Interlocked.Read(ref _droppedLogCount);

    /// <summary>
    /// Enqueues one entry and trims the front if the queue is over its depth limit, so the newest diagnostics
    /// survive an outage rather than the oldest.
    /// <para>
    /// Every path that adds to the queue goes through here, including the requeue of a failed upload batch.
    /// Requeueing cannot exceed the limit on its own — it puts back at most what the upload attempt just took
    /// out — so this is about keeping one enforcement point rather than fixing a reachable overflow. The limit
    /// matters most while the server is unreachable, which is exactly when requeueing dominates, and a second
    /// unbounded entry point into the queue would be an easy thing to reintroduce later.
    /// </para>
    /// <para>
    /// The check runs after enqueueing and without a lock. <c>Count</c> is a snapshot and concurrent producers
    /// can each observe the same depth, so the queue can sit a few entries either side of the limit — the
    /// intended precision for a backstop whose job is to bound growth, not to hold an exact length.
    /// </para>
    /// </summary>
    private static void EnqueueBounded(Log log)
    {
        _globalLogQueue.Enqueue(log);

        while (_globalLogQueue.Count > _maxQueueDepth && _globalLogQueue.TryDequeue(out _))
        {
            WarnOnDrop(Interlocked.Increment(ref _droppedLogCount));
        }
    }

    /// <summary>
    /// Emits at most one drop warning per minute. Console rather than a logger: the logging pipeline is the
    /// thing that is failing, so routing this through it would enqueue behind the backlog it is reporting.
    /// </summary>
    private static void WarnOnDrop(long totalDropped)
    {
        var now = DateTime.UtcNow.Ticks;
        var last = Interlocked.Read(ref _lastDropWarningTicks);
        if (now - last < TimeSpan.TicksPerMinute)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _lastDropWarningTicks, now, last) != last)
        {
            return;
        }

        Console.Error.WriteLine(
            $"[LoggingServices] Queue is at its {_maxQueueDepth} entry limit; dropping the oldest logs " +
            $"({totalDropped} dropped so far). The server is unreachable, or logs are arriving faster than " +
            $"{_batchSize} per {_processingIntervalMs}ms can upload them.");
    }

    /// <summary>
    /// Initializes the logging services and starts the background processor.
    /// </summary>
    /// <param name="httpClientService">The HTTP client service for sending logs to the server.</param>
    /// <param name="applicationLifetime">Optional hosting lifetime for shutdown handling.</param>
    public static void Initialize(IHttpClientService httpClientService, IHostApplicationLifetime? applicationLifetime = null)
    {
        if (_isInitialized)
        {
            if (_verboseDiagnostics)
            {
                Console.WriteLine("[LoggingServices] Already initialized, skipping");
            }
            return;
        }

        lock (_initLock)
        {
            if (_isInitialized) return;
            
            _httpClientService = httpClientService ?? throw new ArgumentNullException(nameof(httpClientService));

            // Start the background processor
            StartLogProcessor();

            // Register application shutdown handler if hosting is available
            if (applicationLifetime != null)
            {
                applicationLifetime.ApplicationStopping.Register(OnApplicationShutdown);
            }
            
            _isInitialized = true;
            
            Console.WriteLine($"[LoggingServices] Initialized - Upload interval: {_processingIntervalMs/1000}s, max batch size: {_batchSize}");
        }
    }

    /// <summary>
    /// Initializes the logging services using a service provider.
    /// This overload extracts the IHttpClientService from the service provider.
    /// </summary>
    /// <param name="services">The service provider to resolve dependencies from.</param>
    public static void Initialize(IServiceProvider services)
    {
        if (_isInitialized) return;

        lock (_initLock)
        {
            if (_isInitialized) return;

            var httpClientService = services.GetService<IHttpClientService>();
            if (httpClientService == null)
            {
                // Log warning but don't throw - allow graceful degradation
                Console.WriteLine("Warning: IHttpClientService not found in service provider. Logs will be queued but not sent to server.");
                return;
            }

            var lifetime = services.GetService<IHostApplicationLifetime>();
            Initialize(httpClientService, lifetime);
        }
    }

    /// <summary>
    /// Starts the background log processing thread.
    /// </summary>
    private static void StartLogProcessor()
    {
        lock (_processingLock)
        {
            if (_processingThread != null && _processingThread.IsAlive) return;

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            _processingThread = new Thread(() => ProcessLogsThread(token))
            {
                IsBackground = true,
                Name = "LogProcessingThread"
            };
            Console.WriteLine($"[LoggingServices] Starting server log processing thread (interval: {_processingIntervalMs/1000}s, batch size: {_batchSize})...");
            _processingThread.Start();
        }
    }

    /// <summary>
    /// Background thread method that processes logs from the queue.
    /// </summary>
    private static void ProcessLogsThread(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                ProcessLogBatch();

                // WaitHandle.WaitOne is interrupted by Shutdown(); Thread.Sleep is not, and would
                // keep the processor "alive" so the next Initialize skipped starting a new thread.
                if (cancellationToken.WaitHandle.WaitOne(_processingIntervalMs))
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in log processing thread: {ex.Message}");

                if (cancellationToken.WaitHandle.WaitOne(10000))
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Processes a batch of logs from the queue.
    /// Uploads all queued logs (up to batch size) every interval.
    /// </summary>
    private static void ProcessLogBatch()
    {
        if (_globalLogQueue.IsEmpty)
        {
            if (_verboseDiagnostics)
            {
                Console.WriteLine("[LoggingServices] Queue is empty, no logs to process");
            }
            return;
        }
        
        if (_httpClientService == null)
        {
            Console.WriteLine("[LoggingServices] WARNING: HTTP client service is null, cannot upload logs");
            return;
        }

        List<Log> batchToSend = new();
        
        // Dequeue up to batchSize logs
        while (batchToSend.Count < _batchSize && _globalLogQueue.TryDequeue(out var log))
        {
            batchToSend.Add(log);
        }
        
        if (batchToSend.Count == 0)
        {
            if (_verboseDiagnostics)
            {
                Console.WriteLine("[LoggingServices] No logs dequeued from batch");
            }
            return;
        }
        
        // Show upload message
        Console.WriteLine($"[LoggingServices] Uploading batch of {batchToSend.Count} logs, {_globalLogQueue.Count} remaining in queue");
        
        // Track the upload task instead of fire-and-forget
        var uploadTask = SendLogBatchAsync(batchToSend);
        lock (_tasksLock)
        {
            _pendingUploadTasks.Add(uploadTask);
            
            // Clean up completed tasks to prevent memory leak
            _pendingUploadTasks.RemoveAll(t => t.IsCompleted);
        }
    }

    /// <summary>
    /// Sends a batch of logs to the API.
    /// </summary>
    private static async Task SendLogBatchAsync(List<Log> logs)
    {
        if (_httpClientService == null)
        {
            Console.Error.WriteLine("[LoggingServices] ERROR: HTTP client service is not available, log upload failed");
            RequeueLogBatch(logs);
            return;
        }

        try
        {
            if (_verboseDiagnostics)
            {
                Console.WriteLine($"[LoggingServices] Uploading {logs.Count} logs to {_logApiEndpoint}");
            }
            
            var client = await _httpClientService.GetHealthyClientAsync();
            var response = await client.PostAsync(_logApiEndpoint, JsonContent.Create(logs));

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                Console.Error.WriteLine($"[LoggingServices] ERROR: Logger API failed with status {response.StatusCode}");
                Console.Error.WriteLine($"[LoggingServices] Response: {responseBody}");
                RequeueLogBatch(logs);
            }
            else
            {
                // Always show successful upload (not just in verbose mode)
                Console.WriteLine($"[LoggingServices] ✓ Successfully uploaded {logs.Count} logs to server");
                
                // Successful upload - remove retry tracking for these logs
                foreach (var log in logs)
                {
                    if (!string.IsNullOrEmpty(log.Id))
                    {
                        _logRetryCount.TryRemove(log.Id, out _);
                    }
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // HTTP client was disposed - this can happen during shutdown
            // Don't requeue as we're shutting down anyway
            Console.Error.WriteLine("[LoggingServices] HTTP client disposed, skipping log batch");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LoggingServices] ERROR: Logger exception: {ex.Message}");
            if (_verboseDiagnostics)
            {
                Console.Error.WriteLine($"[LoggingServices] Stack trace: {ex.StackTrace}");
            }
            RequeueLogBatch(logs);
        }
    }
    
    /// <summary>
    /// Helper method to re-queue a batch of logs with retry limit.
    /// Logs that exceed MAX_RETRIES are dropped to prevent infinite accumulation.
    /// </summary>
    private static void RequeueLogBatch(List<Log> logs)
    {
        foreach (var log in logs)
        {
            // Skip logs without IDs
            if (string.IsNullOrEmpty(log.Id))
            {
                EnqueueBounded(log);
                continue;
            }

            var retryCount = _logRetryCount.GetOrAdd(log.Id, 0);
            if (retryCount < MAX_RETRIES)
            {
                _logRetryCount[log.Id] = retryCount + 1;
                EnqueueBounded(log);
            }
            else
            {
                // Drop log after max retries to prevent infinite accumulation
                _logRetryCount.TryRemove(log.Id, out _);
                Console.Error.WriteLine($"Dropping log {log.Id} after {MAX_RETRIES} failed attempts");
            }
        }
    }

    /// <summary>
    /// Handles application shutdown by stopping the processor and flushing logs.
    /// </summary>
    public static void OnApplicationShutdown()
    {
        Console.WriteLine("Application shutting down, flushing logs...");
        
        // Stop the background thread
        lock (_processingLock)
        {
            _cancellationTokenSource?.Cancel();
            
            // Wait for the background thread to finish (with timeout)
            if (_processingThread != null && _processingThread.IsAlive)
            {
                Console.WriteLine("Waiting for log processing thread to complete...");
                if (!_processingThread.Join(TimeSpan.FromSeconds(5)))
                {
                    Console.WriteLine("Log processing thread did not complete within timeout, forcing shutdown");
                }
            }

            _processingThread = null;
        }

        // Wait for pending upload tasks to complete (with timeout)
        List<Task> pendingTasks;
        lock (_tasksLock)
        {
            pendingTasks = _pendingUploadTasks.Where(t => !t.IsCompleted).ToList();
        }
        
        if (pendingTasks.Count > 0)
        {
            Console.WriteLine($"Waiting for {pendingTasks.Count} pending log upload tasks to complete...");
            try
            {
                Task.WaitAll(pendingTasks.ToArray(), TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Some log upload tasks did not complete: {ex.Message}");
            }
        }

        // Process remaining logs synchronously
        while (!_globalLogQueue.IsEmpty)
        {
            ProcessLogBatch();
            Thread.Sleep(100);
        }
        
        // Reset initialization flag to allow re-initialization
        lock (_initLock)
        {
            _isInitialized = false;
        }
        
        // Clear retry tracking on shutdown
        _logRetryCount.Clear();
        
        Console.WriteLine("Log flushing completed");
    }
    
    /// <summary>
    /// Manual shutdown method for scenarios where hosting lifetime is not available.
    /// </summary>
    public static void Shutdown()
    {
        OnApplicationShutdown();
    }

    /// <summary>
    /// Configures logging batch settings.
    /// <para>
    /// batchSize / processingIntervalMs is the ceiling on how fast the queue drains. Set it above the rate the
    /// host actually produces logs at, or the queue backlogs for as long as the load lasts and drains at the
    /// configured ceiling afterwards.
    /// </para>
    /// <para>
    /// Both values are read by the background thread on every pass, so a change applies without a restart —
    /// but the thread is already sleeping the previous interval when this is called, so the new cadence starts
    /// one pass late.
    /// </para>
    /// </summary>
    /// <param name="batchSize">Maximum number of logs to send in each batch (default: 500).</param>
    /// <param name="processingIntervalMs">Interval in milliseconds between uploads (default: 2000).</param>
    /// <param name="maxQueueDepth">
    /// Optional cap on queued entries before the oldest are dropped (default: 100,000). This is the backstop
    /// for the server being unreachable, when nothing drains at all regardless of the two values above.
    /// </param>
    public static void ConfigureBatchSettings(int batchSize, int processingIntervalMs, int? maxQueueDepth = null)
    {
        if (batchSize <= 0)
            throw new ArgumentException("Batch size must be positive", nameof(batchSize));

        if (processingIntervalMs <= 0)
            throw new ArgumentException("Processing interval must be positive", nameof(processingIntervalMs));

        if (maxQueueDepth is <= 0)
            throw new ArgumentException("Max queue depth must be positive", nameof(maxQueueDepth));

        if (maxQueueDepth is int depth && depth < batchSize)
            throw new ArgumentException(
                $"Max queue depth ({depth}) must be at least the batch size ({batchSize}), otherwise entries are dropped before a full batch can be assembled.",
                nameof(maxQueueDepth));

        _batchSize = batchSize;
        _processingIntervalMs = processingIntervalMs;

        if (maxQueueDepth is int newDepth)
        {
            _maxQueueDepth = newDepth;
        }

        Console.WriteLine(
            $"[LoggingServices] Settings updated - Upload interval: {_processingIntervalMs}ms, max batch size: {_batchSize} " +
            $"(ceiling {_batchSize * 1000.0 / _processingIntervalMs:F0} logs/sec), max queue depth: {_maxQueueDepth}");
    }
    
    /// <summary>
    /// Enables or disables verbose diagnostic logging.
    /// </summary>
    /// <param name="enabled">Whether to enable verbose diagnostics.</param>
    public static void EnableVerboseDiagnostics(bool enabled = true)
    {
        _verboseDiagnostics = enabled;
        Console.WriteLine($"[LoggingServices] Verbose diagnostics {(enabled ? "enabled" : "disabled")}");
    }
    
    /// <summary>
    /// Gets statistics about the current logging state.
    /// See <see cref="DroppedLogCount"/> for entries discarded at the queue depth limit.
    /// </summary>
    /// <returns>A tuple containing (queued logs count, logs with retries count).</returns>
    public static (int QueuedCount, int RetryingCount) GetLoggingStats()
    {
        return (_globalLogQueue.Count, _logRetryCount.Count);
    }
}
