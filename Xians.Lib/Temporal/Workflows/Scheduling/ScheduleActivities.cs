using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Common;
using Xians.Lib.Agents.Core;
using Xians.Lib.Temporal.Workflows.Scheduling.Models;

namespace Xians.Lib.Temporal.Workflows.Scheduling;

/// <summary>
/// System activity for managing schedules using the Xians Schedule SDK.
/// Automatically registered with all workflows, allowing them to create and manage schedules programmatically.
/// </summary>
public class ScheduleActivities
{
    private readonly ILogger<ScheduleActivities> _logger;

    public ScheduleActivities()
    {
        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<ScheduleActivities>();
    }

    /// <summary>
    /// Gets the workflow for the current activity execution context.
    /// Uses XiansContext which maintains the central workflow registry.
    /// </summary>
    private XiansAgent GetCurrentAgent()
    {
        return XiansContext.CurrentAgent;
    }

    /// <summary>
    /// Creates a cron-based schedule if it doesn't already exist using the Xians Schedule SDK.
    /// </summary>
    /// <param name="request">The create cron schedule request containing schedule ID, cron expression, workflow input, and optional timezone.</param>
    /// <returns>True if schedule was created, false if it already existed.</returns>
    [Activity]
    public async Task<bool> CreateScheduleIfNotExists(CreateCronScheduleRequest request)
    {
        var agent = GetCurrentAgent();

        try
        {
            if (await agent.Schedules!.ExistsAsync(request.ScheduleName))
            {
                _logger.LogDebug("Schedule '{ScheduleId}' already exists, skipping creation", request.ScheduleName);
                return false;
            }

            _logger.LogDebug("Schedule '{ScheduleId}' does not exist, creating it", request.ScheduleName);

            // Reconstruct search attributes from serializable format
            var builder = agent.Schedules
                .Create(request.ScheduleName, request.WorkflowType, request.IdPostfix)
                .WithCronSchedule(request.CronExpression, request.Timezone)
                .WithInput(request.WorkflowInput);

            // Add search attributes if provided
            if (request.SearchAttributes != null)
            {
                var searchAttrs = ReconstructSearchAttributes(request.SearchAttributes);
                builder = builder.WithTypedSearchAttributes(searchAttrs);
            }

            await builder.CreateIfNotExistsAsync();

            _logger.LogDebug(
                "✅ Successfully created schedule '{ScheduleId}' with cron '{CronExpression}'",
                request.ScheduleName, request.CronExpression);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create schedule '{ScheduleId}'", request.ScheduleName);
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
        var agent = GetCurrentAgent();

        try
        {
            if (await agent.Schedules.ExistsAsync(request.ScheduleName))
            {
                _logger.LogDebug("Schedule '{ScheduleName}' already exists, skipping creation", request.ScheduleName);
                return false;
            }

            _logger.LogDebug("Schedule '{ScheduleName}' does not exist, creating it", request.ScheduleName);

            // Reconstruct search attributes from serializable format
            var builder = agent.Schedules
                .Create(request.ScheduleName, request.WorkflowType)
                .WithIntervalSchedule(request.Interval)
                .WithInput(request.WorkflowInput);

            // Add search attributes if provided
            if (request.SearchAttributes != null)
            {
                var searchAttrs = ReconstructSearchAttributes(request.SearchAttributes);
                builder = builder.WithTypedSearchAttributes(searchAttrs);
            }

            await builder.CreateIfNotExistsAsync();

            _logger.LogDebug(
                "Successfully created interval schedule '{ScheduleName}' with interval '{Interval}'",
                request.ScheduleName, request.Interval);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create interval schedule '{ScheduleName}'", request.ScheduleName);
            throw;
        }
    }

    /// <summary>
    /// Checks if a schedule exists using the Xians Schedule SDK.
    /// </summary>
    /// <param name="request">The schedule exists request containing the schedule name.</param>
    /// <returns>True if the schedule exists, false otherwise.</returns>
    [Activity]
    public async Task<bool> ScheduleExists(ScheduleExistsRequest request)
    {
        var agent = GetCurrentAgent();
        return await agent.Schedules.ExistsAsync(request.ScheduleName, request.IdPostfix);
    }

    /// <summary>
    /// Deletes a schedule using the Xians Schedule SDK.
    /// </summary>
    /// <param name="request">The delete schedule request containing the schedule ID and idPostfix.</param>
    /// <returns>True if deleted successfully, false if not found.</returns>
    [Activity]
    public async Task<bool> DeleteSchedule(DeleteScheduleRequest request)
    {
        var agent = GetCurrentAgent();

        try
        {
            await agent.Schedules.DeleteAsync(request.ScheduleName, request.IdPostfix);
            _logger.LogDebug("Successfully deleted schedule '{ScheduleName}'", request.ScheduleName);
            return true;
        }
        catch (Xians.Lib.Agents.Scheduling.Models.ScheduleNotFoundException)
        {
            _logger.LogWarning("Schedule '{ScheduleName}' not found for deletion", request.ScheduleName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete schedule '{ScheduleName}'", request.ScheduleName);
            throw;
        }
    }

    /// <summary>
    /// Pauses a schedule using the Xians Schedule SDK.
    /// </summary>
    /// <param name="request">The pause schedule request containing the schedule ID, idPostfix, and optional note.</param>
    [Activity]
    public async Task PauseSchedule(PauseScheduleRequest request)
    {
        var agent = GetCurrentAgent();

        try
        {
            await agent.Schedules.PauseAsync(request.ScheduleName, request.IdPostfix, request.Note);
            _logger.LogDebug("Successfully paused schedule '{ScheduleName}'", request.ScheduleName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause schedule '{ScheduleName}'", request.ScheduleName);
            throw;
        }
    }

    /// <summary>
    /// Resumes a schedule using the Xians Schedule SDK.
    /// </summary>
    /// <param name="request">The resume schedule request containing the schedule ID, idPostfix, and optional note.</param>
    [Activity]
    public async Task ResumeSchedule(ResumeScheduleRequest request)
    {
        var agent = GetCurrentAgent();

        try
        {
            await agent.Schedules.UnpauseAsync(request.ScheduleName, request.IdPostfix, request.Note);
            _logger.LogDebug("Successfully resumed schedule '{ScheduleName}'", request.ScheduleName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume schedule '{ScheduleName}'", request.ScheduleName);
            throw;
        }
    }

    /// <summary>
    /// Triggers an immediate execution of a schedule using the Xians Schedule SDK.
    /// </summary>
    /// <param name="request">The trigger schedule request containing the schedule ID and idPostfix.</param>
    [Activity]
    public async Task TriggerSchedule(TriggerScheduleRequest request)
    {
        var agent = GetCurrentAgent();

        try
        {
            await agent.Schedules.TriggerAsync(request.ScheduleName, request.IdPostfix);
            _logger.LogDebug("Successfully triggered schedule '{ScheduleName}'", request.ScheduleName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger schedule '{ScheduleName}'", request.ScheduleName);
            throw;
        }
    }

    /// <summary>
    /// Reconstructs SearchAttributeCollection from serializable dictionary format.
    /// </summary>
    private SearchAttributeCollection? ReconstructSearchAttributes(Dictionary<string, object>? searchAttrs)
    {
        if (searchAttrs == null || !searchAttrs.Any())
            return null;

        var builder = new SearchAttributeCollection.Builder();
        foreach (var kvp in searchAttrs)
        {
            // Create search attribute key and add to collection
            // Note: We assume keyword type for simplicity. Could be enhanced to support other types.
            var key = SearchAttributeKey.CreateKeyword(kvp.Key);
            builder.Set(key, kvp.Value?.ToString() ?? string.Empty);
        }
        return builder.ToSearchAttributeCollection();
    }
}

