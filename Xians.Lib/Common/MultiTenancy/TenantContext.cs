using Microsoft.Extensions.Logging;
using Xians.Lib.Common;
using Xians.Lib.Common.Models;
using Xians.Lib.Common.Exceptions;
using Xians.Lib.Common.MultiTenancy.Exceptions;
using Xians.Lib.Agents.Core;

namespace Xians.Lib.Common.MultiTenancy;

/// <summary>
/// Centralized utility for handling tenant context and workflow identifier parsing.
/// Provides consistent tenant extraction, validation, and task queue naming across the library.
/// 
/// Workflow ID Format: {TenantId}:{WorkflowType}:{OptionalSuffix}
/// Examples:
///   - "acme-corp:CustomerService:BuiltIn Workflow:uuid-123"
///   - "contoso:GlobalNotifications:Alerts:uuid-456"
/// </summary>
public static class TenantContext
{
    /// <summary>
    /// Validates and splits a workflow ID into its component parts.
    /// </summary>
    private static string[] ValidateAndSplitWorkflowId(string workflowId, int minParts, string expectedFormat)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
        {
            throw new WorkflowException(WorkflowConstants.ErrorMessages.WorkflowIdNullOrEmpty, null, workflowId);
        }

        var parts = workflowId.Split(':');
        if (parts.Length < minParts)
        {
            throw new WorkflowException(
                $"Invalid WorkflowId format. Expected '{expectedFormat}', got '{workflowId}'",
                null,
                workflowId);
        }

        return parts;
    }

    /// <summary>
    /// Extracts the tenant ID from a workflow ID.
    /// </summary>
    /// <param name="workflowId">The workflow ID in format TenantId:WorkflowType:...</param>
    /// <returns>The extracted tenant ID.</returns>
    /// <exception cref="WorkflowException">Thrown when the workflow ID format is invalid.</exception>
    public static string ExtractTenantId(string workflowId)
    {
        var parts = ValidateAndSplitWorkflowId(workflowId, 2, "TenantId:WorkflowType:...");
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
        var parts = ValidateAndSplitWorkflowId(workflowId, 2, "TenantId:WorkflowType:...");
        // Return the workflow type at position 1
        return parts[1];
    }

    /// <summary>
    /// Generates a task queue name based on system scope and tenant information.
    /// </summary>
    /// <param name="workflowType">The workflow type identifier.</param>
    /// <param name="systemScoped">Whether the workflow is system-scoped.</param>
    /// <param name="tenantId">The tenant ID (required for non-system-scoped workflows).</param>
    /// <param name="agentName">The agent name (required when workflowType starts with "Platform:").</param>
    /// <param name="workflowName">The workflow name (for builtin workflows like "Conversational", "Web").</param>
    /// <returns>The generated task queue name.</returns>
    /// <exception cref="TenantIsolationException">Thrown when tenantId is required but not provided.</exception>
    public static string GetTaskQueueName(string workflowType, bool systemScoped, string? tenantId = null, string? agentName = null, string? workflowName = null)
    {
        // Replace "Platform" with agent name for platform workflows
        var effectiveWorkflowType = workflowType;
        if (workflowType.StartsWith("Platform:") && !string.IsNullOrWhiteSpace(agentName))
        {
            effectiveWorkflowType = workflowType.Replace("Platform:", $"{agentName}:");
            
            // For builtin workflows with a name, append it with a dash
            // e.g., "MyAgent:Builtin Workflow-Conversational"
            if (workflowType == WorkflowConstants.Keys.BuiltinWorkflowType && !string.IsNullOrWhiteSpace(workflowName))
            {
                effectiveWorkflowType = $"{effectiveWorkflowType}-{workflowName}";
            }
        }

        if (systemScoped)
        {
            // System-scoped agents use workflow type as task queue
            // This allows a single worker pool to handle requests from multiple tenants
            return effectiveWorkflowType;
        }

        // Non-system-scoped agents use TenantId:WorkflowType format
        // This ensures tenant isolation at the worker level
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new TenantIsolationException(
                "TenantId is required for non-system-scoped workflows.",
                tenantId,
                null);
        }
        return $"{tenantId}:{effectiveWorkflowType}";
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

    /// <summary>
    /// Parses a workflow ID and extracts all relevant information.
    /// </summary>
    /// <param name="workflowId">The workflow ID to parse.</param>
    /// <returns>A WorkflowIdentifier object containing parsed information.</returns>
    public static WorkflowIdentifier Parse(string workflowId)
    {
        return new WorkflowIdentifier(workflowId);
    }

    /// <summary>
    /// Builds a workflow ID from its component parts.
    /// </summary>
    /// <param name="workflowType">The workflow type (format: "AgentName:WorkflowName").</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="suffix">Optional suffix to make the workflow ID unique (e.g., participantId, scope).</param>
    /// <returns>A fully formed workflow ID.</returns>
    /// <remarks>
    /// Workflow ID Format: {TenantId}:{WorkflowType}:{OptionalSuffix}
    /// Examples:
    ///   - BuildWorkflowId("MyAgent:Chat", "acme-corp") => "acme-corp:MyAgent:Chat"
    ///   - BuildWorkflowId("MyAgent:Chat", "acme-corp", "user-123") => "acme-corp:MyAgent:Chat:user-123"
    ///   - BuildWorkflowId("MyAgent:Chat", "acme-corp", "user-123", "notifications") => "acme-corp:MyAgent:Chat:user-123:notifications"
    /// </remarks>
    public static string BuildWorkflowId(string workflowType, string tenantId, params string?[] suffixParts)
    {
        if (string.IsNullOrWhiteSpace(workflowType))
        {
            throw new ArgumentException(WorkflowConstants.ErrorMessages.WorkflowTypeNullOrEmpty, nameof(workflowType));
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException(WorkflowConstants.ErrorMessages.TenantIdNullOrEmpty, nameof(tenantId));
        }

        var agent = XiansContext.CurrentAgent;

        //if workflowType have : take the second part as workflowType
        if (workflowType.Contains(':'))
        {
            workflowType = workflowType.Split(':')[1];
        }

        var workflowId = $"{tenantId}:{agent.Name}:{workflowType}";

        // Append non-null, non-empty suffix parts
        foreach (var part in suffixParts)
        {
            if (!string.IsNullOrWhiteSpace(part))
            {
                workflowId += $":{part}";
            }
        }

        return workflowId;
    }
}

