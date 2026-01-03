using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Client;
using Xians.Lib.Agents.Tasks;
using Xians.Lib.Agents.Tasks.Models;

namespace Xians.Lib.Workflows.Tasks;

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
            var taskService = new TaskService(_client, tenantId, logger);
            
            var taskInfo = await taskService.QueryTaskInfoAsync(taskId);

            ActivityExecutionContext.Current.Logger.LogInformation(
                "Task info queried successfully: TaskId={TaskId}",
                taskId);

            return taskInfo;
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error querying task info: TaskId={TaskId}",
                taskId);
            throw;
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
            var taskService = new TaskService(_client, tenantId, logger);
            
            await taskService.UpdateDraftAsync(taskId, updatedDraft);

            ActivityExecutionContext.Current.Logger.LogInformation(
                "Draft updated successfully: TaskId={TaskId}",
                taskId);
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error updating draft: TaskId={TaskId}",
                taskId);
            throw;
        }
    }

    /// <summary>
    /// Sends a signal to complete the task.
    /// </summary>
    [Activity]
    public async Task CompleteTaskAsync(string tenantId, string taskId)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "CompleteTask activity started: TaskId={TaskId}, TenantId={TenantId}",
            taskId,
            tenantId);

        try
        {
            var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<TaskService>();
            var taskService = new TaskService(_client, tenantId, logger);
            
            await taskService.CompleteTaskAsync(taskId);

            ActivityExecutionContext.Current.Logger.LogInformation(
                "Task completed successfully: TaskId={TaskId}",
                taskId);
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error completing task: TaskId={TaskId}",
                taskId);
            throw;
        }
    }

    /// <summary>
    /// Sends a signal to reject the task.
    /// </summary>
    [Activity]
    public async Task RejectTaskAsync(string tenantId, string taskId, string rejectionMessage)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "RejectTask activity started: TaskId={TaskId}, TenantId={TenantId}",
            taskId,
            tenantId);

        try
        {
            var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<TaskService>();
            var taskService = new TaskService(_client, tenantId, logger);
            
            await taskService.RejectTaskAsync(taskId, rejectionMessage);

            ActivityExecutionContext.Current.Logger.LogInformation(
                "Task rejected successfully: TaskId={TaskId}",
                taskId);
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error rejecting task: TaskId={TaskId}",
                taskId);
            throw;
        }
    }
}

