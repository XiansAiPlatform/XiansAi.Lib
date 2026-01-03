using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Scheduling;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Workflows.Scheduling.Models;

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
        return XiansContext.CurrentWorkflow;
    }

    /// <summary>
    /// Creates a cron-based schedule if it doesn't already exist using the Xians Schedule SDK.
    /// </summary>
    /// <param name="request">The create cron schedule request containing schedule ID, cron expression, workflow input, and optional timezone.</param>
    /// <returns>True if schedule was created, false if it already existed.</returns>
    [Activity]
    public async Task<bool> CreateScheduleIfNotExists(CreateCronScheduleRequest request)
    {
        var workflow = GetCurrentWorkflow();

        try
        {
            if (await workflow.Schedules!.ExistsAsync(request.ScheduleId))
            {
                _logger.LogInformation("Schedule '{ScheduleId}' already exists, skipping creation", request.ScheduleId);
                return false;
            }

            _logger.LogInformation("Schedule '{ScheduleId}' does not exist, creating it", request.ScheduleId);

            await workflow.Schedules!
                .Create(request.ScheduleId)
                .WithCronSchedule(request.CronExpression, request.Timezone)
                .WithInput(request.WorkflowInput)
                .StartAsync();

            _logger.LogInformation(
                "✅ Successfully created schedule '{ScheduleId}' with cron '{CronExpression}'",
                request.ScheduleId, request.CronExpression);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create schedule '{ScheduleId}'", request.ScheduleId);
            throw;
        }
    }

    /// <summary>
    /// Creates an interval-based schedule if it doesn't already exist using the Xians Schedule SDK.
    /// </summary>
    /// <param name="request">The create interval schedule request containing schedule ID, interval, and workflow input.</param>
    /// <returns>True if schedule was created, false if it already existed.</returns>
    [Activity]
    public async Task<bool> CreateIntervalScheduleIfNotExists(CreateIntervalScheduleRequest request)
    {
        var workflow = GetCurrentWorkflow();

        try
        {
            if (await workflow.Schedules!.ExistsAsync(request.ScheduleId))
            {
                _logger.LogInformation("Schedule '{ScheduleId}' already exists, skipping creation", request.ScheduleId);
                return false;
            }

            _logger.LogInformation("Schedule '{ScheduleId}' does not exist, creating it", request.ScheduleId);

            await workflow.Schedules!
                .Create(request.ScheduleId)
                .WithIntervalSchedule(request.Interval)
                .WithInput(request.WorkflowInput)
                .StartAsync();

            _logger.LogInformation(
                "✅ Successfully created interval schedule '{ScheduleId}' with interval '{Interval}'",
                request.ScheduleId, request.Interval);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create interval schedule '{ScheduleId}'", request.ScheduleId);
            throw;
        }
    }

    /// <summary>
    /// Checks if a schedule exists using the Xians Schedule SDK.
    /// </summary>
    /// <param name="request">The schedule exists request containing the schedule ID.</param>
    /// <returns>True if the schedule exists, false otherwise.</returns>
    [Activity]
    public async Task<bool> ScheduleExists(ScheduleExistsRequest request)
    {
        var workflow = GetCurrentWorkflow();
        return await workflow.Schedules!.ExistsAsync(request.ScheduleId);
    }

    /// <summary>
    /// Deletes a schedule using the Xians Schedule SDK.
    /// </summary>
    /// <param name="request">The delete schedule request containing the schedule ID.</param>
    /// <returns>True if deleted successfully, false if not found.</returns>
    [Activity]
    public async Task<bool> DeleteSchedule(DeleteScheduleRequest request)
    {
        var workflow = GetCurrentWorkflow();

        try
        {
            await workflow.Schedules!.DeleteAsync(request.ScheduleId);
            _logger.LogInformation("✅ Successfully deleted schedule '{ScheduleId}'", request.ScheduleId);
            return true;
        }
        catch (Xians.Lib.Agents.Scheduling.Models.ScheduleNotFoundException)
        {
            _logger.LogWarning("⚠️ Schedule '{ScheduleId}' not found for deletion", request.ScheduleId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete schedule '{ScheduleId}'", request.ScheduleId);
            throw;
        }
    }

    /// <summary>
    /// Pauses a schedule using the Xians Schedule SDK.
    /// </summary>
    /// <param name="request">The pause schedule request containing the schedule ID and optional note.</param>
    [Activity]
    public async Task PauseSchedule(PauseScheduleRequest request)
    {
        var workflow = GetCurrentWorkflow();

        try
        {
            await workflow.Schedules!.PauseAsync(request.ScheduleId, request.Note);
            _logger.LogInformation("⏸️ Successfully paused schedule '{ScheduleId}'", request.ScheduleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause schedule '{ScheduleId}'", request.ScheduleId);
            throw;
        }
    }

    /// <summary>
    /// Resumes a schedule using the Xians Schedule SDK.
    /// </summary>
    /// <param name="request">The resume schedule request containing the schedule ID and optional note.</param>
    [Activity]
    public async Task ResumeSchedule(ResumeScheduleRequest request)
    {
        var workflow = GetCurrentWorkflow();

        try
        {
            await workflow.Schedules!.UnpauseAsync(request.ScheduleId, request.Note);
            _logger.LogInformation("▶️ Successfully resumed schedule '{ScheduleId}'", request.ScheduleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume schedule '{ScheduleId}'", request.ScheduleId);
            throw;
        }
    }

    /// <summary>
    /// Triggers an immediate execution of a schedule using the Xians Schedule SDK.
    /// </summary>
    /// <param name="request">The trigger schedule request containing the schedule ID.</param>
    [Activity]
    public async Task TriggerSchedule(TriggerScheduleRequest request)
    {
        var workflow = GetCurrentWorkflow();

        try
        {
            await workflow.Schedules!.TriggerAsync(request.ScheduleId);
            _logger.LogInformation("🚀 Successfully triggered schedule '{ScheduleId}'", request.ScheduleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger schedule '{ScheduleId}'", request.ScheduleId);
            throw;
        }
    }
}

