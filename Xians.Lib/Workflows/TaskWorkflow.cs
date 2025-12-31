using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

[Workflow("Platform:Task Workflow")]
public class TaskWorkflow
{
    private readonly ILogger<TaskWorkflow> _logger;
    private bool _taskCompleted;
    private string? _currentDraft;
    private string? _taskError;

    public TaskWorkflow()
    {
        _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<TaskWorkflow>();
    }

    [WorkflowRun]
    public async Task<TaskWorkflowResult> RunAsync(TaskWorkflowRequest request)
    {
        _logger.LogInformation("Human-in-the-loop task started: {TaskId} - {Title}", 
            request.TaskId, request.Title);

        // Initialize with the provided draft work
        _currentDraft = request.DraftWork;

        // Wait for human to complete the task via signal
        // This blocks the workflow until CompleteTask or FailTask is called
        await Workflow.WaitConditionAsync(() => _taskCompleted);

        _logger.LogInformation("Human-in-the-loop task completed: {TaskId}", request.TaskId);

        return new TaskWorkflowResult
        {
            TaskId = request.TaskId,
            Success = _taskError == null,
            FinalWork = _currentDraft,
            Error = _taskError,
            CompletedAt = Workflow.UtcNow
        };
    }

    [WorkflowSignal]
    public void UpdateDraft(string updatedDraft)
    {
        _logger.LogInformation("Draft updated");
        _currentDraft = updatedDraft;
    }

    [WorkflowSignal]
    public void CompleteTask()
    {
        _logger.LogInformation("Task marked as complete");
        _taskCompleted = true;
    }

    [WorkflowSignal]
    public void FailTask(string error)
    {
        _logger.LogWarning("Task failure signal received: {Error}", error);
        _taskError = error;
        _taskCompleted = true;
    }

    [WorkflowQuery]
    public string? GetCurrentDraft()
    {
        return _currentDraft;
    }

    [WorkflowQuery]
    public string GetStatus()
    {
        if (_taskCompleted)
        {
            return _taskError != null ? "Failed" : "Completed";
        }
        return "Awaiting Human Action";
    }
}

public record TaskWorkflowRequest
{
    public required string TaskId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public string? DraftWork { get; init; }
    public string? AssignedTo { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

public record TaskWorkflowResult
{
    public required string TaskId { get; init; }
    public required bool Success { get; init; }
    public string? FinalWork { get; init; }
    public string? Error { get; init; }
    public DateTime CompletedAt { get; init; }
}