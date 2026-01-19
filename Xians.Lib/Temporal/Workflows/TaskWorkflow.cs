using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Xians.Lib.Agents.Tasks.Models;

namespace Xians.Lib.Temporal.Workflows;

public class TaskWorkflow
{
    private static readonly string[] DefaultActions = ["approve", "reject"];
    
    private readonly ILogger<TaskWorkflow> _logger;
    private bool _isCompleted;
    private string? _initialWork;
    private string? _finalWork;
    private TaskWorkflowRequest? _request;
    private string _taskId = string.Empty;
    private string[]? _availableActions;
    private string? _performedAction;
    private string? _actionComment;

    public TaskWorkflow()
    {
        _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<TaskWorkflow>();
    }

    [WorkflowRun]
    public virtual async Task<TaskWorkflowResult> RunAsync(TaskWorkflowRequest request)
    {
        _request = request;
        _taskId = request.TaskId ?? throw new ArgumentException("TaskId must be provided", nameof(request));
        _availableActions = request.Actions is { Length: > 0 } ? request.Actions : DefaultActions;
        _initialWork = request.DraftWork;
        _finalWork = request.DraftWork;

        _logger.LogInformation("Task started: {TaskId} - {Title}, Actions: [{Actions}]", 
            _taskId, request.Title, string.Join(", ", _availableActions));

        await Workflow.WaitConditionAsync(() => _isCompleted);

        _logger.LogInformation("Task completed: {TaskId}, Action={Action}", 
            _taskId, _performedAction);

        return new TaskWorkflowResult
        {
            TaskId = _taskId,
            InitialWork = _initialWork,
            FinalWork = _finalWork,
            PerformedAction = _performedAction,
            Comment = _actionComment,
            CompletedAt = Workflow.UtcNow
        };
    }

    [WorkflowSignal]
    public Task UpdateDraft(string updatedDraft)
    {
        _logger.LogInformation("Draft updated for task: {TaskId}", _taskId);
        _finalWork = updatedDraft;
        return Task.CompletedTask;
    }

    [WorkflowSignal]
    public Task PerformAction(TaskActionRequest actionRequest)
    {
        _logger.LogInformation("Action performed on task {TaskId}: {Action}, Comment: {Comment}", 
            _taskId, actionRequest.Action, actionRequest.Comment ?? "(none)");
        
        _performedAction = actionRequest.Action;
        _actionComment = actionRequest.Comment;
        _isCompleted = true;
        
        return Task.CompletedTask;
    }

    [WorkflowQuery]
    public TaskInfo GetTaskInfo()
    {
        return new TaskInfo
        {
            TaskId = _taskId,
            Title = _request?.Title ?? string.Empty,
            Description = _request?.Description ?? string.Empty,
            InitialWork = _initialWork,
            FinalWork = _finalWork,
            IsCompleted = _isCompleted,
            ParticipantId = _request?.ParticipantId ?? string.Empty,
            Metadata = _request?.Metadata,
            AvailableActions = _availableActions,
            PerformedAction = _performedAction,
            Comment = _actionComment
        };
    }
}
