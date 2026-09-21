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
/// <remarks>
/// Every method takes an optional <c>fullScheduleId</c>. When the caller was workflow code it has
/// already resolved the id against workflow search attributes and memo, which are unreadable from an
/// activity - so a non-null value is used verbatim rather than re-derived.
/// </remarks>
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

    public async Task<XiansSchedule> GetAsync(string scheduleName, string? activationName, string? fullScheduleId = null)
    {
        try
        {
            var client = await _temporalService.GetClientAsync();

            if (fullScheduleId == null)
            {
                // Treat empty the same as absent. BuildFullScheduleId emits an empty segment for ""
                // but omits the segment entirely for null, so the two produce different ids.
                if (string.IsNullOrEmpty(activationName))
                    activationName = XiansContext.GetIdPostfix();

                fullScheduleId = ScheduleIdHelper.BuildFullScheduleId(
                    XiansContext.ResolveTenantId(_agent), _agent.Name, activationName, scheduleName);
            }

            var handle = client.GetScheduleHandle(fullScheduleId);
            await handle.DescribeAsync();
            return new XiansSchedule(handle, _agent, scheduleName, activationName);
        }
        catch (Temporalio.Exceptions.RpcException ex) when (
            ex.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
        {
            _logger.LogDebug("Schedule '{ScheduleName}' not found", scheduleName);
            throw new ScheduleNotFoundException(scheduleName, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get schedule '{ScheduleName}'", scheduleName);
            throw;
        }
    }

    public async Task<ScheduleIdentity> GetIdentityAsync(
        string scheduleName, string? activationName, string? fullScheduleId = null)
    {
        var schedule = await GetAsync(scheduleName, activationName, fullScheduleId);
        return new ScheduleIdentity
        {
            ScheduleName = scheduleName,
            IdPostfix = schedule.IdPostfix,
            FullScheduleId = schedule.Id
        };
    }

    public async Task<bool> ExistsAsync(string scheduleName, string? activationName, string? fullScheduleId = null)
    {
        try
        {
            await GetAsync(scheduleName, activationName, fullScheduleId);
            return true;
        }
        catch (ScheduleNotFoundException)
        {
            return false;
        }
    }

    public async Task DeleteAsync(string scheduleName, string? activationName, string? fullScheduleId = null)
    {
        var schedule = await GetAsync(scheduleName, activationName, fullScheduleId);
        await schedule.DeleteViaHandleAsync();
    }

    public async Task PauseAsync(
        string scheduleName, string? activationName, string? note, string? fullScheduleId = null)
    {
        var schedule = await GetAsync(scheduleName, activationName, fullScheduleId);
        await schedule.PauseViaHandleAsync(note);
    }

    public async Task UnpauseAsync(
        string scheduleName, string? activationName, string? note, string? fullScheduleId = null)
    {
        var schedule = await GetAsync(scheduleName, activationName, fullScheduleId);
        await schedule.UnpauseViaHandleAsync(note);
    }

    public async Task TriggerAsync(string scheduleName, string? activationName, string? fullScheduleId = null)
    {
        var schedule = await GetAsync(scheduleName, activationName, fullScheduleId);
        await schedule.TriggerViaHandleAsync();
    }

    public async Task<ScheduleDescription> DescribeAsync(
        string scheduleName, string? activationName, string? fullScheduleId = null)
    {
        var schedule = await GetAsync(scheduleName, activationName, fullScheduleId);
        return await schedule.DescribeViaHandleAsync();
    }

    public async Task<ScheduleSnapshot> DescribeSnapshotAsync(
        string scheduleName, string? activationName, string? fullScheduleId = null)
    {
        var schedule = await GetAsync(scheduleName, activationName, fullScheduleId);
        var description = await schedule.DescribeViaHandleAsync();
        return ToSnapshot(schedule.Id, description);
    }

    public async Task BackfillAsync(
        string scheduleName,
        string? activationName,
        IReadOnlyCollection<ScheduleBackfill> backfills,
        string? fullScheduleId = null)
    {
        var schedule = await GetAsync(scheduleName, activationName, fullScheduleId);
        await schedule.BackfillViaHandleAsync(backfills);
    }

    /// <summary>
    /// Builds the list filter from the current tenant / agent / idPostfix.
    /// Called from workflow code so the prefix is resolved against search attributes and memo.
    /// </summary>
    internal static ListSchedulesRequest BuildListRequest(XiansAgent agent)
    {
        var tenantId = XiansContext.ResolveTenantId(agent);
        var idPostfix = XiansContext.SafeIdPostfix;
        return new ListSchedulesRequest
        {
            TenantId = tenantId,
            AgentName = agent.Name,
            IdPostfix = idPostfix,
            Prefix = ScheduleIdHelper.BuildFullScheduleId(tenantId, agent.Name, idPostfix, "")
        };
    }

    public async Task<List<XiansSchedule>> ListAsync(ListSchedulesRequest? request = null)
    {
        request ??= BuildListRequest(_agent);
        var client = await _temporalService.GetClientAsync();
        var result = new List<XiansSchedule>();
        var query =
            $"tenantId = '{ScheduleIdHelper.EscapeVisibilityLiteral(request.TenantId)}' AND " +
            $"agent = '{ScheduleIdHelper.EscapeVisibilityLiteral(request.AgentName)}'";

        await foreach (var schedule in client.ListSchedulesAsync(new ScheduleListOptions { Query = query }))
        {
            if (!schedule.Id.StartsWith(request.Prefix, StringComparison.Ordinal))
                continue;

            var identity = IdentityFromListedId(schedule.Id, request.Prefix, request.IdPostfix);
            result.Add(new XiansSchedule(
                client.GetScheduleHandle(identity.FullScheduleId),
                _agent,
                identity.ScheduleName,
                identity.IdPostfix));
        }

        return result;
    }

    public async Task<List<ScheduleIdentity>> ListIdentitiesAsync(ListSchedulesRequest request)
    {
        var schedules = await ListAsync(request);
        return schedules.Select(s => new ScheduleIdentity
        {
            ScheduleName = s.ScheduleName ?? s.Id,
            IdPostfix = s.IdPostfix,
            FullScheduleId = s.Id
        }).ToList();
    }

    internal static ScheduleIdentity IdentityFromListedId(string fullScheduleId, string prefix, string? idPostfix)
    {
        var scheduleName = fullScheduleId.StartsWith(prefix, StringComparison.Ordinal)
            ? fullScheduleId[prefix.Length..]
            : fullScheduleId;

        return new ScheduleIdentity
        {
            ScheduleName = scheduleName,
            IdPostfix = idPostfix,
            FullScheduleId = fullScheduleId
        };
    }

    internal static ScheduleSnapshot ToSnapshot(string id, ScheduleDescription description)
    {
        return new ScheduleSnapshot
        {
            Id = id,
            Paused = description.Schedule.State.Paused,
            Note = description.Schedule.State.Note,
            NumActions = description.Info.NumActions,
            NumActionsMissedCatchupWindow = description.Info.NumActionsMissedCatchupWindow,
            NumActionsSkippedOverlap = description.Info.NumActionsSkippedOverlap,
            CreatedAt = description.Info.CreatedAt,
            LastUpdatedAt = description.Info.LastUpdatedAt,
            NextActionTimes = description.Info.NextActionTimes.ToList()
        };
    }
}
