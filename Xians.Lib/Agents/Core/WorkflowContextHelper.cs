using System;
using Temporalio.Activities;
using Temporalio.Workflows;

namespace Xians.Lib.Agents.Core;

/// <summary>
/// Helper for extracting workflow/activity context metadata.
/// Centralizes the pattern of checking Workflow.InWorkflow vs ActivityExecutionContext.HasCurrent.
/// </summary>
public static class WorkflowContextHelper
{
    /// <summary>
    /// Gets the current workflow ID.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    public static string GetWorkflowId()
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
            throw new InvalidOperationException(
                "Not in workflow or activity context. This operation requires Temporal context.");
        }
    }

    /// <summary>
    /// Gets the current workflow type.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    public static string GetWorkflowType()
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
            throw new InvalidOperationException(
                "Not in workflow or activity context. This operation requires Temporal context.");
        }
    }

    /// <summary>
    /// Gets the current workflow run ID.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    public static string GetWorkflowRunId()
    {
        if (Workflow.InWorkflow)
        {
            return Workflow.Info.RunId;
        }
        else if (ActivityExecutionContext.HasCurrent)
        {
            return ActivityExecutionContext.Current.Info.WorkflowRunId;
        }
        else
        {
            throw new InvalidOperationException(
                "Not in workflow or activity context. This operation requires Temporal context.");
        }
    }

    /// <summary>
    /// Gets the current task queue name.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    public static string GetTaskQueue()
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
            throw new InvalidOperationException(
                "Not in workflow or activity context. This operation requires Temporal context.");
        }
    }

    /// <summary>
    /// Extracts the agent name from the current workflow type.
    /// Expected format: "AgentName:WorkflowName"
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <returns>The agent name, or the full workflow type if no separator found.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    public static string GetAgentName()
    {
        var workflowType = GetWorkflowType();
        var separatorIndex = workflowType.IndexOf(':');
        
        if (separatorIndex > 0)
        {
            return workflowType.Substring(0, separatorIndex);
        }
        
        return workflowType; // Fallback to full workflow type
    }

    /// <summary>
    /// Tries to get the current workflow ID without throwing an exception.
    /// </summary>
    /// <returns>The workflow ID if in workflow/activity context, otherwise null.</returns>
    public static string? TryGetWorkflowId()
    {
        try
        {
            return InWorkflowOrActivity ? GetWorkflowId() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Tries to get the current workflow type without throwing an exception.
    /// </summary>
    /// <returns>The workflow type if in workflow/activity context, otherwise null.</returns>
    public static string? TryGetWorkflowType()
    {
        try
        {
            return InWorkflowOrActivity ? GetWorkflowType() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Tries to get the current workflow run ID without throwing an exception.
    /// </summary>
    /// <returns>The workflow run ID if in workflow/activity context, otherwise null.</returns>
    public static string? TryGetWorkflowRunId()
    {
        try
        {
            return InWorkflowOrActivity ? GetWorkflowRunId() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Tries to get the current agent name without throwing an exception.
    /// </summary>
    /// <returns>The agent name if in workflow/activity context, otherwise null.</returns>
    public static string? TryGetAgentName()
    {
        try
        {
            return InWorkflowOrActivity ? GetAgentName() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if the code is currently executing within a Temporal workflow or activity.
    /// </summary>
    public static bool InWorkflowOrActivity =>
        Workflow.InWorkflow || ActivityExecutionContext.HasCurrent;

    /// <summary>
    /// Checks if the code is currently executing within a Temporal workflow.
    /// </summary>
    public static bool InWorkflow => Workflow.InWorkflow;

    /// <summary>
    /// Checks if the code is currently executing within a Temporal activity.
    /// </summary>
    public static bool InActivity => ActivityExecutionContext.HasCurrent;
}






