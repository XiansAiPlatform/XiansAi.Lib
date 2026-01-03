using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Workflows.Scheduling;
using Xians.Lib.Workflows.Scheduling.Models;

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
        string? timezone = null)
    {
        var request = new CreateCronScheduleRequest
        {
            ScheduleId = scheduleId,
            CronExpression = cronExpression,
            WorkflowInput = workflowInput,
            Timezone = timezone
        };

        return await ExecuteAsync(
            act => act.CreateScheduleIfNotExists(request),
            async svc =>
            {
                if (await svc.ExistsAsync(scheduleId))
                    return false;

                await svc.Create(scheduleId)
                    .WithCronSchedule(cronExpression, timezone)
                    .WithInput(workflowInput)
                    .StartAsync();
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
        object[] workflowInput)
    {
        var request = new CreateIntervalScheduleRequest
        {
            ScheduleId = scheduleId,
            Interval = interval,
            WorkflowInput = workflowInput
        };

        return await ExecuteAsync(
            act => act.CreateIntervalScheduleIfNotExists(request),
            async svc =>
            {
                if (await svc.ExistsAsync(scheduleId))
                    return false;

                await svc.Create(scheduleId)
                    .WithIntervalSchedule(interval)
                    .WithInput(workflowInput)
                    .StartAsync();
                return true;
            },
            operationName: "CreateIntervalScheduleIfNotExists");
    }

    /// <summary>
    /// Checks if a schedule exists using context-aware execution.
    /// </summary>
    public async Task<bool> ScheduleExistsAsync(string scheduleId)
    {
        var request = new ScheduleExistsRequest
        {
            ScheduleId = scheduleId
        };

        return await ExecuteAsync(
            act => act.ScheduleExists(request),
            svc => svc.ExistsAsync(scheduleId),
            operationName: "ScheduleExists");
    }
}

