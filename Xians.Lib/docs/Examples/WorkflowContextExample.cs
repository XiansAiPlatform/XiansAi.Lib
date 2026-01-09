using Temporalio.Client;
using Xians.Lib.Agents.Core;

namespace Xians.Lib.Examples;

/// <summary>
/// Demonstrates how to use XiansContext.Workflows to access the Temporal client and workflow operations.
/// </summary>
public class WorkflowContextExample
{
    /// <summary>
    /// Example 1: Get Temporal client (simplest approach)
    /// </summary>
    public static async Task GetClientSimpleAsync()
    {
        // After initializing the platform, you can easily get the Temporal client
        // All agents share the same Temporal connection
        var temporalClient = await XiansContext.Workflows.GetClientAsync();
        
        Console.WriteLine($"Connected to namespace: {temporalClient.Options.Namespace}");
    }

    /// <summary>
    /// Example 2: Get Temporal client from any agent context
    /// </summary>
    public static async Task GetClientFromContextAsync()
    {
        // The client is shared across all agents, so it doesn't matter which agent calls it
        var temporalClient = await XiansContext.Workflows.GetClientAsync();
        
        Console.WriteLine("Got Temporal client (shared across all agents)");
    }

    /// <summary>
    /// Example 3: Get workflow handle using GetWorkflowHandleAsync (Recommended)
    /// </summary>
    public static async Task GetWorkflowHandleSimpleAsync()
    {
        // Get workflow handle using the workflow class and ID postfix
        // This automatically constructs the full workflow ID
        var workflowHandle = await XiansContext.Workflows.GetWorkflowHandleAsync<MyWorkflow>(
            idPostfix: "12345"
        );
        
        // Send signal with lambda expression
        await workflowHandle.SignalAsync(wf => wf.ApproveAsync(true));
        
        Console.WriteLine("Signal sent successfully using GetWorkflowHandleAsync");
    }

    /// <summary>
    /// Example 4: Signal a workflow using manual workflow ID
    /// </summary>
    public static async Task SignalWorkflowAsync()
    {
        // Get the Temporal client
        var temporalClient = await XiansContext.Workflows.GetClientAsync();
        
        // Get workflow handle with manually constructed ID
        var workflowHandle = temporalClient.GetWorkflowHandle("tenant123:MyAgent:Task:12345");
        
        // Send signal
        await workflowHandle.SignalAsync("approval-signal", [new { Approved = true }]);
        
        Console.WriteLine("Signal sent successfully");
    }

    /// <summary>
    /// Example 5: Query a workflow using GetWorkflowHandleAsync
    /// </summary>
    public static async Task QueryWorkflowAsync()
    {
        // Get workflow handle (automatically constructs workflow ID)
        var workflowHandle = await XiansContext.Workflows.GetWorkflowHandleAsync<MyWorkflow>(
            idPostfix: "12345"
        );
        
        // Query workflow state
        var status = await workflowHandle.QueryAsync(wf => wf.GetStatusAsync());
        
        Console.WriteLine($"Workflow status: {status}");
    }

    /// <summary>
    /// Example 6: Access Temporal service for health checks
    /// </summary>
    public static async Task HealthCheckAsync()
    {
        // Get the Temporal service (not just the client)
        var temporalService = XiansContext.Workflows.GetService();
        
        // Check connection health
        bool isHealthy = temporalService.IsConnectionHealthy();
        Console.WriteLine($"Connection healthy: {isHealthy}");
        
        // Force reconnection if needed
        if (!isHealthy)
        {
            Console.WriteLine("Connection unhealthy, reconnecting...");
            await temporalService.ForceReconnectAsync();
            Console.WriteLine("Reconnected successfully");
        }
    }

    /// <summary>
    /// Example 7: Complete workflow interaction example using GetWorkflowHandleAsync
    /// </summary>
    public static async Task CompleteWorkflowInteractionAsync()
    {
        // Get workflow handle using GetWorkflowHandleAsync
        var handle = await XiansContext.Workflows.GetWorkflowHandleAsync<DataProcessorWorkflow>(
            idPostfix: "abc123"
        );
        
        // Query current status
        var status = await handle.QueryAsync(wf => wf.GetCurrentStatusAsync());
        Console.WriteLine($"Current status: {status}");
        
        // Send update signal
        await handle.SignalAsync(wf => wf.UpdateDataAsync(new { NewValue = "updated" }));
        Console.WriteLine("Update signal sent");
        
        // Wait a bit and query again
        await Task.Delay(1000);
        status = await handle.QueryAsync(wf => wf.GetCurrentStatusAsync());
        Console.WriteLine($"Updated status: {status}");
    }

    /// <summary>
    /// Example 8: Get workflow handle without idPostfix
    /// </summary>
    public static async Task GetWorkflowHandleWithoutPostfixAsync()
    {
        // If the workflow was created without an idPostfix,
        // you can get its handle by not providing one
        var handle = await XiansContext.Workflows.GetWorkflowHandleAsync<MyWorkflow>();
        
        // The workflow ID will be: {tenantId}:{agentName}:{workflowName}
        // For example: "tenant123:MyAgent:Task"
        
        var status = await handle.QueryAsync(wf => wf.GetStatusAsync());
        Console.WriteLine($"Workflow status: {status}");
    }

    /// <summary>
    /// Example 9: Using from within a workflow
    /// </summary>
    public static async Task UseFromWorkflowAsync()
    {
        // When called from within a workflow or activity, 
        // XiansContext.Workflows automatically uses the current agent
        var temporalClient = await XiansContext.Workflows.GetClientAsync();
        
        // Start another workflow
        await temporalClient.StartWorkflowAsync(
            (OtherWorkflow wf) => wf.RunAsync(),
            new WorkflowOptions
            {
                Id = "some-workflow-id",
                TaskQueue = "my-task-queue"
            });
    }

    /// <summary>
    /// Example 10: Get untyped workflow handle for signaling only
    /// </summary>
    public static async Task GetUntypedWorkflowHandleAsync()
    {
        // If you only need to signal a workflow and don't need typed queries,
        // you can use GetWorkflowHandleUntypedAsync
        var handle = await XiansContext.Workflows.GetWorkflowHandleUntypedAsync<MyWorkflow>(
            idPostfix: "12345"
        );
        
        // Send signal (untyped) - signal name and arguments
        await handle.SignalAsync("approval-signal", [new { Approved = true }]);
        
        Console.WriteLine("Signal sent to workflow");
    }
}

// Example workflow classes for demonstration
public class MyWorkflow
{
    public Task<string> GetStatusAsync() => Task.FromResult("running");
    public Task ApproveAsync(bool approved) => Task.CompletedTask;
}

public class DataProcessorWorkflow
{
    public Task<string> GetCurrentStatusAsync() => Task.FromResult("processing");
    public Task UpdateDataAsync(object data) => Task.CompletedTask;
}

public class OtherWorkflow
{
    public Task RunAsync() => Task.CompletedTask;
}




