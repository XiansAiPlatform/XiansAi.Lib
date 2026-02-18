using Temporalio.Common;
using Temporalio.Workflows;

namespace Xians.Lib.Agents.Tasks.Models;

public record TaskWorkflowRequest
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public string? DraftWork { get; init; }
    
    /// <summary>
    /// User ID of the task participant. If null, inherits from parent workflow's UserId.
    /// </summary>
    public string? ParticipantId { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
    
    /// <summary>
    /// Available actions for this task. If null/empty, defaults to ["approve", "reject"].
    /// </summary>
    public string[]? Actions { get; init; }
    
    /// <summary>
    /// Timeout duration for the task. If null, task waits indefinitely.
    /// </summary>
    public TimeSpan? Timeout { get; init; }


    /// <summary>
    /// Boolean flag to indicate if the task should be abandoned when the parent workflow closes.
    /// </summary>
    public bool? SurviveParentClose { get; init; } = false;


    /// <summary>
    /// Name for the Task Workflow. if already a running workflow exists with the same name, the new workflow will terminate the existing one.
    /// </summary>
    public string TaskName { get; init; } =  Workflow.InWorkflow ? Workflow.NewGuid().ToString() : Guid.NewGuid().ToString();


    /// <summary>
    /// Retry policy for the task. If null, defaults to MaximumAttempts=1.
    /// </summary>
    public RetryPolicy? RetryPolicy { get; init; }
}
