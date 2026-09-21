namespace Xians.Lib.Temporal.Workflows.Webhooks.Models;

/// <summary>Activity payload for listing builtin webhooks.</summary>
public class WebhookListActivityRequest
{
    public required string AgentName { get; set; }
    public string? ActivationName { get; set; }
}

/// <summary>Activity payload for creating a builtin webhook.</summary>
public class WebhookCreateActivityRequest
{
    public required string AgentName { get; set; }
    public required string ActivationName { get; set; }
    public string? WebhookName { get; set; }
    public string? WorkflowName { get; set; }
    public string? ParticipantId { get; set; }
    public int? TimeoutSeconds { get; set; }
    public string? Name { get; set; }
}
