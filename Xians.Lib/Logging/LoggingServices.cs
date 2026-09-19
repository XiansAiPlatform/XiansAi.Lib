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
    private static int _batchSize = 100;
    private static int _processingIntervalMs = 30000; // 30 seconds - how often to upload logs
    
    // Retry tracking to prevent infinite loops
    private static readonly ConcurrentDictionary<string, int> _logRetryCount = new();
    private const int MAX_RETRIES = 3;
    
    // Diagnostics
    private static bool _verboseDiagnostics = false;

    // Track first log enqueued for diagnostics
    private static bool _firstLogEnqueued = false;

    // Upload failure reporting. When the log server is unreachable every batch is requeued and nothing
    // drains, so the failure path runs on every cycle for as long as the outage lasts — reported without a
    // limit that is a stdout flood precisely when an operator is trying to read the console. These fields
    // collapse a streak into its first line plus one repeat per interval, carrying the count so the
    // magnitude stays visible. Access is normally confined to the log-processing thread and its upload
    // continuations, but Shutdown can drain on the caller's thread at the same time, hence the interlocked
    // counter.
    private const int MAX_RESPONSE_BODY_CHARS = 500;
    private static readonly TimeSpan FAILURE_REPORT_INTERVAL = TimeSpan.FromMinutes(5);
    private static int _consecutiveUploadFailures;
    private static DateTime _lastFailureReportUtc = DateTime.MinValue;
    
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
        
        _globalLogQueue.Enqueue(log);
        
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

            // A new session starts with a clean failure streak, so its first upload failure reports
            // immediately rather than landing inside a suppression window left by the previous one.
            Interlocked.Exchange(ref _consecutiveUploadFailures, 0);
            _lastFailureReportUtc = DateTime.MinValue;

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
            // A thread left over from a previous session can still be alive while already cancelled — it is
            // about to stop on its own token. Treating that as "a processor is running" left the service
            // with none at all: the old thread exited moments later and nothing started a replacement, so
            // after a Shutdown/Initialize pair logs queued forever and were never uploaded. Only an
            // uncancelled thread counts as running.
            if (_processingThread is { IsAlive: true } && _cancellationTokenSource is { IsCancellationRequested: false })
            {
                return;
            }

            // Give a cancelled predecessor a moment to exit. If it does not, starting a replacement is still
            // correct: the old one stops at the top of its next loop iteration and does no work meanwhile.
            if (_processingThread is { IsAlive: true })
            {
                _processingThread.Join(TimeSpan.FromSeconds(1));
            }

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

                // Waits on the token rather than sleeping blind, so cancellation is observed immediately.
                // A blind sleep meant Shutdown waited out the remainder of the interval — up to the error
                // backoff below — and then reported the thread as hung on the console.
                cancellationToken.WaitHandle.WaitOne(_processingIntervalMs);
            }
            catch (Exception ex)
            {
                ReportUploadFailure($"ERROR: log processing thread failed: {ex.Message}");

                // Back off after an error, still observing cancellation.
                cancellationToken.WaitHandle.WaitOne(10000);
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
            // A misconfigured host hits this on every cycle for the life of the process, so it goes through
            // the same rate limiter as the upload failures below.
            ReportUploadFailure("WARNING: HTTP client service is null, cannot upload logs");
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
        
        // Diagnostic only. This writes straight to stdout rather than through an ILogger, so a host has no
        // log level, category filter or environment variable that can reach it — the neighbouring
        // diagnostics in this class are gated for that reason, and this one was missed. Uploading happens on
        // a fixed interval for as long as the process lives, so ungated it is unconditional console traffic.
        if (_verboseDiagnostics)
        {
            Console.WriteLine($"[LoggingServices] Uploading batch of {batchToSend.Count} logs, {_globalLogQueue.Count} remaining in queue");
        }
        
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
            ReportUploadFailure("ERROR: HTTP client service is not available, log upload failed");
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
                ReportUploadFailure(
                    $"ERROR: Logger API failed with status {response.StatusCode}. Response: {TruncateResponseBody(responseBody)}");
                RequeueLogBatch(logs);
            }
            else
            {
                // Diagnostic only — a successful upload is the expected outcome and says nothing an
                // operator needs. Failures above stay unconditional on stderr, so silencing this does not
                // hide a problem. See the matching gate in ProcessLogBatch.
                if (_verboseDiagnostics)
                {
                    Console.WriteLine($"[LoggingServices] ✓ Successfully uploaded {logs.Count} logs to server");
                }

                // Ends any suppression window and reports the recovery if a streak was running.
                ReportUploadRecovered();

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
            ReportUploadFailure("HTTP client disposed, skipping log batch");
        }
        catch (Exception ex)
        {
            ReportUploadFailure($"ERROR: Logger exception: {ex.Message}");
            if (_verboseDiagnostics)
            {
                Console.Error.WriteLine($"[LoggingServices] Stack trace: {ex.StackTrace}");
            }
            RequeueLogBatch(logs);
        }
    }
    
    /// <summary>
    /// Writes an upload failure to stderr, at most once per <see cref="FAILURE_REPORT_INTERVAL"/> while a
    /// failure streak continues. The first failure of a streak always reports.
    /// </summary>
    /// <remarks>
    /// Nothing is hidden by this: the first occurrence is immediate, and every suppressed repeat is counted
    /// into the streak that the next report and the recovery line both carry. What it removes is the
    /// thousands of identical lines an hour-long outage used to produce.
    /// </remarks>
    /// <param name="message">The failure description, already formatted.</param>
    private static void ReportUploadFailure(string message)
    {
        var failures = Interlocked.Increment(ref _consecutiveUploadFailures);
        var now = DateTime.UtcNow;

        if (failures > 1 && now - _lastFailureReportUtc < FAILURE_REPORT_INTERVAL)
        {
            return;
        }

        _lastFailureReportUtc = now;

        var suffix = failures > 1
            ? $" ({failures} consecutive failures; further reports suppressed for {FAILURE_REPORT_INTERVAL.TotalMinutes:0} minutes)"
            : string.Empty;

        Console.Error.WriteLine($"[LoggingServices] {message}{suffix}");
    }

    /// <summary>
    /// Clears the failure streak after a successful upload, reporting the recovery when there was one to
    /// recover from — otherwise an outage would end as silently as it was suppressed.
    /// </summary>
    private static void ReportUploadRecovered()
    {
        var failures = Interlocked.Exchange(ref _consecutiveUploadFailures, 0);
        if (failures > 0)
        {
            Console.Error.WriteLine($"[LoggingServices] Log upload recovered after {failures} consecutive failures");
        }
    }

    /// <summary>
    /// Trims an error response body to <see cref="MAX_RESPONSE_BODY_CHARS"/> before it reaches the console.
    /// A rejected request can come back with an arbitrarily large body (an HTML error page, a full validation
    /// report), and the first few hundred characters are what identifies the problem.
    /// </summary>
    /// <param name="body">Raw response body.</param>
    private static string TruncateResponseBody(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return "(empty)";
        }

        return body.Length <= MAX_RESPONSE_BODY_CHARS
            ? body
            : string.Concat(body.AsSpan(0, MAX_RESPONSE_BODY_CHARS), $"… (truncated, {body.Length} chars total)");
    }

    /// <summary>
    /// Helper method to re-queue a batch of logs with retry limit.
    /// Logs that exceed MAX_RETRIES are dropped to prevent infinite accumulation.
    /// </summary>
    private static void RequeueLogBatch(List<Log> logs)
    {
        // Counted, then reported once for the batch. This used to write a line per dropped entry, inside a
        // loop over a batch of up to _batchSize logs — so a sustained outage, where every batch is requeued
        // until its entries exhaust MAX_RETRIES, produced up to _batchSize stderr lines per cycle. The
        // individual ids identified nothing actionable: they are ids of log entries that never reached the
        // server, so there is nothing to look them up in.
        var droppedCount = 0;

        foreach (var log in logs)
        {
            // Skip logs without IDs
            if (string.IsNullOrEmpty(log.Id))
            {
                _globalLogQueue.Enqueue(log);
                continue;
            }
            
            var retryCount = _logRetryCount.GetOrAdd(log.Id, 0);
            if (retryCount < MAX_RETRIES)
            {
                _logRetryCount[log.Id] = retryCount + 1;
                _globalLogQueue.Enqueue(log);
            }
            else
            {
                // Drop log after max retries to prevent infinite accumulation
                _logRetryCount.TryRemove(log.Id, out _);
                droppedCount++;
            }
        }

        if (droppedCount > 0)
        {
            Console.Error.WriteLine(
                $"[LoggingServices] Dropped {droppedCount} log(s) after {MAX_RETRIES} failed upload attempts");
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
    /// </summary>
    /// <param name="batchSize">Maximum number of logs to send in each batch (default: 100).</param>
    /// <param name="processingIntervalMs">Interval in milliseconds between uploads (default: 30000).</param>
    public static void ConfigureBatchSettings(int batchSize, int processingIntervalMs)
    {
        if (batchSize <= 0)
            throw new ArgumentException("Batch size must be positive", nameof(batchSize));
        
        if (processingIntervalMs <= 0)
            throw new ArgumentException("Processing interval must be positive", nameof(processingIntervalMs));

        _batchSize = batchSize;
        _processingIntervalMs = processingIntervalMs;
        
        Console.WriteLine($"[LoggingServices] Settings updated - Upload interval: {_processingIntervalMs/1000}s, max batch size: {_batchSize}");
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
    /// </summary>
    /// <returns>A tuple containing (queued logs count, logs with retries count).</returns>
    public static (int QueuedCount, int RetryingCount) GetLoggingStats()
    {
        return (_globalLogQueue.Count, _logRetryCount.Count);
    }
}
