using Microsoft.Extensions.Logging;
using Agentri.Flow;
using Agentri.Activity;
using Agentri.Logging;
using Agentri.Server;

namespace Temporal;

/// <summary>
/// Helper class for command line applications to properly manage Temporal connections
/// and handle graceful shutdown
/// </summary>
public static class CommandLineHelper
{
    private static readonly ILogger _logger = Globals.LogFactory.CreateLogger(typeof(CommandLineHelper));
    private static volatile bool _shutdownHandlersSetup = false;
    private static volatile bool _isShuttingDown = false;
    private static volatile bool _cleanupInProgress = false;
    private static readonly object _lock = new();
    private static readonly object _cleanupLock = new();
    private static CancellationTokenSource? _shutdownTokenSource;
    private static TaskCompletionSource<bool>? _cleanupCompletionSource;

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
    /// Thread-safe and prevents multiple concurrent cleanup operations
    /// </summary>
    public static async Task CleanupResourcesAsync()
    {
        TaskCompletionSource<bool>? currentCleanupTask = null;
        
        lock (_cleanupLock)
        {
            // If cleanup is already in progress, wait for it to complete
            if (_cleanupInProgress)
            {
                currentCleanupTask = _cleanupCompletionSource;
            }
            else
            {
                // Mark cleanup as in progress and create completion source
                _cleanupInProgress = true;
                _isShuttingDown = true;
                _cleanupCompletionSource = new TaskCompletionSource<bool>();
            }
        }

        // If cleanup is already running, wait for it to complete
        if (currentCleanupTask != null)
        {
            try
            {
                await currentCleanupTask.Task;
            }
            catch
            {
                // Ignore exceptions from other cleanup operations
            }
            return;
        }

        // Perform the actual cleanup
        try
        {
            await PerformCleanupAsync();
            _cleanupCompletionSource?.SetResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during resource cleanup");
            _cleanupCompletionSource?.SetException(ex);
            throw;
        }
    }

    /// <summary>
    /// Internal method that performs the actual cleanup operations
    /// </summary>
    private static async Task PerformCleanupAsync()
    {
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
                await TemporalClientService.CleanupAsync().WaitAsync(TimeSpan.FromSeconds(5), timeoutToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Temporal cleanup timed out, continuing with shutdown");
            }
            catch (OperationCanceledException) when (timeoutToken.IsCancellationRequested)
            {
                _logger.LogWarning("Temporal cleanup was cancelled due to timeout, continuing with shutdown");
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
            
            _logger.LogInformation("Resource cleanup completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during resource cleanup");
            throw; // Re-throw to ensure cleanup completion source gets the exception
        }
    }

    /// <summary>
    /// Diagnostic method to check for potential shutdown blocking issues
    /// Call this if your application is not shutting down properly
    /// </summary>
    public static void DiagnoseShutdownIssues()
    {
        Console.WriteLine("=== Shutdown Diagnostics ===");
        
        Console.WriteLine($"Shutdown handlers setup: {_shutdownHandlersSetup}");
        Console.WriteLine($"Is shutting down: {_isShuttingDown}");
        Console.WriteLine($"Cleanup in progress: {_cleanupInProgress}");
        
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
    /// Resets the CommandLineHelper state - primarily for testing purposes
    /// WARNING: Only use this in test scenarios, not in production code
    /// </summary>
    internal static void ResetForTesting()
    {
        lock (_lock)
        {
            lock (_cleanupLock)
            {
                _shutdownHandlersSetup = false;
                _isShuttingDown = false;
                _cleanupInProgress = false;
                
                _shutdownTokenSource?.Dispose();
                _shutdownTokenSource = null;
                
                _cleanupCompletionSource = null;
                
                // Remove event handlers if they were set up
                try
                {
                    AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
                    Console.CancelKeyPress -= OnCancelKeyPress;
                }
                catch
                {
                    // Ignore errors when removing handlers that might not be registered
                }
            }
        }
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

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        // Prevent multiple ProcessExit handlers from running simultaneously
        if (_isShuttingDown) return;
        
        _logger.LogInformation("Process exit detected, cleaning up resources...");
        
        // Use a timeout to prevent hanging during process exit
        try
        {
            var cleanupTask = CleanupResourcesAsync();
            if (!cleanupTask.Wait(TimeSpan.FromSeconds(5)))
            {
                _logger.LogWarning("Process exit cleanup timed out after 5 seconds");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during process exit cleanup");
        }
    }

    private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        _logger.LogInformation("Ctrl+C detected, initiating graceful shutdown...");
        e.Cancel = true; // Prevent immediate termination
        
        // Signal shutdown to all listeners
        lock (_lock)
        {
            _shutdownTokenSource?.Cancel();
        }
        
        // Start cleanup in a separate task to avoid blocking the event handler
        Task.Run(async () =>
        {
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
                    try
                    {
                        await cleanupTask; // Ensure any exceptions are observed
                        Environment.Exit(0);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during cleanup: {ex.Message}");
                        ForceShutdown(1);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during shutdown: {ex.Message}");
                ForceShutdown(1);
            }
        });
    }
} 