using Temporalio.Workflows;
using Temporalio.Exceptions;
using Xians.Lib.Agents.Core;

namespace Xians.Examples.A2ACustomWorkflow;

/// <summary>
/// Example custom workflow that demonstrates handling A2A signals, queries, and updates.
/// </summary>
[Workflow("ExampleAgent:DataProcessor")]
public class DataProcessorWorkflow
{
    private readonly Queue<ProcessRequest> _requestQueue = new();
    private readonly Dictionary<string, ProcessResult> _results = new();
    private const int MAX_QUEUE_SIZE = 100;

    [WorkflowRun]
    public async Task RunAsync()
    {
        Workflow.Logger.LogInformation("DataProcessorWorkflow started");
        await ProcessRequestsLoopAsync();
    }

    /// <summary>
    /// Signal handler - fire-and-forget processing.
    /// Other workflows can send requests without waiting for response.
    /// </summary>
    [WorkflowSignal("ProcessData")]
    public Task ProcessDataSignal(ProcessRequest request)
    {
        Workflow.Logger.LogInformation("Received signal: ProcessData for request {RequestId}", request.Id);
        _requestQueue.Enqueue(request);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Query handler - retrieve a specific result.
    /// Does not modify workflow state.
    /// </summary>
    [WorkflowQuery("GetResult")]
    public ProcessResult? GetResult(string requestId)
    {
        Workflow.Logger.LogDebug("Query: GetResult for request {RequestId}", requestId);
        _results.TryGetValue(requestId, out var result);
        return result;
    }

    /// <summary>
    /// Query handler - get overall workflow status.
    /// </summary>
    [WorkflowQuery("GetStatus")]
    public WorkflowStatus GetStatus()
    {
        Workflow.Logger.LogDebug("Query: GetStatus");
        return new WorkflowStatus
        {
            PendingRequests = _requestQueue.Count,
            CompletedRequests = _results.Count,
            IsHealthy = true
        };
    }

    /// <summary>
    /// Update handler - synchronous request-response processing.
    /// Validates, processes, and returns result in one operation.
    /// </summary>
    [WorkflowUpdate("ProcessDataSync")]
    public async Task<ProcessResult> ProcessDataUpdate(ProcessRequest request)
    {
        Workflow.Logger.LogInformation("Received update: ProcessDataSync for request {RequestId}", request.Id);
        
        // Validation happens in validator method
        
        // Process the request
        var result = await ProcessRequestAsync(request);
        
        // Store result
        _results[request.Id] = result;
        
        return result;
    }

    /// <summary>
    /// Validator for ProcessDataSync update.
    /// Runs immediately and can reject requests before processing.
    /// </summary>
    [WorkflowUpdateValidator(nameof(ProcessDataUpdate))]
    public void ValidateProcessData(ProcessRequest request)
    {
        if (string.IsNullOrEmpty(request.Id))
        {
            throw new ApplicationFailureException("Request ID is required");
        }
        
        if (_results.Count >= MAX_QUEUE_SIZE)
        {
            throw new ApplicationFailureException("Queue is full, cannot accept more requests");
        }
    }

    /// <summary>
    /// Main processing loop - handles queued requests from signals.
    /// </summary>
    private async Task ProcessRequestsLoopAsync()
    {
        while (true)
        {
            await Workflow.WaitConditionAsync(() => _requestQueue.Count > 0);
            
            var request = _requestQueue.Dequeue();
            Workflow.Logger.LogInformation("Processing queued request {RequestId}", request.Id);
            
            try
            {
                var result = await ProcessRequestAsync(request);
                _results[request.Id] = result;
                Workflow.Logger.LogInformation("Completed request {RequestId}", request.Id);
            }
            catch (Exception ex)
            {
                Workflow.Logger.LogError(ex, "Error processing request {RequestId}", request.Id);
                _results[request.Id] = new ProcessResult
                {
                    RequestId = request.Id,
                    Status = "Failed",
                    Data = ex.Message,
                    CompletedAt = DateTime.UtcNow
                };
            }
        }
    }

    /// <summary>
    /// Processes a single request (simulated processing).
    /// In a real workflow, this would call activities.
    /// </summary>
    private async Task<ProcessResult> ProcessRequestAsync(ProcessRequest request)
    {
        // Simulate processing time
        await Workflow.DelayAsync(TimeSpan.FromSeconds(1));
        
        return new ProcessResult
        {
            RequestId = request.Id,
            Status = "Completed",
            Data = $"Processed: {request.Data}",
            CompletedAt = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Request model for processing.
/// </summary>
public class ProcessRequest
{
    public string Id { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

/// <summary>
/// Result model for completed processing.
/// </summary>
public class ProcessResult
{
    public string RequestId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
}

/// <summary>
/// Workflow status information.
/// </summary>
public class WorkflowStatus
{
    public int PendingRequests { get; set; }
    public int CompletedRequests { get; set; }
    public bool IsHealthy { get; set; }
}






