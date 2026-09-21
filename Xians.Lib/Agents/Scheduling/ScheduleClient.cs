using Microsoft.Extensions.Logging;
using Temporalio.Client.Schedules;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Scheduling.Models;
using Xians.Lib.Temporal;
using Xians.Lib.Temporal.Workflows.Scheduling.Models;

namespace Xians.Lib.Agents.Scheduling;

/// <summary>
/// Temporal-client operations for schedules. Used directly from activities and by the
/// workflow activity stub (never from workflow code).
/// </summary>
internal sealed class ScheduleClient
{
    private readonly XiansAgent _agent;
    private readonly ITemporalClientService _temporalService;
    private readonly ILogger _logger;

    public ScheduleClient(XiansAgent agent)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _temporalService = agent.TemporalService
            ?? throw new InvalidOperationException("Temporal service is not configured. Cannot manage schedules.");
        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<ScheduleClient>();
    }

    public async Task<XiansSchedule> GetAsync(string scheduleName, string? idPostfix)
    {
        try
        {
            var client = await _temporalService.GetClientAsync();
            string tenantId = XiansContext.TenantId;
            idPostfix ??= XiansContext.GetIdPostfix();

            var fullScheduleId = ScheduleIdHelper.BuildFullScheduleId(tenantId, _agent.Name, idPostfix, scheduleName);
            var handle = client.GetScheduleHandle(fullScheduleId);
            await handle.DescribeAsync();
            return new XiansSchedule(handle, _agent, scheduleName, idPostfix);
        }
        catch (Temporalio.Exceptions.RpcException ex) when (
            ex.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
        {
            _logger.LogDebug("Schedule '{ScheduleName}' not found", scheduleName);
            throw new ScheduleNotFoundException(scheduleName, ex);
        }
        catch (ScheduleNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get schedule '{ScheduleName}'", scheduleName);
            throw;
        }
    }

    public async Task<ScheduleIdentity> GetIdentityAsync(string scheduleName, string? idPostfix)
    {
        var schedule = await GetAsync(scheduleName, idPostfix);
        return new ScheduleIdentity
        {
            ScheduleName = scheduleName,
            IdPostfix = schedule.IdPostfix,
            FullScheduleId = schedule.Id
        };
    }

    public async Task<bool> ExistsAsync(string scheduleName, string? idPostfix)
    {
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

    public async Task DeleteAsync(string scheduleName, string? idPostfix)
    {
        var schedule = await GetAsync(scheduleName, idPostfix);
        await schedule.DeleteViaHandleAsync();
    }

    public async Task PauseAsync(string scheduleName, string? idPostfix, string? note)
    {
        var schedule = await GetAsync(scheduleName, idPostfix);
        await schedule.PauseViaHandleAsync(note);
    }

    public async Task UnpauseAsync(string scheduleName, string? idPostfix, string? note)
    {
        var schedule = await GetAsync(scheduleName, idPostfix);
        await schedule.UnpauseViaHandleAsync(note);
    }

    public async Task TriggerAsync(string scheduleName, string? idPostfix)
    {
        var schedule = await GetAsync(scheduleName, idPostfix);
        await schedule.TriggerViaHandleAsync();
    }

    public async Task<ScheduleDescription> DescribeAsync(string scheduleName, string? idPostfix)
    {
        var schedule = await GetAsync(scheduleName, idPostfix);
        return await schedule.DescribeViaHandleAsync();
    }

    public async Task BackfillAsync(
        string scheduleName,
        string? idPostfix,
        IReadOnlyCollection<ScheduleBackfill> backfills)
    {
        var schedule = await GetAsync(scheduleName, idPostfix);
        await schedule.BackfillViaHandleAsync(backfills);
    }
}
