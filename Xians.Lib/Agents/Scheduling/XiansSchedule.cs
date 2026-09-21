using Microsoft.Extensions.Logging;
using Temporalio.Client.Schedules;
using Temporalio.Exceptions;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Scheduling.Models;
using Xians.Lib.Temporal.Workflows.Scheduling.Models;

namespace Xians.Lib.Agents.Scheduling;

/// <summary>
/// Represents a Xians schedule that wraps Temporal's ScheduleHandle.
/// Provides convenient methods for managing schedule lifecycle.
/// Safe to use from Temporal workflows when obtained via <see cref="ScheduleCollection.GetAsync(string)"/>
/// or schedule creation (handle RPCs are stubbed to <c>ScheduleActivities</c>).
/// </summary>
public class XiansSchedule
{
    private readonly ScheduleHandle? _handle;
    private readonly XiansAgent? _agent;
    private readonly string? _scheduleName;
    private readonly string? _idPostfix;
    private readonly string? _fullScheduleId;
    private readonly ILogger<XiansSchedule> _logger;

    internal XiansSchedule(ScheduleHandle handle)
        : this(handle, owner: null, scheduleName: null, idPostfix: null)
    {
    }

    internal XiansSchedule(ScheduleHandle handle, XiansAgent? owner, string? scheduleName, string? idPostfix)
    {
        _handle = handle ?? throw new ArgumentNullException(nameof(handle));
        _agent = owner;
        _scheduleName = scheduleName;
        _idPostfix = idPostfix;
        _fullScheduleId = handle.Id;
        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<XiansSchedule>();
    }

    internal XiansSchedule(XiansAgent agent, ScheduleIdentity identity)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _scheduleName = identity.ScheduleName;
        _idPostfix = identity.IdPostfix;
        _fullScheduleId = identity.FullScheduleId;
        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<XiansSchedule>();
    }

    /// <summary>
    /// Gets the schedule ID.
    /// </summary>
    public string Id => _handle?.Id ?? _fullScheduleId
        ?? throw new InvalidOperationException("Schedule id is not available.");

    internal string? ScheduleName => _scheduleName;
    internal string? IdPostfix => _idPostfix;

    /// <summary>
    /// Gets information about the schedule including next run times and recent actions.
    /// Cannot be called from a workflow: Temporal's <see cref="ScheduleDescription"/> has no public
    /// constructor and so cannot be rebuilt from an activity result. Use
    /// <see cref="GetSnapshotAsync"/> instead, which returns the same information in a serializable form.
    /// </summary>
    public Task<ScheduleDescription> DescribeAsync()
    {
        if (Workflow.InWorkflow)
        {
            throw new ApplicationFailureException(
                "XiansSchedule.DescribeAsync cannot run inside a workflow because Temporal's " +
                "ScheduleDescription cannot be deserialized from an activity result. " +
                "Call GetSnapshotAsync from workflow code, or DescribeAsync from an activity.",
                nonRetryable: true);
        }

        return DescribeViaHandleAsync();
    }

    /// <summary>
    /// Gets a serializable snapshot of the schedule (paused state, action counts, next run times).
    /// Safe to call from a workflow, an activity, or regular code.
    /// </summary>
    public Task<ScheduleSnapshot> GetSnapshotAsync()
    {
        if (Workflow.InWorkflow)
            return Executor().DescribeSnapshotAsync(RequireScheduleName(), _idPostfix);

        return GetSnapshotViaHandleAsync();
    }

    private async Task<ScheduleSnapshot> GetSnapshotViaHandleAsync()
    {
        var description = await DescribeViaHandleAsync();
        return ScheduleClient.ToSnapshot(Id, description);
    }

    /// <summary>
    /// Pauses the schedule, preventing future workflow executions.
    /// </summary>
    /// <param name="note">Optional note explaining why the schedule is paused.</param>
    public Task PauseAsync(string? note = null)
    {
        if (Workflow.InWorkflow)
            return Executor().PauseAsync(RequireScheduleName(), _idPostfix, note);
        return PauseViaHandleAsync(note);
    }

    /// <summary>
    /// Unpauses the schedule, resuming workflow executions.
    /// </summary>
    /// <param name="note">Optional note explaining why the schedule is unpaused.</param>
    public Task UnpauseAsync(string? note = null)
    {
        if (Workflow.InWorkflow)
            return Executor().UnpauseAsync(RequireScheduleName(), _idPostfix, note);
        return UnpauseViaHandleAsync(note);
    }

    /// <summary>
    /// Triggers an immediate execution of the scheduled workflow.
    /// </summary>
    public Task TriggerAsync()
    {
        if (Workflow.InWorkflow)
            return Executor().TriggerAsync(RequireScheduleName(), _idPostfix);
        return TriggerViaHandleAsync();
    }

    /// <summary>
    /// Updates the schedule configuration.
    /// Cannot be called from a workflow: the updater callback is not serializable across an activity
    /// boundary. Call this from an activity or from non-workflow code.
    /// </summary>
    /// <param name="updater">Function that takes current schedule input and returns updated schedule.</param>
    public async Task UpdateAsync(Func<ScheduleUpdateInput, ScheduleUpdate> updater)
    {
        if (Workflow.InWorkflow)
        {
            // A FailureException fails the workflow run with this message. A plain
            // InvalidOperationException would instead fail the workflow task and retry forever,
            // because the worker registers only a narrow WorkflowFailureExceptionTypes list.
            throw new ApplicationFailureException(
                "XiansSchedule.UpdateAsync cannot run inside a workflow because the updater callback cannot be serialized. " +
                "Call UpdateAsync from an activity or from non-workflow code.",
                nonRetryable: true);
        }

        try
        {
            await RequireHandle().UpdateAsync(updater);
            _logger.LogDebug("Schedule '{ScheduleId}' updated successfully", Id);
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
        if (Workflow.InWorkflow)
        {
            var deleted = await Executor().DeleteAsync(RequireScheduleName(), _idPostfix);
            if (!deleted)
                throw new ScheduleNotFoundException(RequireScheduleName());
            return;
        }

        await DeleteViaHandleAsync();
    }

    /// <summary>
    /// Backfills the schedule by executing actions for a specified time range.
    /// </summary>
    /// <param name="backfills">Collection of backfill specifications.</param>
    public Task BackfillAsync(IReadOnlyCollection<ScheduleBackfill> backfills)
    {
        if (Workflow.InWorkflow)
            return Executor().BackfillAsync(RequireScheduleName(), _idPostfix, backfills);
        return BackfillViaHandleAsync(backfills);
    }

    /// <summary>
    /// Gets the underlying Temporal schedule handle for advanced scenarios.
    /// Not available inside a workflow (there is no Temporal client in workflow code).
    /// </summary>
    public ScheduleHandle GetHandle() => RequireHandle();

    internal async Task<ScheduleDescription> DescribeViaHandleAsync()
    {
        try
        {
            return await RequireHandle().DescribeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to describe schedule '{ScheduleId}'", Id);
            throw new InvalidOperationException($"Failed to describe schedule '{Id}'", ex);
        }
    }

    internal async Task PauseViaHandleAsync(string? note)
    {
        try
        {
            await RequireHandle().PauseAsync(note);
            _logger.LogDebug("Schedule '{ScheduleId}' paused. Note: {Note}", Id, note ?? "None");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause schedule '{ScheduleId}'", Id);
            throw new InvalidOperationException($"Failed to pause schedule '{Id}'", ex);
        }
    }

    internal async Task UnpauseViaHandleAsync(string? note)
    {
        try
        {
            await RequireHandle().UnpauseAsync(note);
            _logger.LogDebug("Schedule '{ScheduleId}' unpaused. Note: {Note}", Id, note ?? "None");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unpause schedule '{ScheduleId}'", Id);
            throw new InvalidOperationException($"Failed to unpause schedule '{Id}'", ex);
        }
    }

    internal async Task TriggerViaHandleAsync()
    {
        try
        {
            await RequireHandle().TriggerAsync();
            _logger.LogDebug("Schedule '{ScheduleId}' triggered manually", Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger schedule '{ScheduleId}'", Id);
            throw new InvalidOperationException($"Failed to trigger schedule '{Id}'", ex);
        }
    }

    internal async Task DeleteViaHandleAsync()
    {
        try
        {
            await RequireHandle().DeleteAsync();
            _logger.LogDebug("Schedule '{ScheduleId}' deleted successfully", Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete schedule '{ScheduleId}'", Id);
            throw new InvalidOperationException($"Failed to delete schedule '{Id}'", ex);
        }
    }

    internal async Task BackfillViaHandleAsync(IReadOnlyCollection<ScheduleBackfill> backfills)
    {
        try
        {
            await RequireHandle().BackfillAsync(backfills);
            _logger.LogDebug("Schedule '{ScheduleId}' backfilled for {Count} time ranges", Id, backfills.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to backfill schedule '{ScheduleId}'", Id);
            throw new InvalidOperationException($"Failed to backfill schedule '{Id}'", ex);
        }
    }

    private ScheduleHandle RequireHandle()
        => _handle ?? throw new InvalidOperationException(
            "Schedule handle is not available in workflow context. Use DescribeAsync/PauseAsync/etc. on this instance, " +
            "or call GetHandle from an activity.");

    private string RequireScheduleName()
        => _scheduleName ?? throw new InvalidOperationException(
            "This schedule was not bound to a schedule name. Use Schedules.GetAsync(name) or create the schedule through the SDK.");

    private ScheduleActivityExecutor Executor()
    {
        if (_agent == null)
        {
            throw new InvalidOperationException(
                "This schedule is not bound to an agent and cannot run inside a workflow. " +
                "Obtain it via Schedules.GetAsync or schedule creation.");
        }

        var logger = Common.Infrastructure.LoggerFactory.CreateLogger<ScheduleActivityExecutor>();
        return new ScheduleActivityExecutor(_agent, logger);
    }
}
