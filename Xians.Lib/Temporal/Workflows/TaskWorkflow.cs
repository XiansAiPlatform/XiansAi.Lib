using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Xians.Lib.Agents.Tasks.Models;

namespace Xians.Lib.Temporal.Workflows;

public class TaskWorkflow
{
    private static readonly string[] DefaultActions = ["approve", "reject"];
    
    private readonly ILogger<TaskWorkflow> _logger;
    private bool _isCompleted;
    private bool _timedOut;
    private string? _initialWork;
    private string? _finalWork;
    private TaskWorkflowRequest? _request;
    private Dictionary<string, object>? _metadata;
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
        _metadata = CloneMetadata(request.Metadata);
        _availableActions = request.Actions is { Length: > 0 } ? request.Actions : DefaultActions;
        _initialWork = request.DraftWork;
        _finalWork = request.DraftWork;

        // Start timeout timer if specified
        if (request.Timeout.HasValue)
        {
            _ = Workflow.RunTaskAsync(async () =>
            {
                await Workflow.DelayAsync(request.Timeout.Value);
                if (!_isCompleted)
                {
                    _timedOut = true;
                    _performedAction = null;
                    _actionComment = null;
                }
            });
        }

        // Wait for either completion or timeout
        await Workflow.WaitConditionAsync(() => _isCompleted || _timedOut);

        return new TaskWorkflowResult
        {
            InitialWork = _initialWork,
            FinalWork = _finalWork,
            PerformedAction = _performedAction,
            Comment = _actionComment,
            CompletedAt = Workflow.UtcNow,
            TimedOut = _timedOut,
            Completed = _isCompleted,
            Metadata = _metadata
        };
    }

    [WorkflowSignal]
    public Task UpdateDraft(string updatedDraft)
    {
        _finalWork = updatedDraft;
        return Task.CompletedTask;
    }

    [WorkflowSignal]
    public Task PerformAction(TaskActionRequest actionRequest)
    {
        _performedAction = actionRequest.Action;
        _actionComment = actionRequest.Comment;
        MergeMetadata(actionRequest.Metadata);
        _isCompleted = true;
        
        return Task.CompletedTask;
    }

    [WorkflowQuery]
    public TaskInfo GetTaskInfo()
    {
        return new TaskInfo
        {
            Title = _request?.Title ?? string.Empty,
            Description = _request?.Description ?? string.Empty,
            InitialWork = _initialWork,
            FinalWork = _finalWork,
            IsCompleted = _isCompleted,
            ParticipantId = _request?.ParticipantId ?? string.Empty,
            Metadata = _metadata,
            AvailableActions = _availableActions,
            PerformedAction = _performedAction,
            Comment = _actionComment,
            TimedOut = _timedOut
        };
    }

    private static Dictionary<string, object>? CloneMetadata(Dictionary<string, object>? metadata)
    {
        if (metadata == null || metadata.Count == 0)
        {
            return null;
        }

        return new Dictionary<string, object>(metadata);
    }

    private void MergeMetadata(Dictionary<string, object>? metadata)
    {
        if (metadata == null || metadata.Count == 0)
        {
            return;
        }

        _metadata ??= new Dictionary<string, object>();

        foreach (var entry in metadata)
        {
            _metadata[entry.Key] = entry.Value;
        }
    }
}
