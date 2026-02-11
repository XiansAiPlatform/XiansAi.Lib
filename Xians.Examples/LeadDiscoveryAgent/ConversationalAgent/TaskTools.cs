using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Agents.Tasks;

namespace Xians.Agent.Sample.SupervisorAgent;

/// <summary>
/// Tools for interacting with HITL (Human-in-the-Loop) tasks.
/// </summary>
internal class TaskTools
{
    private readonly ILogger _logger;
    private readonly UserMessageContext _context;

    public TaskTools(UserMessageContext context)
    {
        _context = context;
        _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.Instance.CreateLogger<TaskTools>();
    }

    /// <summary>
    /// Gets the current task information including status, draft work, and metadata.
    /// </summary>
    [Description("Get information about the current task including its status, draft work, and metadata.")]
    public async Task<string> GetTaskInfo()
    {
        _logger.LogInformation("GetTaskInfo tool invoked");

        try
        {
            var taskWorkflowId = await _context.GetLastTaskIdAsync();
            if (string.IsNullOrWhiteSpace(taskWorkflowId))
            {
                return "No task workflow ID found in context.";
            }

            _logger.LogDebug("Retrieved task workflow ID: {WorkflowId}", taskWorkflowId);
            
            var task = await HitlTask.FromWorkflowIdAsync(taskWorkflowId);
            var info = await task.GetInfoAsync();

            var status = info.IsCompleted 
                ? $"Completed ({info.PerformedAction})" 
                : "Pending";

            var result = $"Task: {info.Title}\n" +
                        $"Description: {info.Description}\n" +
                        $"Status: {status}\n" +
                        $"Available Actions: {string.Join(", ", info.AvailableActions ?? [])}\n" +
                        $"Current Draft: {info.InitialWork ?? "No draft yet"}\n";

            if (info.IsCompleted && !string.IsNullOrEmpty(info.Comment))
            {
                result += $"Comment: {info.Comment}\n";
            }

            _logger.LogInformation("GetTaskInfo completed successfully for TaskId={TaskId}", task.TaskId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTaskInfo tool failed");
            return $"Error getting task info: {ex.Message}";
        }
    }

    /// <summary>
    /// Updates the draft work for the current task.
    /// </summary>
    [Description("Update the draft work for the current task.")]
    public async Task<string> UpdateTaskDraft([Description("The updated draft content")] string updatedDraft)
    {
        _logger.LogInformation("UpdateTaskDraft tool invoked");

        try
        {
            var taskWorkflowId = await _context.GetLastTaskIdAsync();
            if (string.IsNullOrWhiteSpace(taskWorkflowId))
            {
                return "No task workflow ID found in context.";
            }

            var task = await HitlTask.FromWorkflowIdAsync(taskWorkflowId);
            await task.UpdateDraftAsync(updatedDraft);

            _logger.LogInformation("UpdateTaskDraft completed successfully for TaskId={TaskId}", task.TaskId);
            return $"Task draft updated successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateTaskDraft tool failed");
            return $"Error updating task draft: {ex.Message}";
        }
    }

    /// <summary>
    /// Performs an action on the current task.
    /// </summary>
    [Description("Perform an action on the current task (e.g., approve, reject, publish).")]
    public async Task<string> PerformTaskAction(
        [Description("The action to perform")] string action,
        [Description("Optional comment for the action")] string? comment = null)
    {
        _logger.LogInformation("PerformTaskAction tool invoked with Action={Action}", action);

        try
        {
            var taskWorkflowId = await _context.GetLastTaskIdAsync();
            if (string.IsNullOrWhiteSpace(taskWorkflowId))
            {
                return "No task workflow ID found in context.";
            }

            var task = await HitlTask.FromWorkflowIdAsync(taskWorkflowId);
            await task.PerformActionAsync(action, comment);

            _logger.LogInformation("PerformTaskAction completed successfully for TaskId={TaskId}, Action={Action}", task.TaskId, action);
            return $"Task action '{action}' performed successfully." + (comment != null ? $" Comment: {comment}" : "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PerformTaskAction tool failed");
            return $"Error performing task action: {ex.Message}";
        }
    }

    /// <summary>
    /// Approves the current task.
    /// </summary>
    [Description("Approve and complete the current task.")]
    public async Task<string> ApproveTask([Description("Optional comment")] string? comment = null)
    {
        return await PerformTaskAction("approve", comment);
    }

    /// <summary>
    /// Rejects the current task.
    /// </summary>
    [Description("Reject the current task with an optional reason.")]
    public async Task<string> RejectTask([Description("Optional reason for rejecting")] string? reason = null)
    {
        return await PerformTaskAction("reject", reason);
    }

    /// <summary>
    /// Gets the current draft work content.
    /// </summary>
    [Description("Get the current draft work content for the task.")]
    public async Task<string> GetTaskDraft()
    {
        _logger.LogInformation("GetTaskDraft tool invoked");

        try
        {
            var taskWorkflowId = await _context.GetLastTaskIdAsync();
            if (string.IsNullOrWhiteSpace(taskWorkflowId))
            {
                return "No task workflow ID found in context.";
            }

            var task = await HitlTask.FromWorkflowIdAsync(taskWorkflowId);
            var draft = await task.GetInitialWorkAsync();

            _logger.LogInformation("GetTaskDraft completed successfully for TaskId={TaskId}", task.TaskId);
            return draft ?? "No draft work available yet.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTaskDraft tool failed");
            return $"Error getting task draft: {ex.Message}";
        }
    }
}
