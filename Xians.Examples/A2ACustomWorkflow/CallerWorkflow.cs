using Temporalio.Workflows;
using Xians.Lib.Agents.Core;

namespace Xians.Examples.A2ACustomWorkflow;

/// <summary>
/// Example workflow that demonstrates calling a custom workflow via A2A signals, queries, and updates.
/// Shows all three patterns: fire-and-forget, read-only query, and synchronous update.
/// </summary>
[Workflow("ExampleAgent:Caller")]
public class CallerWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(string targetWorkflowId)
    {
        Workflow.Logger.LogInformation("CallerWorkflow started, targeting workflow: {WorkflowId}", targetWorkflowId);

        // Get reference to the target workflow
        var dataProcessor = XiansContext.GetCustomWorkflowReference<DataProcessorWorkflow>(targetWorkflowId);

        // ===== Pattern 1: Fire-and-Forget Signal =====
        // Send request without waiting for result
        Workflow.Logger.LogInformation("Sending fire-and-forget signal...");
        await XiansContext.A2A.SendSignalAsync(
            dataProcessor,
            "ProcessData",
            new ProcessRequest 
            { 
                Id = "async-req-1", 
                Data = "async processing data" 
            });

        Workflow.Logger.LogInformation("Signal sent, continuing without waiting for result");

        // ===== Pattern 2: Synchronous Update =====
        // Send request and wait for result
        Workflow.Logger.LogInformation("Sending synchronous update...");
        var syncResult = await XiansContext.A2A.UpdateAsync<ProcessResult>(
            dataProcessor,
            "ProcessDataSync",
            new ProcessRequest 
            { 
                Id = "sync-req-1", 
                Data = "synchronous processing data" 
            });

        Workflow.Logger.LogInformation(
            "Received sync result: Status={Status}, Data={Data}", 
            syncResult.Status, 
            syncResult.Data);

        // ===== Pattern 3: Query Status =====
        // Read workflow state without modification
        Workflow.Logger.LogInformation("Querying workflow status...");
        var status = await XiansContext.A2A.QueryAsync<WorkflowStatus>(
            dataProcessor,
            "GetStatus");

        Workflow.Logger.LogInformation(
            "Workflow status - Pending: {Pending}, Completed: {Completed}, Healthy: {Healthy}",
            status.PendingRequests,
            status.CompletedRequests,
            status.IsHealthy);

        // ===== Pattern 4: Signal + Delayed Query =====
        // Send signal, wait, then query for result
        Workflow.Logger.LogInformation("Testing signal + delayed query pattern...");
        
        var requestId = "async-req-2";
        await XiansContext.A2A.SendSignalAsync(
            dataProcessor,
            "ProcessData",
            new ProcessRequest { Id = requestId, Data = "delayed query test" });

        // Wait for processing (in real scenario, might poll or wait for specific condition)
        await Workflow.DelayAsync(TimeSpan.FromSeconds(2));

        // Query for the result
        var delayedResult = await XiansContext.A2A.QueryAsync<ProcessResult>(
            dataProcessor,
            "GetResult",
            requestId);

        if (delayedResult != null)
        {
            Workflow.Logger.LogInformation(
                "Retrieved delayed result: Status={Status}, Data={Data}",
                delayedResult.Status,
                delayedResult.Data);
        }
        else
        {
            Workflow.Logger.LogWarning("Delayed result not found for request {RequestId}", requestId);
        }

        // ===== Pattern 5: Error Handling =====
        // Demonstrate validation failure
        Workflow.Logger.LogInformation("Testing error handling...");
        try
        {
            // This should fail validation (empty ID)
            await XiansContext.A2A.UpdateAsync<ProcessResult>(
                dataProcessor,
                "ProcessDataSync",
                new ProcessRequest { Id = "", Data = "invalid request" });
        }
        catch (Exception ex)
        {
            Workflow.Logger.LogInformation("Caught expected validation error: {Message}", ex.Message);
        }

        Workflow.Logger.LogInformation("CallerWorkflow completed successfully");
    }
}



