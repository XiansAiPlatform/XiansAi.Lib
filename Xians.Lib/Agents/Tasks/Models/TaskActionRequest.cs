namespace Xians.Lib.Agents.Tasks.Models;

/// <summary>
/// Request to perform an action on a task.
/// </summary>
public record TaskActionRequest
{
    /// <summary>
    /// The action to perform (should be one of the task's available actions).
    /// </summary>
    public required string Action { get; init; }
    
    /// <summary>
    /// Optional comment from the human performing the action.
    /// </summary>
    public string? Comment { get; init; }
}
