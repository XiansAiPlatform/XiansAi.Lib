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
    /// <param name="name">The name for the builtin workflow.</param>
    /// <returns>A workflow type in the format "{AgentName}:{name}"</returns>
    public static string BuildBuiltInWorkflowType(string agentName, string name)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            throw new ArgumentException("Agent name cannot be null or empty.", nameof(agentName));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be null or empty.", nameof(name));
        }

        return agentName + ":" + name;
    }

    /// <summary>
    /// Constructs a builtin workflow ID.
    /// </summary>
    /// <param name="agentName">The name of the agent owning the workflow.</param>
    /// <param name="name">The name for the builtin workflow.</param>
    /// <returns>A workflow ID in the format "{AgentName}:{name}"</returns>
    public static string BuildBuiltInWorkflowId(string agentName, string name)
    {
        return XiansContext.TenantId + ":" + BuildBuiltInWorkflowType(agentName, name);
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

