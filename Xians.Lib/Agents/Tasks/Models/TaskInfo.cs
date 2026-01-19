namespace Xians.Lib.Agents.Tasks.Models;

public record TaskInfo
{
    public required string TaskId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    
    /// <summary>
    /// The initial draft work when the task was created.
    /// </summary>
    public string? InitialWork { get; init; }
    
    /// <summary>
    /// The current draft work (may have been updated).
    /// </summary>
    public string? FinalWork { get; init; }
    
    public required bool IsCompleted { get; init; }
    public string? ParticipantId { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
    
    /// <summary>
    /// Available actions for this task.
    /// </summary>
    public string[]? AvailableActions { get; init; }
    
    /// <summary>
    /// The action that was performed (if completed).
    /// </summary>
    public string? PerformedAction { get; init; }
    
    /// <summary>
    /// Comment provided with the action.
    /// </summary>
    public string? Comment { get; init; }
}
