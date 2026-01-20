namespace Xians.Lib.Agents.Tasks.Models;

public record TaskWorkflowRequest
{
    public string? TaskId { get; init; }
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
}
