namespace Xians.Examples.HITLAgent;

/// <summary>
/// What the user asked for in chat, passed into <see cref="HitlTaskWorkflow"/>.
/// </summary>
public class HitlTaskRequest
{
    public required string ParticipantId { get; set; }

    public string? Scope { get; set; }

    public required string Title { get; set; }

    public required string Description { get; set; }

    /// <summary>
    /// Text the reviewer can edit. Falls back to the description when the user did not supply a draft.
    /// </summary>
    public string? DraftWork { get; set; }

    public string[] Actions { get; set; } = ["approve", "reject"];

    /// <summary>
    /// How long the task waits for a person, in seconds. Null waits until someone completes it.
    /// </summary>
    public int? TimeoutSeconds { get; set; }
}
