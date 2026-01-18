using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Temporal.Workflows.Scheduling;
using Xians.Lib.Temporal.Workflows.Scheduling.Models;

namespace Xians.Lib.Agents.Scheduling;

/// <summary>
/// Activity executor for schedule operations.
/// Handles context-aware execution of schedule activities.
/// Eliminates duplication of Workflow.InWorkflow checks in ScheduleBuilder.
/// </summary>
internal class ScheduleActivityExecutor : ContextAwareActivityExecutor<ScheduleActivities, ScheduleCollection>
{
    private readonly XiansWorkflow _workflow;

    public ScheduleActivityExecutor(XiansWorkflow workflow, ILogger logger)
        : base(logger)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
    }

    protected override ScheduleCollection CreateService()
    {
        return _workflow.Schedules
            ?? throw new InvalidOperationException("Schedule collection is not available.");
    }

    /// <summary>
    /// Creates a cron schedule if it doesn't exist using context-aware execution.
    /// </summary>
    public async Task<bool> CreateScheduleIfNotExistsAsync(
        string scheduleId,
        string cronExpression,
        object[] workflowInput,
        string idPostfix,
        string? timezone = null)
    {
        var request = new CreateCronScheduleRequest
        {
            ScheduleId = scheduleId,
            CronExpression = cronExpression,
            WorkflowInput = workflowInput,
            IdPostfix = idPostfix,
            Timezone = timezone
        };

        return await ExecuteAsync(
            act => act.CreateScheduleIfNotExists(request),
            async svc =>
            {
                if (await svc.ExistsAsync(scheduleId, idPostfix))
                    return false;

                await svc.Create(scheduleId, idPostfix)
                    .WithCronSchedule(cronExpression, timezone)
                    .WithInput(workflowInput)
                    .CreateIfNotExistsAsync();
                return true;
            },
            operationName: "CreateScheduleIfNotExists");
    }

    /// <summary>
    /// Creates an interval schedule if it doesn't exist using context-aware execution.
    /// </summary>
    public async Task<bool> CreateIntervalScheduleIfNotExistsAsync(
        string scheduleId,
        TimeSpan interval,
        object[] workflowInput,
        string idPostfix)
    {
        var request = new CreateIntervalScheduleRequest
        {
            ScheduleId = scheduleId,
            Interval = interval,
            WorkflowInput = workflowInput,
            IdPostfix = idPostfix
        };

        return await ExecuteAsync(
            act => act.CreateIntervalScheduleIfNotExists(request),
            async svc =>
            {
                if (await svc.ExistsAsync(scheduleId, idPostfix))
                    return false;

                await svc.Create(scheduleId, idPostfix)
                    .WithIntervalSchedule(interval)
                    .WithInput(workflowInput)
                    .CreateIfNotExistsAsync();
                return true;
            },
            operationName: "CreateIntervalScheduleIfNotExists");
    }

    /// <summary>
    /// Checks if a schedule exists using context-aware execution.
    /// </summary>
    public async Task<bool> ScheduleExistsAsync(string scheduleId, string idPostfix)
    {
        var request = new ScheduleExistsRequest
        {
            ScheduleId = scheduleId,
            IdPostfix = idPostfix
        };

        return await ExecuteAsync(
            act => act.ScheduleExists(request),
            svc => svc.ExistsAsync(scheduleId, idPostfix),
            operationName: "ScheduleExists");
    }
}

