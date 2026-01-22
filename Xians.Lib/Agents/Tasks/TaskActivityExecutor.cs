using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Tasks.Models;
using Xians.Lib.Temporal;
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
        var agentName = XiansContext.CurrentAgent?.Name 
            ?? throw new InvalidOperationException("Agent name not available in workflow context");
        return new TaskService(_client, agentName, _tenantId, logger);
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
    /// Performs an action on a task using context-aware execution.
    /// </summary>
    public async Task PerformActionAsync(string taskId, string action, string? comment = null)
    {
        await ExecuteAsync(
            act => act.PerformActionAsync(_tenantId, taskId, action, comment),
            svc => svc.PerformActionAsync(taskId, action, comment),
            operationName: "PerformAction");
    }
}
