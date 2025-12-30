using Microsoft.Extensions.Logging;
using Xians.Lib.Common;
using Xians.Lib.Workflows.Models;
using Xians.Lib.Common.MultiTenancy;

namespace Xians.Lib.Workflows.Messaging;

/// <summary>
/// Validates incoming messages for tenant isolation and agent name matching.
/// </summary>
internal static class MessageValidator
{
    /// <summary>
    /// Validates that the workflow tenant matches the handler's tenant (for non-system-scoped agents).
    /// </summary>
    public static bool ValidateTenantIsolation(
        string workflowTenantId,
        WorkflowHandlerMetadata metadata,
        ILogger logger)
    {
        return TenantContext.ValidateTenantIsolation(
            workflowTenantId,
            metadata.TenantId,
            metadata.SystemScoped,
            logger);
    }

    /// <summary>
    /// Validates that the agent name in the message matches the registered handler's agent name.
    /// </summary>
    public static bool ValidateAgentName(
        string? messageAgent,
        WorkflowHandlerMetadata metadata,
        string requestId,
        ILogger logger)
    {
        // Trim message agent to handle whitespace differences (metadata.AgentName already trimmed at registration)
        if (metadata.AgentName != messageAgent?.Trim())
        {
            logger.LogWarning(
                "Agent name mismatch: Expected={ExpectedAgent}, Received={ReceivedAgent}, RequestId={RequestId}",
                metadata.AgentName,
                messageAgent,
                requestId);
            return false;
        }

        return true;
    }
}
