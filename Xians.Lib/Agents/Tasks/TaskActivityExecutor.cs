using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Tasks.Models;
using Xians.Lib.Temporal.Workflows.Tasks;

namespace Xians.Lib.Agents.Tasks;

/// <summary>
/// Activity executor for task operations.
/// Handles context-aware execution of task activities.
/// Eliminates duplication of Workflow.InWorkflow checks in TaskCollection.
/// </summary>
internal class TaskActivityExecutor : ContextAwareActivityExecutor<TaskActivities, TaskService>
{
    private readonly ITemporalClient _client;
    private readonly string _tenantId;

    public TaskActivityExecutor(ITemporalClient client, string tenantId, ILogger logger)
        : base(logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _tenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
    }

    protected override TaskService CreateService()
    {
        var logger = Common.Infrastructure.LoggerFactory.CreateLogger<TaskService>();
        return new TaskService(_client, _tenantId, logger);
    }

    /// <summary>
    /// Queries the current status of a task workflow using context-aware execution.
    /// </summary>
    public async Task<TaskInfo> QueryTaskInfoAsync(string taskId)
    {
        return await ExecuteAsync(
            act => act.QueryTaskInfoAsync(_tenantId, taskId),
            svc => svc.QueryTaskInfoAsync(taskId),
            operationName: "QueryTaskInfo");
    }

    /// <summary>
    /// Sends a signal to update the draft work using context-aware execution.
    /// </summary>
    public async Task UpdateDraftAsync(string taskId, string updatedDraft)
    {
        await ExecuteAsync(
            act => act.UpdateDraftAsync(_tenantId, taskId, updatedDraft),
            svc => svc.UpdateDraftAsync(taskId, updatedDraft),
            operationName: "UpdateDraft");
    }

    /// <summary>
    /// Sends a signal to complete the task using context-aware execution.
    /// </summary>
    public async Task CompleteTaskAsync(string taskId)
    {
        await ExecuteAsync(
            act => act.CompleteTaskAsync(_tenantId, taskId),
            svc => svc.CompleteTaskAsync(taskId),
            operationName: "CompleteTask");
    }

    /// <summary>
    /// Sends a signal to reject the task using context-aware execution.
    /// </summary>
    public async Task RejectTaskAsync(string taskId, string rejectionMessage)
    {
        await ExecuteAsync(
            act => act.RejectTaskAsync(_tenantId, taskId, rejectionMessage),
            svc => svc.RejectTaskAsync(taskId, rejectionMessage),
            operationName: "RejectTask");
    }
}

