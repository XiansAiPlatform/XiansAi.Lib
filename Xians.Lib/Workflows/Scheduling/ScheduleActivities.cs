using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Scheduling;
using Xians.Lib.Common.Infrastructure;

namespace Xians.Lib.Workflows.Scheduling;

/// <summary>
/// System activity for managing schedules using the Xians Schedule SDK.
/// Automatically registered with all workflows, allowing them to create and manage schedules programmatically.
/// </summary>
public class ScheduleActivities
{
    private readonly ILogger<ScheduleActivities> _logger;

    public ScheduleActivities()
    {
        _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<ScheduleActivities>();
    }

    /// <summary>
    /// Gets the workflow for the current activity execution context.
    /// Uses XiansContext which maintains the central workflow registry.
    /// </summary>
    private XiansWorkflow GetCurrentWorkflow()
    {
        var workflowType = ActivityExecutionContext.Current.Info.WorkflowType;
        return XiansContext.CurrentWorkflow;
    }

    /// <summary>
    /// Creates a cron-based schedule if it doesn't already exist using the Xians Schedule SDK.
    /// </summary>
    /// <param name="scheduleId">Unique identifier for the schedule.</param>
    /// <param name="cronExpression">Cron expression for the schedule.</param>
    /// <param name="workflowInput">Input arguments for the workflow.</param>
    /// <param name="timezone">Optional timezone for the schedule.</param>
    /// <returns>True if schedule was created, false if it already existed.</returns>
    [Activity]
    public async Task<bool> CreateScheduleIfNotExists(
        string scheduleId,
        string cronExpression,
        object[] workflowInput,
        string? timezone = null)
    {
        var workflow = GetCurrentWorkflow();

        try
        {
            if (await workflow.Schedules!.ExistsAsync(scheduleId))
            {
                _logger.LogInformation("Schedule '{ScheduleId}' already exists, skipping creation", scheduleId);
                return false;
            }

            _logger.LogInformation("Schedule '{ScheduleId}' does not exist, creating it", scheduleId);

            await workflow.Schedules!
                .Create(scheduleId)
                .WithCronSchedule(cronExpression, timezone)
                .WithInput(workflowInput)
                .StartAsync();

            _logger.LogInformation(
                "✅ Successfully created schedule '{ScheduleId}' with cron '{CronExpression}'",
                scheduleId, cronExpression);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create schedule '{ScheduleId}'", scheduleId);
            throw;
        }
    }

    /// <summary>
    /// Creates an interval-based schedule if it doesn't already exist using the Xians Schedule SDK.
    /// </summary>
    [Activity]
    public async Task<bool> CreateIntervalScheduleIfNotExists(
        string scheduleId,
        TimeSpan interval,
        object[] workflowInput)
    {
        var workflow = GetCurrentWorkflow();

        try
        {
            if (await workflow.Schedules!.ExistsAsync(scheduleId))
            {
                _logger.LogInformation("Schedule '{ScheduleId}' already exists, skipping creation", scheduleId);
                return false;
            }

            _logger.LogInformation("Schedule '{ScheduleId}' does not exist, creating it", scheduleId);

            await workflow.Schedules!
                .Create(scheduleId)
                .WithIntervalSchedule(interval)
                .WithInput(workflowInput)
                .StartAsync();

            _logger.LogInformation(
                "✅ Successfully created interval schedule '{ScheduleId}' with interval '{Interval}'",
                scheduleId, interval);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create interval schedule '{ScheduleId}'", scheduleId);
            throw;
        }
    }

    /// <summary>
    /// Checks if a schedule exists using the Xians Schedule SDK.
    /// </summary>
    [Activity]
    public async Task<bool> ScheduleExists(string scheduleId)
    {
        var workflow = GetCurrentWorkflow();
        return await workflow.Schedules!.ExistsAsync(scheduleId);
    }

    /// <summary>
    /// Deletes a schedule using the Xians Schedule SDK.
    /// </summary>
    [Activity]
    public async Task<bool> DeleteSchedule(string scheduleId)
    {
        var workflow = GetCurrentWorkflow();

        try
        {
            await workflow.Schedules!.DeleteAsync(scheduleId);
            _logger.LogInformation("✅ Successfully deleted schedule '{ScheduleId}'", scheduleId);
            return true;
        }
        catch (Xians.Lib.Agents.Scheduling.Models.ScheduleNotFoundException)
        {
            _logger.LogWarning("⚠️ Schedule '{ScheduleId}' not found for deletion", scheduleId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete schedule '{ScheduleId}'", scheduleId);
            throw;
        }
    }

    /// <summary>
    /// Pauses a schedule using the Xians Schedule SDK.
    /// </summary>
    [Activity]
    public async Task PauseSchedule(string scheduleId, string? note = null)
    {
        var workflow = GetCurrentWorkflow();

        try
        {
            await workflow.Schedules!.PauseAsync(scheduleId, note);
            _logger.LogInformation("⏸️ Successfully paused schedule '{ScheduleId}'", scheduleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause schedule '{ScheduleId}'", scheduleId);
            throw;
        }
    }

    /// <summary>
    /// Resumes a schedule using the Xians Schedule SDK.
    /// </summary>
    [Activity]
    public async Task ResumeSchedule(string scheduleId, string? note = null)
    {
        var workflow = GetCurrentWorkflow();

        try
        {
            await workflow.Schedules!.UnpauseAsync(scheduleId, note);
            _logger.LogInformation("▶️ Successfully resumed schedule '{ScheduleId}'", scheduleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume schedule '{ScheduleId}'", scheduleId);
            throw;
        }
    }

    /// <summary>
    /// Triggers an immediate execution of a schedule using the Xians Schedule SDK.
    /// </summary>
    [Activity]
    public async Task TriggerSchedule(string scheduleId)
    {
        var workflow = GetCurrentWorkflow();

        try
        {
            await workflow.Schedules!.TriggerAsync(scheduleId);
            _logger.LogInformation("🚀 Successfully triggered schedule '{ScheduleId}'", scheduleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger schedule '{ScheduleId}'", scheduleId);
            throw;
        }
    }
}

