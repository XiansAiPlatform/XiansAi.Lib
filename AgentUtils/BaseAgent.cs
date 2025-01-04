using Temporalio.Activities;
using Microsoft.Extensions.Logging;

public class BaseAgent
{
    private readonly ILogger<BaseAgent> _logger;
    private Activity? _currentActivity;
    public BaseAgent()
    {
        _logger = Globals.LogFactory.CreateLogger<BaseAgent>();
    }

    protected bool IsInWorkflow()
    {
        return ActivityExecutionContext.Current != null;
    }

    protected void NewCurrentActivity()
    {
        _currentActivity = CreateActivity();
    }

    protected Activity? GetCurrentActivity()
    {
        if (_currentActivity != null) {
            return _currentActivity;
        } else {
            return CreateActivity();
        }
    }

    private Activity? CreateActivity()
    {
        try {
            return new Activity {
                ActivityId = ActivityExecutionContext.Current.Info.ActivityId,
                ActivityName = ActivityExecutionContext.Current.Info.ActivityType,
                StartedTime = ActivityExecutionContext.Current.Info.StartedTime,
                WorkflowId = ActivityExecutionContext.Current.Info.WorkflowId,
                WorkflowType = ActivityExecutionContext.Current.Info.WorkflowType,
                TaskQueue = ActivityExecutionContext.Current.Info.TaskQueue
            };
        } catch (Exception e) {
            _logger.LogWarning(e, "Failed to get activity id, not running in temporal context");
            throw;
        }
    }
}
