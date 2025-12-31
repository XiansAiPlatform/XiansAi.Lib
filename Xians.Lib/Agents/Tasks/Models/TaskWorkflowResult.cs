namespace Xians.Lib.Agents.Tasks.Models;

public record TaskWorkflowResult
{
    public required string TaskId { get; init; }
    public required bool Success { get; init; }
    public string? FinalWork { get; init; }
    public string? RejectionReason { get; init; }
    public DateTime CompletedAt { get; init; }
}

