namespace Xians.Lib.Agents.Tasks.Models;

public record TaskWorkflowRequest
{
    public string? TaskId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public string? DraftWork { get; init; }
    public required string ParticipantId { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

