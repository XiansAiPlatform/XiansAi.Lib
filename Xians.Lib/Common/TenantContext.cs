using Microsoft.Extensions.Logging;
using Xians.Lib.Common.Models;
using Xians.Lib.Common.Exceptions;

namespace Xians.Lib.Common;

/// <summary>
/// Centralized utility for handling tenant context and workflow identifier parsing.
/// Provides consistent tenant extraction, validation, and task queue naming across the library.
/// 
/// Workflow ID Format: {TenantId}:{WorkflowType}:{OptionalSuffix}
/// Examples:
///   - "acme-corp:CustomerService:Default Workflow:uuid-123"
///   - "contoso:GlobalNotifications:Alerts:uuid-456"
/// </summary>
public static class TenantContext
{
    /// <summary>
    /// Extracts the tenant ID from a workflow ID.
    /// </summary>
    /// <param name="workflowId">The workflow ID in format TenantId:WorkflowType:...</param>
    /// <returns>The extracted tenant ID.</returns>
    /// <exception cref="WorkflowException">Thrown when the workflow ID format is invalid.</exception>
    public static string ExtractTenantId(string workflowId)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
        {
            throw new WorkflowException("WorkflowId cannot be null or empty.", null, workflowId);
        }

        var parts = workflowId.Split(':');
        if (parts.Length < 2)
        {
            throw new WorkflowException(
                $"Invalid WorkflowId format. Expected 'TenantId:WorkflowType:...', got '{workflowId}'",
                null,
                workflowId);
        }

        return parts[0];
    }

    /// <summary>
    /// Extracts the workflow type from a workflow ID.
    /// </summary>
    /// <param name="workflowId">The workflow ID in format TenantId:WorkflowType:...</param>
    /// <returns>The extracted workflow type.</returns>
    /// <exception cref="WorkflowException">Thrown when the workflow ID format is invalid.</exception>
    public static string ExtractWorkflowType(string workflowId)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
        {
            throw new WorkflowException("WorkflowId cannot be null or empty.", null, workflowId);
        }

        var parts = workflowId.Split(':');
        if (parts.Length < 2)
        {
            throw new WorkflowException(
                $"Invalid WorkflowId format. Expected 'TenantId:WorkflowType:...', got '{workflowId}'",
                null,
                workflowId);
        }

        // WorkflowType may contain colons (e.g., "AgentName:FlowName")
        // So we take everything after the first colon and before any additional postfix
        // For backwards compatibility with current format where WorkflowType is at position 1
        return parts[1];
    }

    /// <summary>
    /// Generates a task queue name based on system scope and tenant information.
    /// </summary>
    /// <param name="workflowType">The workflow type identifier.</param>
    /// <param name="systemScoped">Whether the workflow is system-scoped.</param>
    /// <param name="tenantId">The tenant ID (required for non-system-scoped workflows).</param>
    /// <returns>The generated task queue name.</returns>
    /// <exception cref="TenantIsolationException">Thrown when tenantId is required but not provided.</exception>
    public static string GetTaskQueueName(string workflowType, bool systemScoped, string? tenantId = null)
    {
        if (systemScoped)
        {
            // System-scoped agents use workflow type as task queue
            // This allows a single worker pool to handle requests from multiple tenants
            return workflowType;
        }
        else
        {
            // Non-system-scoped agents use TenantId:WorkflowType format
            // This ensures tenant isolation at the worker level
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new TenantIsolationException(
                    "TenantId is required for non-system-scoped workflows.",
                    tenantId,
                    null);
            }
            return $"{tenantId}:{workflowType}";
        }
    }

    /// <summary>
    /// Validates tenant isolation for a workflow.
    /// For system-scoped workflows, always returns true (no validation needed).
    /// For non-system-scoped workflows, validates that the workflow's tenant matches the expected tenant.
    /// </summary>
    /// <param name="workflowTenantId">The tenant ID extracted from the workflow ID.</param>
    /// <param name="expectedTenantId">The expected tenant ID (from agent registration).</param>
    /// <param name="systemScoped">Whether the workflow is system-scoped.</param>
    /// <param name="logger">Optional logger for validation failures.</param>
    /// <returns>True if validation passes, false otherwise.</returns>
    public static bool ValidateTenantIsolation(
        string workflowTenantId, 
        string? expectedTenantId, 
        bool systemScoped,
        ILogger? logger = null)
    {
        if (systemScoped)
        {
            // System-scoped agents can handle multiple tenants - no validation needed
            logger?.LogDebug(
                "System-scoped workflow processing - no tenant validation required. WorkflowTenant={WorkflowTenant}",
                workflowTenantId);
            return true;
        }
        else
        {
            // Non-system-scoped agents must validate tenant isolation
            if (expectedTenantId != workflowTenantId)
            {
                logger?.LogError(
                    "Tenant isolation violation: ExpectedTenant={ExpectedTenant}, WorkflowTenant={WorkflowTenant}",
                    expectedTenantId,
                    workflowTenantId);
                return false;
            }

            logger?.LogDebug(
                "Tenant validation passed: TenantId={TenantId}",
                workflowTenantId);
            return true;
        }
    }

    /// <summary>
    /// Parses a workflow ID and extracts all relevant information.
    /// </summary>
    /// <param name="workflowId">The workflow ID to parse.</param>
    /// <returns>A WorkflowIdentifier object containing parsed information.</returns>
    public static WorkflowIdentifier Parse(string workflowId)
    {
        return new WorkflowIdentifier(workflowId);
    }
}

