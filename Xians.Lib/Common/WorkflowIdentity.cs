using Xians.Lib.Agents.Core;
using Xians.Lib.Common.MultiTenancy;

namespace Xians.Lib.Common;

/// <summary>
/// Utility class for constructing workflow types and workflow IDs following platform conventions.
/// Centralizes the logic for builtin, platform, and custom workflow identity construction.
/// </summary>
public static class WorkflowIdentity
{
    /// <summary>
    /// Constructs a builtin workflow type identifier.
    /// </summary>
    /// <param name="agentName">The name of the agent owning the workflow.</param>
    /// <param name="name">Optional name for the builtin workflow.</param>
    /// <returns>A workflow type in the format "{AgentName}:BuiltIn Workflow" or "{AgentName}:BuiltIn Workflow - {name}"</returns>
    public static string BuildBuiltInWorkflowType(string agentName, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            throw new ArgumentException("Agent name cannot be null or empty.", nameof(agentName));
        }

        return agentName + ":BuiltIn Workflow" + (name != null ? $" - {name}" : "");
    }

    /// <summary>
    /// Constructs a builtin workflow ID.
    /// </summary>
    /// <param name="agentName">The name of the agent owning the workflow.</param>
    /// <param name="name">Optional name for the builtin workflow.</param>
    /// <returns>A workflow ID in the format "{AgentName}:BuiltIn Workflow" or "{AgentName}:BuiltIn Workflow - {name}"</returns>
    public static string BuildBuiltInWorkflowId(string agentName, string? name = null)
    {
        return XiansContext.TenantId + ":" + BuildBuiltInWorkflowType(agentName, name);
    }

    /// <summary>
    /// Checks if a workflow type is a builtin workflow.
    /// </summary>
    /// <param name="workflowType">The workflow type to check.</param>
    /// <returns>True if the workflow type contains "BuiltIn Workflow", false otherwise.</returns>
    public static bool IsBuiltInWorkflow(string workflowType)
    {
        return workflowType?.Contains("BuiltIn Workflow") ?? false;
    }

}

