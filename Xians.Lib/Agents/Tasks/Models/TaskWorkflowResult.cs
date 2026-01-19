namespace Xians.Lib.Agents.Tasks.Models;

public record TaskWorkflowResult
{
    public required string TaskId { get; init; }
    
    /// <summary>
    /// The initial draft work when the task was created.
    /// </summary>
    public string? InitialWork { get; init; }
    
    /// <summary>
    /// The final work when the task was completed.
    /// </summary>
    public string? FinalWork { get; init; }
    
    public DateTime CompletedAt { get; init; }
    
    /// <summary>
    /// The action that was performed to complete this task.
    /// </summary>
    public string? PerformedAction { get; init; }
    
    /// <summary>
    /// Comment provided with the action.
    /// </summary>
    public string? Comment { get; init; }
}
