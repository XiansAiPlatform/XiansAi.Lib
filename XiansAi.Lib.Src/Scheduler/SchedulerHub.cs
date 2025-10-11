using Temporal;
using Temporalio.Client;
using Temporalio.Client.Schedules;
using XiansAi.Flow;

namespace XiansAi.Scheduler;

/// <summary>
/// Provides scheduling capabilities for FlowBase temporal workflows.
/// Enables creating, managing, and controlling scheduled workflow executions.
/// </summary>
public class SchedulerHub
{
    private readonly ITemporalClient _client;

    public SchedulerHub()
    {
        var client = TemporalClientService.Instance.GetClientAsync().Result;
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Creates a new scheduled workflow execution.
    /// </summary>
    /// <param name="scheduleId">Unique identifier for the schedule</param>
    /// <param name="workflowIdOrType">Target workflow ID or type identifier</param>
    /// <param name="spec">Schedule specification defining when and how often to run</param>
    /// <param name="workflowId">Optional workflow ID for the scheduled executions</param>
    /// <param name="taskQueue">Optional task queue (defaults to workflow type)</param>
    /// <returns>Schedule handle for the created schedule</returns>
    public async Task<ScheduleHandle> CreateScheduleAsync(
        string scheduleName,
        string workflowIdOrType,
        ScheduleSpec spec)
    {
        var identifier = new WorkflowIdentifier(workflowIdOrType);
        var workflowType = identifier.WorkflowType;
        var classType = WorkflowIdentifier.GetClassTypeFor(workflowType);
        var scheduleId = $"{scheduleName}-{Guid.NewGuid()}";

        if (classType == null)
            throw new InvalidOperationException($"No FlowBase implementation found for workflow type '{workflowType}'");

        var taskQueue = AgentContext.SystemScoped ? workflowType : $"{AgentContext.TenantId}:{workflowType}";
        var workflowId = $"{identifier.WorkflowId}-{scheduleId}";

        var action = ScheduleActionStartWorkflow.Create(
            workflowType,
            Array.Empty<object>(),
            new(id: workflowId, taskQueue: taskQueue));

        return await _client.CreateScheduleAsync(
            scheduleId,
            new Schedule(Action: action, Spec: spec));
    }

    /// <summary>
    /// Creates a new scheduled workflow execution using workflow type.
    /// </summary>
    /// <param name="scheduleId">Unique identifier for the schedule</param>
    /// <param name="targetWorkflowType">Target workflow class type</param>
    /// <param name="spec">Schedule specification defining when and how often to run</param>
    /// <param name="workflowId">Optional workflow ID for the scheduled executions</param>
    /// <param name="taskQueue">Optional task queue (defaults to workflow type)</param>
    /// <returns>Schedule handle for the created schedule</returns>
    public async Task<ScheduleHandle> CreateScheduleAsync(
        string scheduleId,
        Type targetWorkflowType,
        ScheduleSpec spec)
    {
        var workflowType = WorkflowIdentifier.GetWorkflowTypeFor(targetWorkflowType);
        
        if (string.IsNullOrEmpty(workflowType))
            throw new InvalidOperationException($"No workflow type found for {targetWorkflowType.Name}");

        return await CreateScheduleAsync(scheduleId, workflowType, spec);
    }

    /// <summary>
    /// Backfills a scheduled workflow execution for missed or delayed actions.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier</param>
    /// <param name="backfills">List of backfill specifications</param>
    public async Task BackfillAsync(string scheduleId, IReadOnlyCollection<ScheduleBackfill> backfills)
    {
        var handle = _client.GetScheduleHandle(scheduleId);
        await handle.BackfillAsync(backfills);
    }

    /// <summary>
    /// Deletes a scheduled workflow. Does not affect workflows already started by the schedule.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier</param>
    public async Task DeleteAsync(string scheduleId)
    {
        var handle = _client.GetScheduleHandle(scheduleId);
        await handle.DeleteAsync();
    }

    /// <summary>
    /// Describes a scheduled workflow, showing configuration and execution history.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier</param>
    /// <returns>Schedule description with detailed information</returns>
    public async Task<ScheduleDescription> DescribeAsync(string scheduleId)
    {
        var handle = _client.GetScheduleHandle(scheduleId);
        return await handle.DescribeAsync();
    }

    /// <summary>
    /// Lists all available schedules.
    /// </summary>
    /// <returns>Async enumerable of schedule descriptions</returns>
    public IAsyncEnumerable<ScheduleListDescription> ListSchedulesAsync()
    {
        return _client.ListSchedulesAsync();
    }

    /// <summary>
    /// Pauses a schedule, stopping all future workflow runs.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier</param>
    /// <param name="note">Optional note explaining why the schedule is paused</param>
    public async Task PauseAsync(string scheduleId, string? note = null)
    {
        var handle = _client.GetScheduleHandle(scheduleId);
        await handle.PauseAsync(note);
    }

    /// <summary>
    /// Unpauses a previously paused schedule.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier</param>
    /// <param name="note">Optional note explaining why the schedule is unpaused</param>
    public async Task UnpauseAsync(string scheduleId, string? note = null)
    {
        var handle = _client.GetScheduleHandle(scheduleId);
        await handle.UnpauseAsync(note);
    }

    /// <summary>
    /// Triggers an immediate execution of a scheduled workflow.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier</param>
    public async Task TriggerAsync(string scheduleId)
    {
        var handle = _client.GetScheduleHandle(scheduleId);
        await handle.TriggerAsync();
    }

    /// <summary>
    /// Updates an existing schedule configuration.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier</param>
    /// <param name="updater">Function to update the schedule</param>
    public async Task UpdateAsync(string scheduleId, Func<ScheduleUpdateInput, ScheduleUpdate> updater)
    {
        var handle = _client.GetScheduleHandle(scheduleId);
        await handle.UpdateAsync(updater);
    }

    /// <summary>
    /// Gets a schedule handle for direct manipulation.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier</param>
    /// <returns>Schedule handle</returns>
    public ScheduleHandle GetScheduleHandle(string scheduleId)
    {
        return _client.GetScheduleHandle(scheduleId);
    }
}

