using System.Reflection;
using Temporalio.Workflows;
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

    /// <summary>
    /// Gets the workflow type string from a workflow class Type.
    /// Extracts the name from the [Workflow] attribute.
    /// </summary>
    /// <param name="workflowClassType">The Type of the workflow class.</param>
    /// <returns>The workflow type string from the WorkflowAttribute.</returns>
    /// <exception cref="ArgumentNullException">Thrown when workflowClassType is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the workflow class doesn't have a WorkflowAttribute with a Name.</exception>
    public static string GetWorkflowTypeFor(Type workflowClassType)
    {
        if (workflowClassType == null)
        {
            throw new ArgumentNullException(nameof(workflowClassType));
        }

        var workflowAttr = workflowClassType.GetCustomAttribute<WorkflowAttribute>();
        if (workflowAttr?.Name == null)
        {
            throw new InvalidOperationException(
                $"Workflow class '{workflowClassType.Name}' does not have a WorkflowAttribute with a Name property set.");
        }

        return workflowAttr.Name;
    }


}

