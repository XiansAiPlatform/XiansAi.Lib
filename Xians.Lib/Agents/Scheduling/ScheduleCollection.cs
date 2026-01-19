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
    private readonly ITemporalClientService _temporalService;
    private readonly ILogger<ScheduleCollection> _logger;

    internal ScheduleCollection(
        XiansAgent agent,
        string workflowType,
        ITemporalClientService temporalService)
    {
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
    /// <param name="idPostfix">The idPostfix to use for the schedule. Must be stable across workflow executions.</param>
    /// <returns>A ScheduleBuilder for configuring the schedule.</returns>
    public ScheduleBuilder Create(string scheduleId, string? idPostfix = null)
    {
        if (string.IsNullOrWhiteSpace(scheduleId))
            throw new ArgumentException("Schedule ID cannot be null or empty", nameof(scheduleId));
        
        if (idPostfix is null)
            idPostfix = WorkflowContextHelper.GetIdPostfix();   

        return new ScheduleBuilder(scheduleId, _workflowType, _agent, idPostfix, _temporalService);
    }

    /// <summary>
    /// Gets an existing schedule by ID.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier.</param>
    /// <param name="idPostfix">Optional idPostfix to use. If not provided, uses current workflow context.</param>
    /// <returns>A XiansSchedule instance for managing the schedule.</returns>
    public async Task<XiansSchedule> GetAsync(string scheduleId, string? idPostfix = null)
    {
        if (string.IsNullOrWhiteSpace(scheduleId))
            throw new ArgumentException("Schedule ID cannot be null or empty", nameof(scheduleId));
        
        if (idPostfix is null)
            idPostfix = WorkflowContextHelper.GetIdPostfix();

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
            var fullScheduleId = ScheduleIdHelper.BuildFullScheduleId(tenantId, _agent.Name, idPostfix, scheduleId);
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
    /// <param name="idPostfix">Optional idPostfix to use. If not provided, uses current workflow context.</param>
    /// <returns>A XiansSchedule instance for managing the schedule.</returns>
    public XiansSchedule Get(string scheduleId, string? idPostfix = null)
    {
        return GetAsync(scheduleId, idPostfix).GetAwaiter().GetResult();
    }


    /// <summary>
    /// Deletes a schedule by ID.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier to delete.</param>
    /// <param name="idPostfix">Optional idPostfix to use. If not provided, uses current workflow context.</param>
    public async Task DeleteAsync(string scheduleId, string? idPostfix = null)
    {
        if (string.IsNullOrWhiteSpace(scheduleId))
            throw new ArgumentException("Schedule ID cannot be null or empty", nameof(scheduleId));
        
        if (idPostfix is null)
            idPostfix = WorkflowContextHelper.GetIdPostfix();

        try
        {
            var schedule = await GetAsync(scheduleId, idPostfix);
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
    /// <param name="idPostfix">Optional idPostfix to use. If not provided, uses current workflow context.</param>
    /// <returns>True if the schedule exists, false otherwise.</returns>
    public async Task<bool> ExistsAsync(string scheduleId, string? idPostfix = null)
    {
        if (string.IsNullOrWhiteSpace(scheduleId))
            throw new ArgumentException("Schedule ID cannot be null or empty", nameof(scheduleId));
        
        if (idPostfix is null)
            idPostfix = WorkflowContextHelper.GetIdPostfix();

        try
        {
            await GetAsync(scheduleId, idPostfix);
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
    /// <param name="idPostfix">Optional idPostfix to use. If not provided, uses current workflow context.</param>
    /// <param name="note">Optional note explaining why the schedule is paused.</param>
    public async Task PauseAsync(string scheduleId, string? idPostfix = null, string? note = null)
    {
        if (idPostfix is null)
            idPostfix = WorkflowContextHelper.GetIdPostfix();
            
        var schedule = await GetAsync(scheduleId, idPostfix);
        await schedule.PauseAsync(note);
    }

    /// <summary>
    /// Unpauses a schedule by ID.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier to unpause.</param>
    /// <param name="idPostfix">Optional idPostfix to use. If not provided, uses current workflow context.</param>
    /// <param name="note">Optional note explaining why the schedule is unpaused.</param>
    public async Task UnpauseAsync(string scheduleId, string? idPostfix = null, string? note = null)
    {
        if (idPostfix is null)
            idPostfix = WorkflowContextHelper.GetIdPostfix();
            
        var schedule = await GetAsync(scheduleId, idPostfix);
        await schedule.UnpauseAsync(note);
    }

    /// <summary>
    /// Triggers an immediate execution of a schedule by ID.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier to trigger.</param>
    /// <param name="idPostfix">Optional idPostfix to use. If not provided, uses current workflow context.</param>
    public async Task TriggerAsync(string scheduleId, string? idPostfix = null)
    {
        if (idPostfix is null)
            idPostfix = WorkflowContextHelper.GetIdPostfix();
            
        var schedule = await GetAsync(scheduleId, idPostfix);
        await schedule.TriggerAsync();
    }

}

