using Microsoft.Extensions.Logging;
using XiansAi.Flow;
using XiansAi.Activity;
using XiansAi.Logging;
using Server;

namespace Temporal;

/// <summary>
/// Helper class for command line applications to properly manage Temporal connections
/// and handle graceful shutdown
/// </summary>
public static class CommandLineHelper
{
    private static readonly ILogger _logger = Globals.LogFactory.CreateLogger(typeof(CommandLineHelper));
    private static bool _shutdownHandlersSetup = false;
    private static bool _isShuttingDown = false;
    private static readonly object _lock = new();
    private static CancellationTokenSource? _shutdownTokenSource;

    /// <summary>
    /// Gets whether shutdown handlers have been configured
    /// </summary>
    public static bool IsShutdownConfigured() => _shutdownHandlersSetup;
    
    /// <summary>
    /// Gets the shutdown cancellation token
    /// </summary>
    public static CancellationToken GetShutdownToken()
    {
        lock (_lock)
        {
            if (_shutdownTokenSource == null)
            {
                _shutdownTokenSource = new CancellationTokenSource();
            }
            return _shutdownTokenSource.Token;
        }
    }

    /// <summary>
    /// Sets up graceful shutdown handling for the application
    /// This should be called at the start of your Main method
    /// </summary>
    public static void SetupGracefulShutdown()
    {
        lock (_lock)
        {
            if (_shutdownHandlersSetup) return;

            _shutdownTokenSource = new CancellationTokenSource();
            
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            Console.CancelKeyPress += OnCancelKeyPress;
            
            _shutdownHandlersSetup = true;
            _logger.LogInformation("Graceful shutdown handlers configured");
        }
    }

    /// <summary>
    /// Runs a workflow with proper connection management and cleanup
    /// </summary>
    public static async Task RunWorkflowAsync<TFlow>(
        Runner<TFlow> flow, 
        CancellationToken cancellationToken = default) 
        where TFlow : class
    {
        SetupGracefulShutdown();

        var workerService = new WorkerService();
        
        try
        {
            _logger.LogInformation("Starting workflow execution...");
            await workerService.RunFlowAsync(flow, cancellationToken == default ? GetShutdownToken() : cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Workflow execution was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during workflow execution");
            throw;
        }
        finally
        {
            await CleanupResourcesAsync();
        }
    }

    /// <summary>
    /// Manually cleanup all resources - can be called if needed
    /// </summary>
    public static async Task CleanupResourcesAsync()
    {
        lock (_lock)
        {
            if (_isShuttingDown) return;
            _isShuttingDown = true;
        }

        // Create a short timeout for cleanup operations
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var timeoutToken = timeoutCts.Token;

        try
        {
            _logger.LogInformation("Cleaning up application resources...");
            
            // Cleanup Temporal connections with timeout
            _logger.LogInformation("Cleaning up Temporal connections...");
            try
            {
                await TemporalClientService.CleanupAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Temporal cleanup timed out, continuing with shutdown");
            }
            
            // Shutdown logging services
            _logger.LogInformation("Shutting down logging services...");
            LoggingServices.Shutdown();
            
            // Dispose SecureApi if needed
            if (SecureApi.IsReady)
            {
                _logger.LogInformation("Disposing SecureApi...");
                SecureApi.Reset();
            }
            
            _logger.LogInformation("Resource cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during resource cleanup");
        }
    }

    /// <summary>
    /// Diagnostic method to check for potential shutdown blocking issues
    /// Call this if your application is not shutting down properly
    /// </summary>
    public static void DiagnoseShutdownIssues()
    {
        Console.WriteLine("=== Shutdown Diagnostics ===");
        
        var logQueueSize = LoggingServices.GlobalLogQueue.Count;
        Console.WriteLine($"Pending logs in queue: {logQueueSize}");
        
        var temporalConnected = TemporalClientService.Instance != null;
        Console.WriteLine($"Temporal client active: {temporalConnected}");
        
        var secureApiReady = SecureApi.IsReady;
        Console.WriteLine($"SecureApi ready: {secureApiReady}");
        
        // Check thread pool status
        ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);
        ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);
        Console.WriteLine($"ThreadPool - Available: {workerThreads}/{maxWorkerThreads} worker, {completionPortThreads}/{maxCompletionPortThreads} completion");
        
        Console.WriteLine("=== End Diagnostics ===");
    }

    /// <summary>
    /// Force shutdown if normal cleanup fails - use as last resort
    /// </summary>
    public static void ForceShutdown(int exitCode = 0)
    {
        Console.WriteLine("FORCE SHUTDOWN: Normal cleanup failed, forcing application exit");
        try
        {
            DiagnoseShutdownIssues();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during diagnostics: {ex.Message}");
        }
        
        Environment.Exit(exitCode);
    }

    private static async void OnProcessExit(object? sender, EventArgs e)
    {
        _logger.LogInformation("Process exit detected, cleaning up resources...");
        await CleanupResourcesAsync();
    }

    private static async void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        _logger.LogInformation("Ctrl+C detected, initiating graceful shutdown...");
        e.Cancel = true; // Prevent immediate termination
        
        // Signal shutdown to all listeners
        lock (_lock)
        {
            _shutdownTokenSource?.Cancel();
        }
        
        // Try graceful cleanup with timeout
        try
        {
            var cleanupTask = CleanupResourcesAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(20));
            
            var completedTask = await Task.WhenAny(cleanupTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                Console.WriteLine("Graceful shutdown timed out after 20 seconds");
                ForceShutdown(1);
            }
            else
            {
                await cleanupTask; // Ensure any exceptions are observed
                Environment.Exit(0);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during shutdown: {ex.Message}");
            ForceShutdown(1);
        }
    }
} 