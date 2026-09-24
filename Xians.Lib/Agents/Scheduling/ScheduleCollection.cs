using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Xians.Lib.Agents.Scheduling.Models;
using Xians.Lib.Temporal;
using Xians.Lib.Agents.Core;

namespace Xians.Lib.Agents.Scheduling;

/// <summary>
/// Manages the collection of schedules for a workflow.
/// Provides methods to create, retrieve, list, and delete schedules.
/// Safe to call from Temporal workflows (Temporal RPCs are stubbed to <c>ScheduleActivities</c>)
/// and from activities (direct Temporal client).
/// </summary>
public class ScheduleCollection
{
    private readonly XiansAgent _agent;
    private readonly ITemporalClientService? _temporalService;
    private readonly ScheduleActivityExecutor _executor;
    private readonly ILogger<ScheduleCollection> _logger;

    internal ScheduleCollection(
        XiansAgent agent,
        ITemporalClientService? temporalService)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _temporalService = temporalService;
        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<ScheduleCollection>();
        var executorLogger = Common.Infrastructure.LoggerFactory.CreateLogger<ScheduleActivityExecutor>();
        _executor = new ScheduleActivityExecutor(_agent, executorLogger);
    }

    /// <summary>
    /// Creates a new schedule with the specified ID.
    /// Returns a builder for configuring the schedule.
    /// </summary>
    /// <param name="scheduleName">Unique identifier for the schedule.</param>
    /// <returns>A ScheduleBuilder for configuring the schedule.</returns>
    public ScheduleBuilder Create<TWorkflow>(string scheduleName)
    {
        var workflowType = WorkflowHelper.GetWorkflowTypeFromClass<TWorkflow>();
        return Create(scheduleName, workflowType);
    }
    internal ScheduleBuilder Create(string scheduleName, string workflowType, string? idPostfix = null)
    {
        if (string.IsNullOrWhiteSpace(scheduleName) || string.IsNullOrWhiteSpace(workflowType))
            throw new ArgumentException("Schedule name and workflow type cannot be null or empty", nameof(scheduleName));

        if (_temporalService == null)
            throw new InvalidOperationException("Temporal service is not configured. Cannot create schedules.");

        return new ScheduleBuilder(scheduleName, _agent, workflowType, _temporalService, idPostfix);
    }

    /// <summary>
    /// Lists schedules owned by the current agent activation.
    /// Safe to call from a workflow, an activity, or regular code.
    /// </summary>
    public async Task<IReadOnlyList<XiansSchedule>> ListAsync()
    {
        if (Workflow.InWorkflow)
        {
            var identities = await _executor.ListAsync();
            return identities.Select(identity => new XiansSchedule(_agent, identity)).ToList();
        }

        return await new ScheduleClient(_agent).ListAsync();
    }

    /// <summary>
    /// Gets an existing schedule by ID.
    /// </summary>
    /// <param name="scheduleName">The schedule identifier.</param>
    /// <returns>A XiansSchedule instance for managing the schedule.</returns>
    public Task<XiansSchedule> GetAsync(string scheduleName)
    {
        return GetAsync(scheduleName, null);
    }

    /// <summary>
    /// Gets an existing schedule by ID.
    /// </summary>
    /// <param name="scheduleName">The schedule identifier.</param>
    /// <param name="idPostfix">The idPostfix to use for the schedule.</param>
    /// <returns>A XiansSchedule instance for managing the schedule.</returns>
    internal async Task<XiansSchedule> GetAsync(string scheduleName, string? idPostfix = null)
    {
        if (string.IsNullOrWhiteSpace(scheduleName))
            throw new ArgumentException("Schedule ID cannot be null or empty", nameof(scheduleName));

        if (Workflow.InWorkflow)
        {
            var identity = await _executor.GetIdentityAsync(scheduleName, idPostfix);
            return new XiansSchedule(_agent, identity);
        }

        return await new ScheduleClient(_agent).GetAsync(scheduleName, idPostfix);
    }

    /// <summary>
    /// Deletes a schedule by ID.
    /// </summary>
    /// <param name="scheduleName">The schedule identifier to delete.</param>
    public Task DeleteAsync(string scheduleName)
    {
        return DeleteAsync(scheduleName, null);
    }

    /// <summary>
    /// Deletes a schedule by ID.
    /// </summary>
    /// <param name="scheduleName">The schedule identifier to delete.</param>
    /// <param name="idPostfix">The idPostfix to use for the schedule.</param>
    internal async Task DeleteAsync(string scheduleName, string? idPostfix = null)
    {
        if (string.IsNullOrWhiteSpace(scheduleName))
            throw new ArgumentException("Schedule name cannot be null or empty", nameof(scheduleName));

        try
        {
            var deleted = await _executor.DeleteAsync(scheduleName, idPostfix);
            if (!deleted)
                throw new ScheduleNotFoundException(scheduleName);
        }
        catch (ScheduleNotFoundException)
        {
            _logger.LogWarning("Schedule '{scheduleName}' not found for deletion", scheduleName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete schedule '{scheduleName}'", scheduleName);
            throw;
        }
    }

    /// <summary>
    /// Checks if a schedule with the specified ID exists.
    /// </summary>
    /// <param name="scheduleName">The schedule identifier to check.</param>
    /// <returns>True if the schedule exists, false otherwise.</returns>
    public Task<bool> ExistsAsync(string scheduleName)
    {
        return ExistsAsync(scheduleName, null);
    }

    /// <summary>
    /// Checks if a schedule with the specified ID exists.
    /// </summary>
    /// <param name="scheduleName">The schedule identifier to check.</param>
    /// <param name="idPostfix">The idPostfix to use for the schedule.</param>
    /// <returns>True if the schedule exists, false otherwise.</returns>
    internal Task<bool> ExistsAsync(string scheduleName, string? idPostfix = null)
    {
        if (string.IsNullOrWhiteSpace(scheduleName))
            throw new ArgumentException("Schedule name cannot be null or empty", nameof(scheduleName));

        return _executor.ExistsAsync(scheduleName, idPostfix);
    }

    public Task PauseAsync(string scheduleName, string? note = null)
    {
        return PauseAsync(scheduleName, null, note);
    }

    /// <summary>
    /// Pauses a schedule by ID.
    /// </summary>
    /// <param name="scheduleName">The schedule identifier to pause.</param>
    /// <param name="idPostfix">The idPostfix to use for the schedule.</param>
    /// <param name="note">Optional note explaining why the schedule is paused.</param>
    internal Task PauseAsync(string scheduleName, string? idPostfix = null, string? note = null)
    {
        return _executor.PauseAsync(scheduleName, idPostfix, note);
    }

    public Task UnpauseAsync(string scheduleName, string? note = null)
    {
        return UnpauseAsync(scheduleName, null, note);
    }

    /// <summary>
    /// Unpauses a schedule by ID.
    /// </summary>
    /// <param name="scheduleName">The schedule identifier to unpause.</param>
    /// <param name="idPostfix">The idPostfix to use for the schedule.</param>
    /// <param name="note">Optional note explaining why the schedule is unpaused.</param>
    internal Task UnpauseAsync(string scheduleName, string? idPostfix = null, string? note = null)
    {
        return _executor.UnpauseAsync(scheduleName, idPostfix, note);
    }

    public Task TriggerAsync(string scheduleName)
    {
        return TriggerAsync(scheduleName, null);
    }

    /// <summary>
    /// Triggers an immediate execution of a schedule by ID.
    /// </summary>
    /// <param name="scheduleName">The schedule identifier to trigger.</param>
    /// <param name="idPostfix">The idPostfix to use for the schedule.</param>
    internal Task TriggerAsync(string scheduleName, string? idPostfix = null)
    {
        return _executor.TriggerAsync(scheduleName, idPostfix);
    }
}
