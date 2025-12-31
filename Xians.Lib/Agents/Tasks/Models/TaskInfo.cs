namespace Xians.Lib.Agents.Tasks.Models;

public record TaskInfo
{
    public required string TaskId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public string? CurrentDraft { get; init; }
    public required bool Success { get; init; }
    public required bool IsCompleted { get; init; }
    public string? RejectionReason { get; init; }
    public string? ParticipantId { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

