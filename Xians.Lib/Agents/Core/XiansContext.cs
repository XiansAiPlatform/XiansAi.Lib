using System.Collections.Concurrent;
using Temporalio.Activities;
using Temporalio.Workflows;
using Xians.Lib.Common.MultiTenancy;

namespace Xians.Lib.Agents.Core;

/// <summary>
/// Static context helper for accessing workflow/activity context and agent registry.
/// Provides convenient access to current workflow information, registered agents, and workflows.
/// </summary>
public static class XiansContext
{
    // Thread-safe registry for agents (workflows are accessible via agents)
    private static readonly ConcurrentDictionary<string, XiansAgent> _agents = new();
    
    // Thread-safe registry for workflows by workflow type
    // Used for quick lookup in workflow/activity context
    private static readonly ConcurrentDictionary<string, XiansWorkflow> _workflows = new();

    #region Workflow/Activity Context

    /// <summary>
    /// Gets the current workflow ID.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    public static string WorkflowId
    {
        get
        {
            if (Workflow.InWorkflow)
            {
                return Workflow.Info.WorkflowId;
            }
            else if (ActivityExecutionContext.HasCurrent)
            {
                return ActivityExecutionContext.Current.Info.WorkflowId;
            }
            else
            {
                throw new InvalidOperationException("Not in workflow or activity context. WorkflowId is only available within Temporal workflows or activities.");
            }
        }
    }

    /// <summary>
    /// Gets the current workflow type.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    public static string WorkflowType
    {
        get
        {
            if (Workflow.InWorkflow)
            {
                return Workflow.Info.WorkflowType;
            }
            else if (ActivityExecutionContext.HasCurrent)
            {
                return ActivityExecutionContext.Current.Info.WorkflowType;
            }
            else
            {
                throw new InvalidOperationException("Not in workflow or activity context. WorkflowType is only available within Temporal workflows or activities.");
            }
        }
    }

    /// <summary>
    /// Gets the current tenant ID extracted from the workflow ID.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    public static string TenantId
    {
        get
        {
            return TenantContext.ExtractTenantId(WorkflowId);
        }
    }

    /// <summary>
    /// Gets the current agent name extracted from the workflow type.
    /// Workflow type format: "AgentName:WorkflowName"
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    public static string AgentName
    {
        get
        {
            var workflowType = WorkflowType;
            var separatorIndex = workflowType.IndexOf(':');

            if (separatorIndex > 0)
            {
                return workflowType.Substring(0, separatorIndex);
            }

            // Fallback: use entire workflow type as agent name
            return workflowType;
        }
    }

    /// <summary>
    /// Gets the task queue name for the current workflow.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    public static string TaskQueue
    {
        get
        {
            if (Workflow.InWorkflow)
            {
                return Workflow.Info.TaskQueue;
            }
            else if (ActivityExecutionContext.HasCurrent)
            {
                return ActivityExecutionContext.Current.Info.TaskQueue;
            }
            else
            {
                throw new InvalidOperationException("Not in workflow or activity context. TaskQueue is only available within Temporal workflows or activities.");
            }
        }
    }

    /// <summary>
    /// Checks if the code is currently executing within a Temporal workflow.
    /// </summary>
    public static bool InWorkflow => Workflow.InWorkflow;

    /// <summary>
    /// Checks if the code is currently executing within a Temporal activity.
    /// </summary>
    public static bool InActivity => ActivityExecutionContext.HasCurrent;

    #endregion

    #region Current Agent Access

    /// <summary>
    /// Gets the current agent instance based on the workflow context.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow/activity context or agent not found.</exception>
    public static XiansAgent CurrentAgent
    {
        get
        {
            var agentName = AgentName;
            if (_agents.TryGetValue(agentName, out var agent))
            {
                return agent;
            }

            throw new InvalidOperationException(
                $"Agent '{agentName}' not found in registry. Ensure the agent is registered with XiansPlatform.");
        }
    }

    /// <summary>
    /// Gets the current workflow instance based on the workflow context.
    /// Useful for accessing workflow-scoped services like Schedules within workflows.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow/activity context or workflow not found.</exception>
    public static XiansWorkflow CurrentWorkflow
    {
        get
        {
            if (!InWorkflow && !InActivity)
            {
                throw new InvalidOperationException(
                    "Not in workflow or activity context. CurrentWorkflow is only available within Temporal workflows or activities.");
            }

            var workflowType = WorkflowType;
            
            if (_workflows.TryGetValue(workflowType, out var workflow))
            {
                return workflow;
            }

            throw new InvalidOperationException(
                $"Workflow '{workflowType}' not found in registry. " +
                $"Ensure the workflow has been registered and is running.");
        }
    }

    #endregion

    #region Agent Registry Access

    /// <summary>
    /// Gets a registered agent by name.
    /// </summary>
    /// <param name="agentName">The name of the agent to retrieve.</param>
    /// <returns>The agent instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when agentName is null or empty.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the agent is not found.</exception>
    public static XiansAgent GetAgent(string agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            throw new ArgumentNullException(nameof(agentName), "Agent name cannot be null or empty.");
        }

        if (_agents.TryGetValue(agentName, out var agent))
        {
            return agent;
        }

        throw new KeyNotFoundException(
            $"Agent '{agentName}' not found. Available agents: {string.Join(", ", _agents.Keys)}");
    }

    /// <summary>
    /// Tries to get a registered agent by name.
    /// </summary>
    /// <param name="agentName">The name of the agent to retrieve.</param>
    /// <param name="agent">The agent instance if found, null otherwise.</param>
    /// <returns>True if the agent was found, false otherwise.</returns>
    public static bool TryGetAgent(string agentName, out XiansAgent? agent)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            agent = null;
            return false;
        }

        return _agents.TryGetValue(agentName, out agent);
    }

    #endregion

    #region Internal Registration

    /// <summary>
    /// Registers an agent in the static registry.
    /// Called internally by XiansPlatform when agents are created.
    /// </summary>
    /// <param name="agent">The agent to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when agent is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an agent with the same name is already registered.</exception>
    internal static void RegisterAgent(XiansAgent agent)
    {
        if (agent == null)
        {
            throw new ArgumentNullException(nameof(agent));
        }

        if (!_agents.TryAdd(agent.Name, agent))
        {
            throw new InvalidOperationException(
                $"Agent '{agent.Name}' is already registered. Each agent must have a unique name.");
        }
    }

    /// <summary>
    /// Registers a workflow in the static registry.
    /// Called internally by XiansWorkflow when workflows are started.
    /// </summary>
    /// <param name="workflowType">The workflow type identifier.</param>
    /// <param name="workflow">The workflow instance to register.</param>
    internal static void RegisterWorkflow(string workflowType, XiansWorkflow workflow)
    {
        if (string.IsNullOrWhiteSpace(workflowType))
        {
            throw new ArgumentNullException(nameof(workflowType));
        }

        if (workflow == null)
        {
            throw new ArgumentNullException(nameof(workflow));
        }

        if (!_workflows.TryAdd(workflowType, workflow))
        {
            // Workflow already registered - this is OK (might be restarting)
            _workflows[workflowType] = workflow;
        }
    }

    /// <summary>
    /// Clears all registered agents and workflows.
    /// Intended for testing purposes only.
    /// </summary>
    internal static void Clear()
    {
        _agents.Clear();
        _workflows.Clear();
    }

    #endregion
}

