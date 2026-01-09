using Temporalio.Activities;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;

namespace Xians.Lib.Temporal.Workflows.A2A;

/// <summary>
/// Activities for executing A2A signals, queries, and updates to custom workflows.
/// </summary>
public class A2ASignalQueryActivities
{
    private readonly A2ASignalQueryService _service;

    public A2ASignalQueryActivities()
    {
        _service = new A2ASignalQueryService();
    }

    /// <summary>
    /// Sends a signal to a workflow.
    /// </summary>
    [Activity]
    public async Task SendSignalAsync(A2ASignalRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "SendSignal activity started: WorkflowId={WorkflowId}, SignalName={SignalName}",
            request.WorkflowId,
            request.SignalName);

        try
        {
            await _service.SendSignalAsync(request.WorkflowId, request.SignalName, request.Args);

            ActivityExecutionContext.Current.Logger.LogInformation(
                "Signal sent successfully: WorkflowId={WorkflowId}, SignalName={SignalName}",
                request.WorkflowId,
                request.SignalName);
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error sending signal: WorkflowId={WorkflowId}, SignalName={SignalName}",
                request.WorkflowId,
                request.SignalName);
            throw;
        }
    }

    /// <summary>
    /// Queries a workflow and returns raw result (non-generic due to Temporal SDK limitations).
    /// Caller must deserialize the result to the expected type.
    /// </summary>
    [Activity("QueryAsync")]
    public async Task<object?> QueryWorkflowAsync(A2AQueryRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "QueryWorkflow activity started: WorkflowId={WorkflowId}, QueryName={QueryName}",
            request.WorkflowId,
            request.QueryName);

        try
        {
            // Call service with object type - result may be JsonElement
            var result = await _service.QueryAsync<object>(
                request.WorkflowId,
                request.QueryName,
                request.Args);

            ActivityExecutionContext.Current.Logger.LogInformation(
                "Query executed successfully: WorkflowId={WorkflowId}, QueryName={QueryName}",
                request.WorkflowId,
                request.QueryName);

            return result;
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error querying workflow: WorkflowId={WorkflowId}, QueryName={QueryName}",
                request.WorkflowId,
                request.QueryName);
            throw;
        }
    }

    /// <summary>
    /// Sends an update to a workflow and returns raw result (non-generic due to Temporal SDK limitations).
    /// Caller must deserialize the result to the expected type.
    /// </summary>
    [Activity("ExecuteUpdateAsync")]
    public async Task<object?> ExecuteUpdateWorkflowAsync(A2AUpdateRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "UpdateWorkflow activity started: WorkflowId={WorkflowId}, UpdateName={UpdateName}",
            request.WorkflowId,
            request.UpdateName);

        try
        {
            // Call service with object type - result may be JsonElement
            var result = await _service.ExecuteUpdateAsync<object>(
                request.WorkflowId,
                request.UpdateName,
                request.Args);

            ActivityExecutionContext.Current.Logger.LogInformation(
                "Update executed successfully: WorkflowId={WorkflowId}, UpdateName={UpdateName}",
                request.WorkflowId,
                request.UpdateName);

            return result;
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error updating workflow: WorkflowId={WorkflowId}, UpdateName={UpdateName}",
                request.WorkflowId,
                request.UpdateName);
            throw;
        }
    }
}

/// <summary>
/// Request model for signal operations.
/// </summary>
public class A2ASignalRequest
{
    public required string WorkflowId { get; set; }
    public required string SignalName { get; set; }
    public object[] Args { get; set; } = Array.Empty<object>();
}

/// <summary>
/// Request model for query operations.
/// </summary>
public class A2AQueryRequest
{
    public required string WorkflowId { get; set; }
    public required string QueryName { get; set; }
    public object[] Args { get; set; } = Array.Empty<object>();
}

/// <summary>
/// Request model for update operations.
/// </summary>
public class A2AUpdateRequest
{
    public required string WorkflowId { get; set; }
    public required string UpdateName { get; set; }
    public object[] Args { get; set; } = Array.Empty<object>();
}

