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



