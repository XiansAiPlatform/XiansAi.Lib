using Microsoft.Extensions.Logging;
using Temporalio.Client.Schedules;
using Xians.Lib.Agents.Scheduling.Models;
using Xians.Lib.Common;
using Xians.Lib.Common.Infrastructure;

namespace Xians.Lib.Agents.Scheduling;

/// <summary>
/// Represents a Xians schedule that wraps Temporal's ScheduleHandle.
/// Provides convenient methods for managing schedule lifecycle.
/// </summary>
public class XiansSchedule
{
    private readonly ScheduleHandle _handle;
    private readonly ILogger<XiansSchedule> _logger;

    internal XiansSchedule(ScheduleHandle handle)
    {
        _handle = handle ?? throw new ArgumentNullException(nameof(handle));
        _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<XiansSchedule>();
    }

    /// <summary>
    /// Gets the schedule ID.
    /// </summary>
    public string Id => _handle.Id;

    /// <summary>
    /// Gets information about the schedule including next run times and recent actions.
    /// </summary>
    public async Task<ScheduleDescription> DescribeAsync()
    {
        try
        {
            return await _handle.DescribeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to describe schedule '{ScheduleId}'", Id);
            throw new InvalidOperationException($"Failed to describe schedule '{Id}'", ex);
        }
    }

    /// <summary>
    /// Pauses the schedule, preventing future workflow executions.
    /// </summary>
    /// <param name="note">Optional note explaining why the schedule is paused.</param>
    public async Task PauseAsync(string? note = null)
    {
        try
        {
            await _handle.PauseAsync(note);
            _logger.LogInformation("Schedule '{ScheduleId}' paused. Note: {Note}", Id, note ?? "None");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause schedule '{ScheduleId}'", Id);
            throw new InvalidOperationException($"Failed to pause schedule '{Id}'", ex);
        }
    }

    /// <summary>
    /// Unpauses the schedule, resuming workflow executions.
    /// </summary>
    /// <param name="note">Optional note explaining why the schedule is unpaused.</param>
    public async Task UnpauseAsync(string? note = null)
    {
        try
        {
            await _handle.UnpauseAsync(note);
            _logger.LogInformation("Schedule '{ScheduleId}' unpaused. Note: {Note}", Id, note ?? "None");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unpause schedule '{ScheduleId}'", Id);
            throw new InvalidOperationException($"Failed to unpause schedule '{Id}'", ex);
        }
    }

    /// <summary>
    /// Triggers an immediate execution of the scheduled workflow.
    /// </summary>
    public async Task TriggerAsync()
    {
        try
        {
            await _handle.TriggerAsync();
            _logger.LogInformation("Schedule '{ScheduleId}' triggered manually", Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger schedule '{ScheduleId}'", Id);
            throw new InvalidOperationException($"Failed to trigger schedule '{Id}'", ex);
        }
    }

    /// <summary>
    /// Updates the schedule configuration.
    /// </summary>
    /// <param name="updater">Function that takes current schedule input and returns updated schedule.</param>
    public async Task UpdateAsync(Func<ScheduleUpdateInput, ScheduleUpdate> updater)
    {
        try
        {
            await _handle.UpdateAsync(updater);
            _logger.LogInformation("Schedule '{ScheduleId}' updated successfully", Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update schedule '{ScheduleId}'", Id);
            throw new InvalidOperationException($"Failed to update schedule '{Id}'", ex);
        }
    }

    /// <summary>
    /// Deletes the schedule. Does not affect workflows already started by the schedule.
    /// </summary>
    public async Task DeleteAsync()
    {
        try
        {
            await _handle.DeleteAsync();
            _logger.LogInformation("Schedule '{ScheduleId}' deleted successfully", Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete schedule '{ScheduleId}'", Id);
            throw new InvalidOperationException($"Failed to delete schedule '{Id}'", ex);
        }
    }

    /// <summary>
    /// Backfills the schedule by executing actions for a specified time range.
    /// </summary>
    /// <param name="backfills">Collection of backfill specifications.</param>
    public async Task BackfillAsync(IReadOnlyCollection<ScheduleBackfill> backfills)
    {
        try
        {
            await _handle.BackfillAsync(backfills);
            _logger.LogInformation("Schedule '{ScheduleId}' backfilled for {Count} time ranges", Id, backfills.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to backfill schedule '{ScheduleId}'", Id);
            throw new InvalidOperationException($"Failed to backfill schedule '{Id}'", ex);
        }
    }

    /// <summary>
    /// Gets the underlying Temporal schedule handle for advanced scenarios.
    /// </summary>
    public ScheduleHandle GetHandle() => _handle;
}

