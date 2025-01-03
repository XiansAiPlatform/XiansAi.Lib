using Temporalio.Activities;
using Microsoft.Extensions.Logging;

public class BaseAgent
{
    private readonly ILogger<BaseAgent> _logger;
    public BaseAgent()
    {
        _logger = Globals.LogFactory.CreateLogger<BaseAgent>();
    }
    public string? GetActivityId()
    {
        try {
            return ActivityExecutionContext.Current.Info.ActivityId;
        } catch (Exception e) {
            _logger.LogWarning(e, "Failed to get activity id, not running in temporal context");
            return null;
        }
    }

    public string? GetActivityType()
    {
        try {
            return ActivityExecutionContext.Current.Info.ActivityType;
        } catch (Exception e) {
            _logger.LogWarning(e, "Failed to get activity id, not running in temporal context");
            return null;
        }
    }

    public string? GetWorkflowId()
    {
        try {
            return ActivityExecutionContext.Current.Info.WorkflowId;
        } catch (Exception e) {
            _logger.LogWarning(e, "Failed to get workflow id, not running in temporal context");
            return null;
        }
    }

    public string? GetWorkflowType()
    {
        try {
            return ActivityExecutionContext.Current.Info.WorkflowType;
        } catch (Exception e) {
            _logger.LogWarning(e, "Failed to get workflow type, not running in temporal context");
            return null;
        }
    }

    public string? GetTaskQueue()
    {
        try {
            return ActivityExecutionContext.Current.Info.TaskQueue;
        } catch (Exception e) {
            _logger.LogWarning(e, "Failed to get task queue, not running in temporal context");
            return null;
        }
    }
}
