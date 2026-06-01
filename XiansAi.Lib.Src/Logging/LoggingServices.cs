using Server;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiansAi.Models;
using System.Net.Http.Json;

namespace XiansAi.Logging;

/// <summary>
/// Static class providing logging service management and shutdown handling
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
    
    // Track pending upload tasks for proper shutdown
    private static readonly List<Task> _pendingUploadTasks = new();
    private static readonly object _tasksLock = new object();

    // Client for sending logs to API
    private static ISecureApiClient? _secureApi;
    private static string _logApiEndpoint = "api/agent/logs";
    private static int _batchSize = 100;
    private static int _processingIntervalMs = 60000;

    // Perf (issue #98): bound the queue so that an extended SecureApi outage cannot
    // grow it indefinitely. Previously RequeueLogBatch re-enqueued failed batches
    // forever, so a server outage was a slow OOM. Override via env var.
    private const int DefaultMaxQueueDepth = 50_000;
    private static readonly int _maxQueueDepth = ParseMaxQueueDepth();
    private static long _droppedLogCount;
    private static long _droppedLogWarningCount;

    private static int ParseMaxQueueDepth()
    {
        var raw = Environment.GetEnvironmentVariable("XIANSAI_LOG_QUEUE_MAX");
        if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out var v) && v > 0)
        {
            return v;
        }
        return DefaultMaxQueueDepth;
    }

    /// <summary>
    /// Enqueues a log to the global queue for processing.
    /// If the queue is over its configured depth, the oldest entries are dropped
    /// to prevent unbounded memory growth during an extended upload outage.
    /// </summary>
    public static void EnqueueLog(Log log)
    {
        _globalLogQueue.Enqueue(log);
        TrimQueueIfNeeded();
    }

    private static void TrimQueueIfNeeded()
    {
        var max = _maxQueueDepth;
        if (max <= 0) return; // 0 disables the bound

        // ConcurrentQueue.Count is an O(1) snapshot in .NET; safe to read on the hot path.
        if (_globalLogQueue.Count <= max) return;

        // Drop oldest entries until we are back under the bound. We never block —
        // if another thread drains faster than we trim, that's fine.
        var dropped = 0;
        while (_globalLogQueue.Count > max && _globalLogQueue.TryDequeue(out _))
        {
            dropped++;
        }

        if (dropped > 0)
        {
            Interlocked.Add(ref _droppedLogCount, dropped);
            // Emit at most one warning per 1000 drop events to avoid log spam.
            var warns = Interlocked.Increment(ref _droppedLogWarningCount);
            if (warns == 1 || warns % 1000 == 0)
            {
                Console.Error.WriteLine(
                    $"LoggingServices: log queue exceeded {max} entries; dropped {dropped} oldest entries (total dropped: {Interlocked.Read(ref _droppedLogCount)}).");
            }
        }
    }

    /// <summary>
    /// Gets the global log queue for direct access
    /// </summary>
    public static ConcurrentQueue<Log> GlobalLogQueue => _globalLogQueue;

    /// <summary>
    /// Initializes the logging services and starts the background processor
    /// </summary>
    public static void Initialize(IServiceProvider services)
    {
        if (_isInitialized) return;

        lock (_initLock)
        {
            if (_isInitialized) return;
            _secureApi = SecureApi.IsReady? SecureApi.Instance : null;

            // Start the background processor
            StartLogProcessor();

            // Register application shutdown handler if hosting is available
            var lifetime = services.GetService<IHostApplicationLifetime>();
            if (lifetime != null)
            {
                lifetime.ApplicationStopping.Register(OnApplicationShutdown);
            }
            
            _isInitialized = true;
        }
    }

    /// <summary>
    /// Starts the background log processing thread
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
            _processingThread.Start();
        }
    }

    /// <summary>
    /// Background thread method that processes logs from the queue
    /// </summary>
    private static void ProcessLogsThread(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                ProcessLogBatch();
                
                // Sleep before processing next batch
                Thread.Sleep(_processingIntervalMs);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in log processing thread: {ex.Message}");
                
                // Sleep a bit longer after an error
                Thread.Sleep(10000);
            }
        }
    }

    /// <summary>
    /// Processes a batch of logs from the queue
    /// </summary>
    private static void ProcessLogBatch()
    {
        if (_globalLogQueue.IsEmpty) return;
        
        // Check and reinitialize secure API client if needed
        EnsureSecureApiClient();
        
        if (_secureApi == null) return;

        List<Log> batchToSend = new();
        
        // Dequeue up to batchSize logs
        while (batchToSend.Count < _batchSize && _globalLogQueue.TryDequeue(out var log))
        {
            batchToSend.Add(log);
        }
        
        if (batchToSend.Count == 0) return;
        
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
    /// Ensures the secure API client is initialized and available
    /// </summary>
    private static void EnsureSecureApiClient()
    {
        if (_secureApi == null && SecureApi.IsReady)
        {
            _secureApi = SecureApi.Instance;
        }
    }

    /// <summary>
    /// Sends a batch of logs to the API
    /// </summary>
    private static async Task SendLogBatchAsync(List<Log> logs)
    {
        // Check and reinitialize secure API client if needed
        EnsureSecureApiClient();
        
        if (_secureApi == null)
        {
            Console.Error.WriteLine("App server secure API is not available, log upload failed");
            RequeueLogBatch(logs);
            return;
        }

        try
        {
            var client = _secureApi.Client;
            var response = await client.PostAsync(_logApiEndpoint, JsonContent.Create(logs));

            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"Logger API failed with status {response.StatusCode}");
                RequeueLogBatch(logs);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Logger exception: {ex.Message}");
            RequeueLogBatch(logs);
        }
    }
    
    /// <summary>
    /// Helper method to re-queue a batch of logs.
    /// After re-queueing, trims to the bound (issue #98: previously the requeue path
    /// was unbounded, so a sustained upload outage was a slow OOM).
    /// </summary>
    private static void RequeueLogBatch(List<Log> logs)
    {
        foreach (var log in logs)
        {
            _globalLogQueue.Enqueue(log);
        }
        TrimQueueIfNeeded();
    }

    /// <summary>
    /// Handles application shutdown by stopping the processor and flushing logs
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
        
        Console.WriteLine("Log flushing completed");
    }
    
    /// <summary>
    /// Manual shutdown method for scenarios where hosting lifetime is not available
    /// </summary>
    public static void Shutdown()
    {
        OnApplicationShutdown();
    }
} 