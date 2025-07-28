using Microsoft.Extensions.Logging;
using XiansAi.Flow;

namespace Temporal;

/// <summary>
/// Helper class for command line applications to properly manage Temporal connections
/// and handle graceful shutdown
/// </summary>
public static class CommandLineHelper
{
    private static readonly ILogger _logger = Globals.LogFactory.CreateLogger(typeof(CommandLineHelper));
    private static bool _shutdownHandlersSetup = false;
    private static readonly object _lock = new();

    /// <summary>
    /// Sets up graceful shutdown handling for the application
    /// This should be called at the start of your Main method
    /// </summary>
    public static void SetupGracefulShutdown()
    {
        lock (_lock)
        {
            if (_shutdownHandlersSetup) return;

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
            await workerService.RunFlowAsync(flow, cancellationToken);
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
        try
        {
            _logger.LogInformation("Cleaning up application resources...");
            await TemporalClientService.CleanupAsync();
            _logger.LogInformation("Resource cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during resource cleanup");
        }
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
        await CleanupResourcesAsync();
        Environment.Exit(0);
    }
} 