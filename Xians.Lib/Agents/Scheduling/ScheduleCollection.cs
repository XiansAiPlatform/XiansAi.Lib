using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Scheduling.Models;
using Xians.Lib.Temporal;
using Xians.Lib.Agents.Core;

namespace Xians.Lib.Agents.Scheduling;

/// <summary>
/// Manages the collection of schedules for a workflow.
/// Provides methods to create, retrieve, list, and delete schedules.
/// </summary>
public class ScheduleCollection
{
    private readonly XiansAgent _agent;
    private readonly ITemporalClientService? _temporalService;
    private readonly ILogger<ScheduleCollection> _logger;

    internal ScheduleCollection(
        XiansAgent agent,
        ITemporalClientService? temporalService)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _temporalService = temporalService;
        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<ScheduleCollection>();
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
    /// Gets an existing schedule by ID.
    /// </summary>  
    /// <param name="scheduleName">The schedule identifier.</param>
    /// <returns>A XiansSchedule instance for managing the schedule.</returns>
    public async Task<XiansSchedule> GetAsync(string scheduleName)
    {
        return await GetAsync(scheduleName);
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
        
        try
        {
            if (_temporalService == null)
                throw new InvalidOperationException("Temporal service is not configured. Cannot get schedules.");
            
            var client = await _temporalService.GetClientAsync();
            string tenantId = XiansContext.TenantId;
            // When null, use empty string so we resolve the same shared schedule as Create() with no idPostfix.
            idPostfix ??= string.Empty;

            // Full schedule ID pattern: tenantId:agentName:idPostfix:scheduleId
            var fullScheduleId = ScheduleIdHelper.BuildFullScheduleId(tenantId, _agent.Name, idPostfix, scheduleName);
            var handle = client.GetScheduleHandle(fullScheduleId);
            
            // Verify the schedule exists by attempting to describe it
            await handle.DescribeAsync();
            
            return new XiansSchedule(handle);
        }
        catch (Temporalio.Exceptions.RpcException ex) when (
            ex.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
        {
            _logger.LogWarning("Schedule '{ScheduleName}' not found", scheduleName);
            throw new ScheduleNotFoundException(scheduleName, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get schedule '{ScheduleName}'", scheduleName);
            throw;
        }
    }

    /// <summary>
    /// Deletes a schedule by ID.
    /// </summary>
    /// <param name="scheduleName">The schedule identifier to delete.</param>
    /// <param name="idPostfix">The idPostfix to use for the schedule.</param>
    public async Task DeleteAsync(string scheduleName)
    {
        await DeleteAsync(scheduleName, null);
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
            var schedule = await GetAsync(scheduleName, idPostfix);
            await schedule.DeleteAsync();
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

    internal async Task<bool> ExistsAsync(string scheduleName)
    {
        return await ExistsAsync(scheduleName, null);
    }
    /// <summary>
    /// Checks if a schedule with the specified ID exists.
    /// </summary>
    /// <param name="scheduleName">The schedule identifier to check.</param>
    /// <param name="idPostfix">The idPostfix to use for the schedule.</param>
    /// <returns>True if the schedule exists, false otherwise.</returns>
    internal async Task<bool> ExistsAsync(string scheduleName, string? idPostfix = null)
    {
        if (string.IsNullOrWhiteSpace(scheduleName))
            throw new ArgumentException("Schedule name cannot be null or empty", nameof(scheduleName));
        
        try
        {
            await GetAsync(scheduleName, idPostfix);
            return true;
        }
        catch (ScheduleNotFoundException)
        {
            return false;
        }
    }

    public async Task PauseAsync(string scheduleName, string? note = null)
    {
        await PauseAsync(scheduleName, null, note);
    }   
    /// <summary>
    /// Pauses a schedule by ID.
    /// </summary>
    /// <param name="scheduleName">The schedule identifier to pause.</param>
    /// <param name="idPostfix">The idPostfix to use for the schedule.</param>
    /// <param name="note">Optional note explaining why the schedule is paused.</param>
    internal async Task PauseAsync(string scheduleName, string? idPostfix = null, string? note = null)
    {
        var schedule = await GetAsync(scheduleName, idPostfix);
        await schedule.PauseAsync(note);
    }

    public async Task UnpauseAsync(string scheduleName, string? note = null)
    {
        await UnpauseAsync(scheduleName, null, note);
    }
    /// <summary>
    /// Unpauses a schedule by ID.
    /// </summary>
    /// <param name="scheduleName">The schedule identifier to unpause.</param>
    /// <param name="idPostfix">The idPostfix to use for the schedule.</param>
    /// <param name="note">Optional note explaining why the schedule is unpaused.</param>
    internal async Task UnpauseAsync(string scheduleName, string? idPostfix = null, string? note = null)
    {
        var schedule = await GetAsync(scheduleName, idPostfix);
        await schedule.UnpauseAsync(note);
    }

    public async Task TriggerAsync(string scheduleName)
    {
        await TriggerAsync(scheduleName, null);
    }
    /// <summary>
    /// Triggers an immediate execution of a schedule by ID.
    /// </summary>
    /// <param name="scheduleName">The schedule identifier to trigger.</param>
    /// <param name="idPostfix">The idPostfix to use for the schedule.</param>
    internal async Task TriggerAsync(string scheduleName, string? idPostfix = null)
    {
        var schedule = await GetAsync(scheduleName, idPostfix);
        await schedule.TriggerAsync();
    }

}

