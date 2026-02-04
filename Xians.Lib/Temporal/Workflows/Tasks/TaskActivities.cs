using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Client;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Tasks;
using Xians.Lib.Agents.Tasks.Models;
using Xians.Lib.Common.Exceptions;

namespace Xians.Lib.Temporal.Workflows.Tasks;

/// <summary>
/// Activities for task operations.
/// Activities can perform non-deterministic operations like querying and signaling workflows.
/// Delegates to shared TaskService to avoid code duplication.
/// </summary>
public class TaskActivities
{
    private readonly ITemporalClient _client;

    public TaskActivities(ITemporalClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Queries the current status of a task workflow.
    /// </summary>
    [Activity]
    public async Task<TaskInfo> QueryTaskInfoAsync(string tenantId, string taskId)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "QueryTaskInfo activity started: TaskId={TaskId}, TenantId={TenantId}",
            taskId,
            tenantId);

        try
        {
            var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<TaskService>();
            var agentName = XiansContext.CurrentAgent?.Name 
                ?? throw new InvalidOperationException("Agent name not available in activity context");
            var taskService = new TaskService(_client, agentName, tenantId, logger);
            
            var taskInfo = await taskService.QueryTaskInfoAsync(taskId);

            ActivityExecutionContext.Current.Logger.LogDebug(
                "Task info queried successfully: TaskId={TaskId}",
                taskId);

            return taskInfo;
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error querying task info: TaskId={TaskId}",
                taskId);
            throw new ActivityExecutionException(
                $"Failed to query task info for TaskId='{taskId}'",
                activityName: nameof(QueryTaskInfoAsync),
                tenantId: tenantId,
                innerException: ex);
        }
    }

    /// <summary>
    /// Sends a signal to update the draft work.
    /// </summary>
    [Activity]
    public async Task UpdateDraftAsync(string tenantId, string taskId, string updatedDraft)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "UpdateDraft activity started: TaskId={TaskId}, TenantId={TenantId}",
            taskId,
            tenantId);

        try
        {
            var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<TaskService>();
            var agentName = XiansContext.CurrentAgent?.Name 
                ?? throw new InvalidOperationException("Agent name not available in activity context");
            var taskService = new TaskService(_client, agentName, tenantId, logger);
            
            await taskService.UpdateDraftAsync(taskId, updatedDraft);

            ActivityExecutionContext.Current.Logger.LogDebug(
                "Draft updated successfully: TaskId={TaskId}",
                taskId);
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error updating draft: TaskId={TaskId}",
                taskId);
            throw new ActivityExecutionException(
                $"Failed to update draft for TaskId='{taskId}'",
                activityName: nameof(UpdateDraftAsync),
                tenantId: tenantId,
                innerException: ex);
        }
    }

    /// <summary>
    /// Performs an action on a task with an optional comment.
    /// </summary>
    [Activity]
    public async Task PerformActionAsync(string tenantId, string taskId, string action, string? comment = null)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "PerformAction activity started: TaskId={TaskId}, Action={Action}, TenantId={TenantId}",
            taskId,
            action,
            tenantId);

        try
        {
            var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<TaskService>();
            var agentName = XiansContext.CurrentAgent?.Name 
                ?? throw new InvalidOperationException("Agent name not available in activity context");
            var taskService = new TaskService(_client, agentName, tenantId, logger);
            
            await taskService.PerformActionAsync(taskId, action, comment);

            ActivityExecutionContext.Current.Logger.LogDebug(
                "Action performed successfully: TaskId={TaskId}, Action={Action}",
                taskId,
                action);
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error performing action: TaskId={TaskId}, Action={Action}",
                taskId,
                action);
            throw new ActivityExecutionException(
                $"Failed to perform action '{action}' for TaskId='{taskId}'",
                activityName: nameof(PerformActionAsync),
                tenantId: tenantId,
                innerException: ex);
        }
    }
}
