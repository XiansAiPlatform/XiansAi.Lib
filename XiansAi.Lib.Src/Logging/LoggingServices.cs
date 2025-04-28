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

    // Client for sending logs to API
    private static ISecureApiClient? _secureApiClient;
    private static string _logApiEndpoint = "/api/agent/logs";
    private static int _batchSize = 10;
    private static int _processingIntervalMs = 5000;

    /// <summary>
    /// Enqueues a log to the global queue for processing
    /// </summary>
    public static void EnqueueLog(Log log)
    {
        _globalLogQueue.Enqueue(log);
    }

    /// <summary>
    /// Gets the global log queue for direct access
    /// </summary>
    public static ConcurrentQueue<Log> GlobalLogQueue => _globalLogQueue;

    /// <summary>
    /// Initializes the logging services and starts the background processor
    /// </summary>
    public static void Initialize(IServiceProvider services, string logApiEndpoint = "/api/agent/logs", int batchSize = 10, int processingIntervalMs = 5000)
    {
        if (_isInitialized) return;

        lock (_initLock)
        {
            if (_isInitialized) return;

            _logApiEndpoint = logApiEndpoint;
            _batchSize = batchSize;
            _processingIntervalMs = processingIntervalMs;
            _secureApiClient = SecureApi.Instance;

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
        if (_secureApiClient == null) return;

        List<Log> batchToSend = new();
        
        // Dequeue up to batchSize logs
        while (batchToSend.Count < _batchSize && _globalLogQueue.TryDequeue(out var log))
        {
            batchToSend.Add(log);
        }
        
        if (batchToSend.Count == 0) return;
        
        // Fire and forget
        _ = SendLogBatchAsync(batchToSend);
    }

    /// <summary>
    /// Sends a batch of logs to the API
    /// </summary>
    private static async Task SendLogBatchAsync(List<Log> logs)
    {
        if (_secureApiClient == null )
        {
            Console.Error.WriteLine("App server secure API is not available, log upload failed");
            RequeueLogBatch(logs);
            return;
        }

        try
        {
            var client = _secureApiClient.Client;
            var fullUrl = PlatformConfig.APP_SERVER_URL + _logApiEndpoint;
            var response = await client.PostAsync(fullUrl, JsonContent.Create(logs));

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
    /// Helper method to re-queue a batch of logs
    /// </summary>
    private static void RequeueLogBatch(List<Log> logs)
    {
        foreach (var log in logs)
        {
            _globalLogQueue.Enqueue(log);
        }
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
        }

        // Process remaining logs synchronously
        while (!_globalLogQueue.IsEmpty)
        {
            ProcessLogBatch();
            Thread.Sleep(100);
        }
        
        Console.WriteLine("All logs flushed successfully.");
    }
    
    /// <summary>
    /// Manual shutdown method for scenarios where hosting lifetime is not available
    /// </summary>
    public static void Shutdown()
    {
        OnApplicationShutdown();
    }
} 