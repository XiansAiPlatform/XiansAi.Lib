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
    
    // Track pending upload tasks for proper shutdown
    private static readonly List<Task> _pendingUploadTasks = new();
    private static readonly object _tasksLock = new object();

    // Client for sending logs to API
    private static IHttpClientService? _httpClientService;
    private static readonly string _logApiEndpoint = WorkflowConstants.ApiEndpoints.Logs;
    private static int _batchSize = 100;
    private static int _processingIntervalMs = 60000; // 60 seconds
    
    // Retry tracking to prevent infinite loops
    private static readonly ConcurrentDictionary<string, int> _logRetryCount = new();
    private const int MAX_RETRIES = 3;

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
            return;
        }
        
        _globalLogQueue.Enqueue(log);
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
        if (_isInitialized) return;

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
    /// Processes a batch of logs from the queue.
    /// </summary>
    private static void ProcessLogBatch()
    {
        if (_globalLogQueue.IsEmpty) return;
        
        if (_httpClientService == null) return;

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
    /// Sends a batch of logs to the API.
    /// </summary>
    private static async Task SendLogBatchAsync(List<Log> logs)
    {
        if (_httpClientService == null)
        {
            Console.Error.WriteLine("HTTP client service is not available, log upload failed");
            RequeueLogBatch(logs);
            return;
        }

        try
        {
            var client = await _httpClientService.GetHealthyClientAsync();
            var response = await client.PostAsync(_logApiEndpoint, JsonContent.Create(logs));

            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"Logger API failed with status {response.StatusCode}");
                RequeueLogBatch(logs);
            }
            else
            {
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
            Console.Error.WriteLine("HTTP client disposed, skipping log batch");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Logger exception: {ex.Message}");
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
    /// <param name="batchSize">Number of logs to send in each batch.</param>
    /// <param name="processingIntervalMs">Interval in milliseconds between batch processing.</param>
    public static void ConfigureBatchSettings(int batchSize, int processingIntervalMs)
    {
        if (batchSize <= 0)
            throw new ArgumentException("Batch size must be positive", nameof(batchSize));
        
        if (processingIntervalMs <= 0)
            throw new ArgumentException("Processing interval must be positive", nameof(processingIntervalMs));

        _batchSize = batchSize;
        _processingIntervalMs = processingIntervalMs;
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
