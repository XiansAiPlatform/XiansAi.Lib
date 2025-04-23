using Server;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace XiansAi.Logging;

/// <summary>
/// Static class providing logging service management and shutdown handling
/// </summary>
public static class LoggingServices
{
    private static readonly ConcurrentDictionary<string, LogQueue> _logQueues = new();
    private static bool _isInitialized = false;
    private static readonly object _initLock = new object();

    /// <summary>
    /// Gets or creates a LogQueue for the specified endpoint
    /// </summary>
    public static LogQueue GetOrCreateLogQueue(string logApiUrl)
    {
        return _logQueues.GetOrAdd(logApiUrl, url => new LogQueue(SecureApi.Instance, url));
    }

    /// <summary>
    /// Initializes the logging services and registers shutdown handlers
    /// </summary>
    public static void Initialize(IServiceProvider services)
    {
        if (_isInitialized) return;

        lock (_initLock)
        {
            if (_isInitialized) return;

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
    /// Handles application shutdown by flushing all log queues
    /// </summary>
    public static void OnApplicationShutdown()
    {
        Console.WriteLine("Application shutting down, flushing logs...");
        
        // Create a list of tasks for flushing all queues
        var flushTasks = _logQueues.Values.Select(queue => queue.FlushAllAsync()).ToArray();
        
        try
        {
            // Wait for all flush tasks to complete with a timeout
            Task.WhenAll(flushTasks).Wait(TimeSpan.FromSeconds(60));
            
            // Dispose all queues
            foreach (var queue in _logQueues.Values)
            {
                queue.Dispose();
            }
            
            _logQueues.Clear();
            Console.WriteLine("All logs flushed successfully.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error flushing logs during shutdown: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Manual shutdown method for scenarios where hosting lifetime is not available
    /// </summary>
    public static void Shutdown()
    {
        OnApplicationShutdown();
    }
} 