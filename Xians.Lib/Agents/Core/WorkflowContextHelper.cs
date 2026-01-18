using System;
using Temporalio.Activities;
using Temporalio.Common;
using Temporalio.Converters;
using Temporalio.Workflows;
using Xians.Lib.Common;

namespace Xians.Lib.Agents.Core;

/// <summary>
/// Helper for extracting workflow/activity context metadata.
/// Centralizes the pattern of checking Workflow.InWorkflow vs ActivityExecutionContext.HasCurrent.
/// </summary>
public static class WorkflowContextHelper
{

    /// <summary>
    /// Gets the idPostfix from search attributes, memo, or workflow ID.
    /// Tries in order: search attributes → memo → workflow ID parsing.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    public static string GetIdPostfix()
    {
        // Try to get from search attributes first (most reliable in workflow context)
        var fromSearchAttrs = GetFromSearchAttributes();
        if (!string.IsNullOrEmpty(fromSearchAttrs))
            return fromSearchAttrs;

        // Try to get from memo (works in both workflow and activity contexts)
        var fromMemo = GetFromMemo();
        if (!string.IsNullOrEmpty(fromMemo))
            return fromMemo;

        // Fall back to parsing workflow ID (legacy support)
        return GetFromWorkflowId();
    }

    /// <summary>
    /// Attempts to get idPostfix from search attributes (workflow context only).
    /// </summary>
    private static string? GetFromSearchAttributes()
    {
        try
        {
            if (Workflow.InWorkflow)
            {
                var searchAttrs = Workflow.TypedSearchAttributes;
                if (searchAttrs != null)
                {
                    var key = SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.idPostfix);
                    return searchAttrs.Get(key);
                }
            }
            // Note: Activities don't have direct access to search attributes
        }
        catch
        {
            // Search attribute doesn't exist or wrong type, continue to next method
        }
        return null;
    }

    /// <summary>
    /// Attempts to get idPostfix from workflow memo.
    /// Works in workflow context only (activities inherit parent workflow ID).
    /// </summary>
    private static string? GetFromMemo()
    {
        try
        {
            if (Workflow.InWorkflow)
            {
                if (Workflow.Memo.TryGetValue(WorkflowConstants.Keys.idPostfix, out var value))
                {
                    return value.Payload.Data.ToStringUtf8()?.Replace("\"", "");
                }
            }
            // Note: Activities don't have access to memo directly, they use the parent workflow's ID
        }
        catch
        {
            // Memo doesn't exist or can't be parsed, continue to next method
        }
        return null;
    }

    /// <summary>
    /// Parses idPostfix from workflow ID as fallback.
    /// Workflow ID format: {tenantId}:{agentName}:{workflowName}:{idPostfix}
    /// Works in both workflow and activity contexts.
    /// </summary>
    private static string GetFromWorkflowId()
    {
        var workflowId = GetWorkflowId();
        var parts = workflowId.Split(':');
        if (parts.Length < 4)
        {
            return string.Empty;
        }
        return parts[parts.Length - 1];
    }
    
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






