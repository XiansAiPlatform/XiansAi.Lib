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

    public Task<ScheduleIdentity> GetIdentityAsync(string scheduleName, string? idPostfix)
    {
        var request = new GetScheduleRequest
        {
            ScheduleName = scheduleName,
            IdPostfix = idPostfix,
            FullScheduleId = ResolveFullScheduleId(scheduleName, idPostfix)
        };

        return ExecuteAsync(
            act => act.GetSchedule(request),
            svc => svc.GetIdentityAsync(scheduleName, idPostfix),
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
            svc => svc.ExistsAsync(scheduleName, idPostfix),
            options: ScheduleActivityOptions.GetQuickCheckOptions(),
            operationName: "ScheduleExists");
    }

    public Task<bool> DeleteAsync(string scheduleName, string? idPostfix)
    {
        var request = new DeleteScheduleRequest
        {
            ScheduleName = scheduleName,
            IdPostfix = idPostfix,
            FullScheduleId = ResolveFullScheduleId(scheduleName, idPostfix)
        };

        return ExecuteAsync(
            act => act.DeleteSchedule(request),
            async svc =>
            {
                try
                {
                    await svc.DeleteAsync(scheduleName, idPostfix);
                    return true;
                }
                catch (ScheduleNotFoundException)
                {
                    return false;
                }
            },
            operationName: "DeleteSchedule");
    }

    public Task PauseAsync(string scheduleName, string? idPostfix, string? note)
    {
        var request = new PauseScheduleRequest
        {
            ScheduleName = scheduleName,
            IdPostfix = idPostfix,
            FullScheduleId = ResolveFullScheduleId(scheduleName, idPostfix),
            Note = note
        };

        return ExecuteAsync(
            act => act.PauseSchedule(request),
            svc => svc.PauseAsync(scheduleName, idPostfix, note),
            operationName: "PauseSchedule");
    }

    public Task UnpauseAsync(string scheduleName, string? idPostfix, string? note)
    {
        var request = new ResumeScheduleRequest
        {
            ScheduleName = scheduleName,
            IdPostfix = idPostfix,
            FullScheduleId = ResolveFullScheduleId(scheduleName, idPostfix),
            Note = note
        };

        return ExecuteAsync(
            act => act.ResumeSchedule(request),
            svc => svc.UnpauseAsync(scheduleName, idPostfix, note),
            operationName: "ResumeSchedule");
    }

    public Task TriggerAsync(string scheduleName, string? idPostfix)
    {
        var request = new TriggerScheduleRequest
        {
            ScheduleName = scheduleName,
            IdPostfix = idPostfix,
            FullScheduleId = ResolveFullScheduleId(scheduleName, idPostfix)
        };

        return ExecuteAsync(
            act => act.TriggerSchedule(request),
            svc => svc.TriggerAsync(scheduleName, idPostfix),
            operationName: "TriggerSchedule");
    }

    public Task<ScheduleSnapshot> DescribeSnapshotAsync(string scheduleName, string? idPostfix)
    {
        var request = new GetScheduleRequest
        {
            ScheduleName = scheduleName,
            IdPostfix = idPostfix,
            FullScheduleId = ResolveFullScheduleId(scheduleName, idPostfix)
        };

        return ExecuteAsync(
            act => act.DescribeSchedule(request),
            svc => svc.DescribeSnapshotAsync(scheduleName, idPostfix),
            operationName: "DescribeSchedule");
    }

    public Task BackfillAsync(
        string scheduleName,
        string? idPostfix,
        IReadOnlyCollection<ScheduleBackfill> backfills)
    {
        var request = new BackfillScheduleRequest
        {
            ScheduleName = scheduleName,
            IdPostfix = idPostfix,
            FullScheduleId = ResolveFullScheduleId(scheduleName, idPostfix),
            Backfills = backfills
        };

        return ExecuteAsync(
            act => act.BackfillSchedule(request),
            svc => svc.BackfillAsync(scheduleName, idPostfix, backfills),
            operationName: "BackfillSchedule");
    }
}
