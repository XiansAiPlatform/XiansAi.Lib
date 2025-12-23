using Xians.Lib.Agents;

namespace Xians.Lib.Workflows.Models;

/// <summary>
/// Metadata for a registered workflow handler including tenant isolation information.
/// Made internal to allow activities to access handler registry.
/// </summary>
internal class WorkflowHandlerMetadata
{
    public required Func<UserMessageContext, Task> Handler { get; set; }
    public required string AgentName { get; set; }
    public string? TenantId { get; set; }
    public required bool SystemScoped { get; set; }
}

