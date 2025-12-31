using Microsoft.Extensions.Logging;
using Temporalio.Common;
using Temporalio.Workflows;
using Xians.Lib.Common;
using Xians.Lib.Agents.Tasks.Models;

[Workflow("Platform:Task Workflow")]
public class TaskWorkflow
{
    private readonly ILogger<TaskWorkflow> _logger;
    private bool _isCompleted = false;
    private string? _rejectionMessage;
    private string? _currentDraft;
    private TaskWorkflowRequest? _request;
    private string _taskId = string.Empty;

    public TaskWorkflow()
    {
        _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<TaskWorkflow>();
    }

    [WorkflowRun]
    public async Task<TaskWorkflowResult> RunAsync(TaskWorkflowRequest request)
    {
        _request = request;
        
        // TaskId is now guaranteed to be set by TaskWorkflowService
        _taskId = request.TaskId ?? throw new ArgumentException("TaskId must be provided", nameof(request));
        
        // Override userId search attribute with ParticipantId to ensure it's not inherited from parent
        var userIdKey = SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.UserId);
        Workflow.UpsertTypedSearchAttributes(userIdKey.ValueSet(request.ParticipantId));
        
        _logger.LogInformation("Human-in-the-loop task started: {TaskId} - {Title}", 
            _taskId, request.Title);

        // Initialize with the provided draft work
        _currentDraft = request.DraftWork;

        // Wait for human to complete or reject the task via signal
        // This blocks the workflow until CompleteTask or RejectTask is called
        await Workflow.WaitConditionAsync(() => _isCompleted);

        _logger.LogInformation("Human-in-the-loop task finished: {TaskId}, Rejected={IsRejected}", 
            _taskId, _rejectionMessage != null);

        return new TaskWorkflowResult
        {
            TaskId = _taskId,
            Success = _rejectionMessage == null, // Success if not rejected
            FinalWork = _currentDraft,
            RejectionReason = _rejectionMessage,
            CompletedAt = Workflow.UtcNow
        };
    }

    [WorkflowSignal]
    public Task UpdateDraft(string updatedDraft)
    {
        _logger.LogInformation("Draft updated");
        _currentDraft = updatedDraft;
        return Task.CompletedTask;
    }

    [WorkflowSignal]
    public Task CompleteTask()
    {
        _logger.LogInformation("Task completed successfully");
        _isCompleted = true;
        
        return Task.CompletedTask;
    }

    [WorkflowSignal]
    public Task RejectTask(string rejectionMessage)
    {
        _logger.LogWarning("Task rejected: {RejectionMessage}", rejectionMessage);
        _rejectionMessage = rejectionMessage;
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
            CurrentDraft = _currentDraft,
            Success = _rejectionMessage == null,
            IsCompleted = _isCompleted,
            RejectionReason = _rejectionMessage,
            ParticipantId = _request?.ParticipantId ?? string.Empty,
            Metadata = _request?.Metadata
        };
    }
}