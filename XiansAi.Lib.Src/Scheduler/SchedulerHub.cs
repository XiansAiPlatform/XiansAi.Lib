using Microsoft.Extensions.Logging;
using Temporal;
using Temporalio.Client;
using Temporalio.Client.Schedules;
using Temporalio.Common;

namespace XiansAi.Scheduler;


/// <summary>
/// Provides scheduling capabilities for FlowBase temporal workflows.
/// Enables creating, managing, and controlling scheduled workflow executions.
/// </summary>
public class SchedulerHub
{
    private static readonly ILogger<SchedulerHub> _logger = Globals.LogFactory.CreateLogger<SchedulerHub>();
    private static async Task<ITemporalClient> GetTemporalClient()
    {
        return await TemporalClientService.Instance.GetClientAsync();
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
    public static async Task<ScheduleHandle> CreateScheduleAsync(
        string workflowIdOrType,
        ScheduleSpec spec,
        string scheduleName, WorkflowOptions? options = null)
    {
        var identifier = new WorkflowIdentifier(workflowIdOrType);
        var workflowType = identifier.WorkflowType;
        var classType = WorkflowIdentifier.GetClassTypeFor(workflowType);

        if (classType == null)
            throw new InvalidOperationException($"No FlowBase implementation found for workflow type '{workflowType}'");

        var taskQueue = AgentContext.SystemScoped ? workflowType : $"{AgentContext.TenantId}:{workflowType}";
        var workflowId = $"{identifier.WorkflowId}-{scheduleName}";

        var defaultOptions = new WorkflowOptions(
            id: workflowId, 
            taskQueue: taskQueue
        );
        defaultOptions.RetryPolicy = new RetryPolicy{
            MaximumAttempts = 3
        };
        defaultOptions.RunTimeout = TimeSpan.FromSeconds(10*60);
        // Add search attributes and memo to workflow executions for consistency
        defaultOptions.TypedSearchAttributes = GetSearchAttributes();
        defaultOptions.Memo = GetMemo();

        options = options ?? defaultOptions;

        var action = ScheduleActionStartWorkflow.Create(
            workflowType,
            [ scheduleName ],
            options);

        var client = await GetTemporalClient();
        var handle = await client.CreateScheduleAsync(
            scheduleName,
            new Schedule(Action: action, Spec: spec));

        // Update schedule with search attributes (must be done after creation)
        await handle.UpdateAsync(scheduleUpdate =>
        {
            return new ScheduleUpdate(
                scheduleUpdate.Description.Schedule,
                TypedSearchAttributes: GetSearchAttributes());
        });

        _logger.LogInformation(
            "Schedule '{ScheduleName}' created successfully with search attributes for workflow '{WorkflowType}'",
            scheduleName, workflowType);

        return handle;
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
    public static async Task<ScheduleHandle> CreateScheduleAsync(
        Type targetWorkflowType,
        ScheduleSpec spec,
        string scheduleName)
    {
        var workflowType = WorkflowIdentifier.GetWorkflowTypeFor(targetWorkflowType);
        
        if (string.IsNullOrEmpty(workflowType))
            throw new InvalidOperationException($"No workflow type found for {targetWorkflowType.Name}");

        return await CreateScheduleAsync(workflowType, spec, scheduleName);
    }

    /// <summary>
    /// Backfills a scheduled workflow execution for missed or delayed actions.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier</param>
    /// <param name="backfills">List of backfill specifications</param>
    public async Task BackfillAsync(string scheduleId, IReadOnlyCollection<ScheduleBackfill> backfills)
    {
        var client = await GetTemporalClient();
        var handle = client.GetScheduleHandle(scheduleId);
        await handle.BackfillAsync(backfills);
    }

    /// <summary>
    /// Deletes a scheduled workflow. Does not affect workflows already started by the schedule.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier</param>
    public static async Task DeleteAsync(string scheduleId)
    {
        try {
            var client = await GetTemporalClient();
            var handle = client.GetScheduleHandle(scheduleId);
            await handle.DeleteAsync();
        } catch (Temporalio.Exceptions.RpcException ex) when (
            ex.Message?.Contains("workflow not found", StringComparison.OrdinalIgnoreCase) == true)
        {
            _logger.LogWarning($"Schedule {scheduleId} not found or already deleted.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting schedule {scheduleId}: {ex}");
        }

    }

    /// <summary>
    /// Describes a scheduled workflow, showing configuration and execution history.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier</param>
    /// <returns>Schedule description with detailed information</returns>
    public static async Task<ScheduleDescription> DescribeAsync(string scheduleId)
    {
        var client = await GetTemporalClient();
        var handle = client.GetScheduleHandle(scheduleId);
        return await handle.DescribeAsync();
    }

    /// <summary>
    /// Lists all available schedules.
    /// </summary>
    /// <returns>Async enumerable of schedule descriptions</returns>
    public static async Task<IAsyncEnumerable<ScheduleListDescription>> ListSchedulesAsync()
    {
        var client = await GetTemporalClient();
        return client.ListSchedulesAsync();
    }

    /// <summary>
    /// Pauses a schedule, stopping all future workflow runs.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier</param>
    /// <param name="note">Optional note explaining why the schedule is paused</param>
    public static async Task PauseAsync(string scheduleId, string? note = null)
    {
        var client = await GetTemporalClient();
        var handle = client.GetScheduleHandle(scheduleId);
        await handle.PauseAsync(note);
    }

    /// <summary>
    /// Unpauses a previously paused schedule.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier</param>
    /// <param name="note">Optional note explaining why the schedule is unpaused</param>
    public static async Task UnpauseAsync(string scheduleId, string? note = null)
    {
        var client = await GetTemporalClient();
        var handle = client.GetScheduleHandle(scheduleId);
        await handle.UnpauseAsync(note);
    }

    /// <summary>
    /// Triggers an immediate execution of a scheduled workflow.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier</param>
    public static async Task TriggerAsync(string scheduleId)
    {
        var client = await GetTemporalClient();
        var handle = client.GetScheduleHandle(scheduleId);
        await handle.TriggerAsync();
    }

    /// <summary>
    /// Updates an existing schedule configuration.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier</param>
    /// <param name="updater">Function to update the schedule</param>
    public static async Task UpdateAsync(string scheduleId, Func<ScheduleUpdateInput, ScheduleUpdate> updater)
    {
        var client = await GetTemporalClient();
        var handle = client.GetScheduleHandle(scheduleId);
        await handle.UpdateAsync(updater);
    }

    /// <summary>
    /// Gets a schedule handle for direct manipulation.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier</param>
    /// <returns>Schedule handle</returns>
    public static async Task<ScheduleHandle> GetScheduleHandle(string scheduleId)
    {
        var client = await GetTemporalClient();
        return client.GetScheduleHandle(scheduleId);
    }

    /// <summary>
    /// Gets search attributes for both the schedule and scheduled workflow executions.
    /// Includes TenantId, AgentName, and UserId for schedule and workflow tracking and filtering.
    /// </summary>
    private static SearchAttributeCollection GetSearchAttributes()
    {
        var tenantId = AgentContext.TenantId;
        var agentName = AgentContext.AgentName;
        var userId = AgentContext.UserId;

        var searchAttributesBuilder = new SearchAttributeCollection.Builder()
            .Set(SearchAttributeKey.CreateKeyword(Constants.TenantIdKey), tenantId)
            .Set(SearchAttributeKey.CreateKeyword(Constants.AgentKey), agentName)
            .Set(SearchAttributeKey.CreateKeyword(Constants.UserIdKey), userId);

        return searchAttributesBuilder.ToSearchAttributeCollection();
    }

    /// <summary>
    /// Gets memo for scheduled workflow executions.
    /// Includes TenantId, AgentName, UserId, and SystemScoped for consistency with search attributes.
    /// </summary>
    private static Dictionary<string, object> GetMemo()
    {
        return new Dictionary<string, object>
        {
            { Constants.TenantIdKey, AgentContext.TenantId },
            { Constants.AgentKey, AgentContext.AgentName },
            { Constants.UserIdKey, AgentContext.UserId },
            { Constants.SystemScopedKey, AgentContext.SystemScoped }
        };
    }
}

