using Microsoft.Extensions.Logging;
using Temporalio.Client.Schedules;
using Temporalio.Exceptions;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Scheduling.Models;
using Xians.Lib.Temporal;
using Xians.Lib.Temporal.Workflows.Scheduling;
using Xians.Lib.Temporal.Workflows.Scheduling.Models;

namespace Xians.Lib.Agents.Scheduling;

/// <summary>
/// Context-aware executor for schedule management.
/// In a workflow the call is stubbed to <see cref="ScheduleActivities"/>;
/// in an activity it uses <see cref="ScheduleClient"/> against Temporal directly.
/// </summary>
internal sealed class ScheduleActivityExecutor : ContextAwareActivityExecutor<ScheduleActivities, ScheduleClient>
{
    private readonly XiansAgent _agent;

    public ScheduleActivityExecutor(XiansAgent agent, ILogger logger)
        : base(logger)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
    }

    protected override ScheduleClient CreateService() => new(_agent);

    protected override ActivityOptions GetDefaultActivityOptions()
        => ScheduleActivityOptions.GetStandardOptions();

    protected override Exception? TranslateActivityFailure(ApplicationFailureException failure)
        => ScheduleNotFoundException.FromFailure(failure);

    /// <summary>
    /// Resolves the full schedule id while still on the workflow side.
    /// </summary>
    /// <remarks>
    /// idPostfix lives in the workflow's search attributes and memo, which
    /// <c>WorkflowMetadataResolver</c> only reads when <c>Workflow.InWorkflow</c> is true. An activity
    /// left to resolve it falls back to parsing the workflow id, which yields null for ids with fewer
    /// than four segments - a different schedule than the caller addressed. Returns null outside a
    /// workflow, where <see cref="ScheduleClient"/> resolves it correctly on its own.
    /// </remarks>
    private string? ResolveFullScheduleId(string scheduleName, string? idPostfix)
    {
        if (!Workflow.InWorkflow)
            return null;

        if (string.IsNullOrEmpty(idPostfix))
            idPostfix = XiansContext.SafeIdPostfix;

        return ScheduleIdHelper.BuildFullScheduleId(
            XiansContext.ResolveTenantId(_agent), _agent.Name, idPostfix, scheduleName);
    }

    /// <summary>
    /// Prefers a full id already resolved by the caller (workflow list/get) over re-deriving it.
    /// </summary>
    private string? EffectiveFullScheduleId(string scheduleName, string? idPostfix, string? fullScheduleId)
        => !string.IsNullOrEmpty(fullScheduleId)
            ? fullScheduleId
            : ResolveFullScheduleId(scheduleName, idPostfix);

    public Task<ScheduleIdentity> GetIdentityAsync(string scheduleName, string? idPostfix, string? fullScheduleId = null)
    {
        var request = new GetScheduleRequest
        {
            ScheduleName = scheduleName,
            IdPostfix = idPostfix,
            FullScheduleId = EffectiveFullScheduleId(scheduleName, idPostfix, fullScheduleId)
        };

        return ExecuteAsync(
            act => act.GetSchedule(request),
            svc => svc.GetIdentityAsync(scheduleName, idPostfix, request.FullScheduleId),
            operationName: "GetSchedule");
    }

    public Task<bool> ExistsAsync(string scheduleName, string? idPostfix)
    {
        var request = new ScheduleExistsRequest
        {
            ScheduleName = scheduleName,
            IdPostfix = idPostfix,
            FullScheduleId = ResolveFullScheduleId(scheduleName, idPostfix)
        };

        return ExecuteAsync(
            act => act.ScheduleExists(request),
            svc => svc.ExistsAsync(scheduleName, idPostfix, request.FullScheduleId),
            options: ScheduleActivityOptions.GetQuickCheckOptions(),
            operationName: "ScheduleExists");
    }

    public Task<bool> DeleteAsync(string scheduleName, string? idPostfix, string? fullScheduleId = null)
    {
        var request = new DeleteScheduleRequest
        {
            ScheduleName = scheduleName,
            IdPostfix = idPostfix,
            FullScheduleId = EffectiveFullScheduleId(scheduleName, idPostfix, fullScheduleId)
        };

        return ExecuteAsync(
            act => act.DeleteSchedule(request),
            async svc =>
            {
                try
                {
                    await svc.DeleteAsync(scheduleName, idPostfix, request.FullScheduleId);
                    return true;
                }
                catch (ScheduleNotFoundException)
                {
                    return false;
                }
            },
            operationName: "DeleteSchedule");
    }

    public Task PauseAsync(string scheduleName, string? idPostfix, string? note, string? fullScheduleId = null)
    {
        var request = new PauseScheduleRequest
        {
            ScheduleName = scheduleName,
            IdPostfix = idPostfix,
            FullScheduleId = EffectiveFullScheduleId(scheduleName, idPostfix, fullScheduleId),
            Note = note
        };

        return ExecuteAsync(
            act => act.PauseSchedule(request),
            svc => svc.PauseAsync(scheduleName, idPostfix, note, request.FullScheduleId),
            operationName: "PauseSchedule");
    }

    public Task UnpauseAsync(string scheduleName, string? idPostfix, string? note, string? fullScheduleId = null)
    {
        var request = new ResumeScheduleRequest
        {
            ScheduleName = scheduleName,
            IdPostfix = idPostfix,
            FullScheduleId = EffectiveFullScheduleId(scheduleName, idPostfix, fullScheduleId),
            Note = note
        };

        return ExecuteAsync(
            act => act.ResumeSchedule(request),
            svc => svc.UnpauseAsync(scheduleName, idPostfix, note, request.FullScheduleId),
            operationName: "ResumeSchedule");
    }

    public Task TriggerAsync(string scheduleName, string? idPostfix, string? fullScheduleId = null)
    {
        var request = new TriggerScheduleRequest
        {
            ScheduleName = scheduleName,
            IdPostfix = idPostfix,
            FullScheduleId = EffectiveFullScheduleId(scheduleName, idPostfix, fullScheduleId)
        };

        return ExecuteAsync(
            act => act.TriggerSchedule(request),
            svc => svc.TriggerAsync(scheduleName, idPostfix, request.FullScheduleId),
            operationName: "TriggerSchedule");
    }

    public Task<ScheduleSnapshot> DescribeSnapshotAsync(string scheduleName, string? idPostfix, string? fullScheduleId = null)
    {
        var request = new GetScheduleRequest
        {
            ScheduleName = scheduleName,
            IdPostfix = idPostfix,
            FullScheduleId = EffectiveFullScheduleId(scheduleName, idPostfix, fullScheduleId)
        };

        return ExecuteAsync(
            act => act.DescribeSchedule(request),
            svc => svc.DescribeSnapshotAsync(scheduleName, idPostfix, request.FullScheduleId),
            operationName: "DescribeSchedule");
    }

    public Task BackfillAsync(
        string scheduleName,
        string? idPostfix,
        IReadOnlyCollection<ScheduleBackfill> backfills,
        string? fullScheduleId = null)
    {
        var request = new BackfillScheduleRequest
        {
            ScheduleName = scheduleName,
            IdPostfix = idPostfix,
            FullScheduleId = EffectiveFullScheduleId(scheduleName, idPostfix, fullScheduleId),
            Backfills = backfills
        };

        return ExecuteAsync(
            act => act.BackfillSchedule(request),
            svc => svc.BackfillAsync(scheduleName, idPostfix, backfills, request.FullScheduleId),
            operationName: "BackfillSchedule");
    }

    public Task<List<ScheduleIdentity>> ListAsync()
    {
        // Prefix is computed here, including on the workflow side, so tenant and idPostfix come from
        // search attributes / memo rather than from the activity's workflow-id parse.
        var request = ScheduleClient.BuildListRequest(_agent);

        return ExecuteAsync(
            act => act.ListSchedules(request),
            svc => svc.ListIdentitiesAsync(request),
            operationName: "ListSchedules");
    }
}
