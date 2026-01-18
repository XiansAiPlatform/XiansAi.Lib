using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Client.Schedules;
using Temporalio.Workflows;
using Xians.Lib.Agents.Scheduling.Models;
using Xians.Lib.Common;
using Xians.Lib.Temporal;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common.Infrastructure;

namespace Xians.Lib.Agents.Scheduling;

/// <summary>
/// Manages the collection of schedules for a workflow.
/// Provides methods to create, retrieve, list, and delete schedules.
/// </summary>
public class ScheduleCollection
{
    private readonly XiansAgent _agent;
    private readonly string _workflowType;
    private readonly string _idPostfix;
    private readonly ITemporalClientService _temporalService;
    private readonly ILogger<ScheduleCollection> _logger;

    internal ScheduleCollection(
        XiansAgent agent,
        string workflowType,
        string idPostfix,
        ITemporalClientService temporalService)
    {
        _idPostfix = idPostfix ?? throw new ArgumentNullException(nameof(idPostfix));
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _workflowType = workflowType ?? throw new ArgumentNullException(nameof(workflowType));
        _temporalService = temporalService ?? throw new ArgumentNullException(nameof(temporalService));
        _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<ScheduleCollection>();
    }

    /// <summary>
    /// Creates a new schedule with the specified ID.
    /// Returns a builder for configuring the schedule.
    /// </summary>
    /// <param name="scheduleId">Unique identifier for the schedule.</param>
    /// <returns>A ScheduleBuilder for configuring the schedule.</returns>
    public ScheduleBuilder Create(string scheduleId)
    {
        if (string.IsNullOrWhiteSpace(scheduleId))
            throw new ArgumentException("Schedule ID cannot be null or empty", nameof(scheduleId));

        return new ScheduleBuilder(scheduleId, _workflowType, _agent, _temporalService);
    }

    /// <summary>
    /// Gets an existing schedule by ID.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier.</param>
    /// <returns>A XiansSchedule instance for managing the schedule.</returns>
    public async Task<XiansSchedule> GetAsync(string scheduleId)
    {
        if (string.IsNullOrWhiteSpace(scheduleId))
            throw new ArgumentException("Schedule ID cannot be null or empty", nameof(scheduleId));

        try
        {
            var client = await _temporalService.GetClientAsync();
            
            // Determine tenant ID - ALL schedules must have a tenant
            string tenantId = _agent.SystemScoped 
                ? XiansContext.TenantId  // Will throw if not in workflow/activity context
                : _agent.Options?.CertificateTenantId 
                    ?? throw new InvalidOperationException(
                        "Tenant-scoped agent must have a valid tenant ID. XiansOptions not properly configured.");
            
            // Full schedule ID pattern: tenantId:agentName:idPostfix:scheduleId
            var fullScheduleId = $"{tenantId}:{_agent.Name}:{_idPostfix}:{scheduleId}";

            var handle = client.GetScheduleHandle(fullScheduleId);
            
            // Verify the schedule exists by attempting to describe it
            await handle.DescribeAsync();
            
            return new XiansSchedule(handle);
        }
        catch (Temporalio.Exceptions.RpcException ex) when (
            ex.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
        {
            _logger.LogWarning("Schedule '{ScheduleId}' not found", scheduleId);
            throw new ScheduleNotFoundException(scheduleId, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get schedule '{ScheduleId}'", scheduleId);
            throw;
        }
    }

    /// <summary>
    /// Gets an existing schedule by ID (convenience method).
    /// </summary>
    /// <param name="scheduleId">The schedule identifier.</param>
    /// <returns>A XiansSchedule instance for managing the schedule.</returns>
    public XiansSchedule Get(string scheduleId)
    {
        return GetAsync(scheduleId).GetAwaiter().GetResult();
    }


    /// <summary>
    /// Deletes a schedule by ID.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier to delete.</param>
    public async Task DeleteAsync(string scheduleId)
    {
        if (string.IsNullOrWhiteSpace(scheduleId))
            throw new ArgumentException("Schedule ID cannot be null or empty", nameof(scheduleId));

        try
        {
            var schedule = await GetAsync(scheduleId);
            await schedule.DeleteAsync();
        }
        catch (ScheduleNotFoundException)
        {
            _logger.LogWarning("Schedule '{ScheduleId}' not found for deletion", scheduleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete schedule '{ScheduleId}'", scheduleId);
            throw;
        }
    }

    /// <summary>
    /// Checks if a schedule with the specified ID exists.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier to check.</param>
    /// <returns>True if the schedule exists, false otherwise.</returns>
    public async Task<bool> ExistsAsync(string scheduleId)
    {
        if (string.IsNullOrWhiteSpace(scheduleId))
            throw new ArgumentException("Schedule ID cannot be null or empty", nameof(scheduleId));

        try
        {
            await GetAsync(scheduleId);
            return true;
        }
        catch (ScheduleNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Pauses a schedule by ID.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier to pause.</param>
    /// <param name="note">Optional note explaining why the schedule is paused.</param>
    public async Task PauseAsync(string scheduleId, string? note = null)
    {
        var schedule = await GetAsync(scheduleId);
        await schedule.PauseAsync(note);
    }

    /// <summary>
    /// Unpauses a schedule by ID.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier to unpause.</param>
    /// <param name="note">Optional note explaining why the schedule is unpaused.</param>
    public async Task UnpauseAsync(string scheduleId, string? note = null)
    {
        var schedule = await GetAsync(scheduleId);
        await schedule.UnpauseAsync(note);
    }

    /// <summary>
    /// Triggers an immediate execution of a schedule by ID.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier to trigger.</param>
    public async Task TriggerAsync(string scheduleId)
    {
        var schedule = await GetAsync(scheduleId);
        await schedule.TriggerAsync();
    }

}

