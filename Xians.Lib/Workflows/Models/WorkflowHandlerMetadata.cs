using Xians.Lib.Agents.Messaging;

namespace Xians.Lib.Workflows.Models;

/// <summary>
/// Metadata for a registered workflow handler including tenant isolation information.
/// Made internal to allow activities to access handler registry.
/// </summary>
internal class WorkflowHandlerMetadata
{
    public Func<UserMessageContext, Task>? ChatHandler { get; set; }
    public Func<UserMessageContext, Task>? DataHandler { get; set; }
    public Func<WebhookContext, Task>? WebhookHandler { get; set; }
    public required string AgentName { get; set; }
    public string? TenantId { get; set; }
    public required bool SystemScoped { get; set; }
}

