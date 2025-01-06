using Temporalio.Activities;
using Microsoft.Extensions.Logging;

namespace XiansAi.Activity;
public class ActivityBase
{
    private readonly ILogger<ActivityBase> _logger;
    private Models.Activity? _currentActivity;
    public ActivityBase()
    {
        _logger = Globals.LogFactory.CreateLogger<ActivityBase>();
    }

    public bool IsInWorkflow()
    {
        return ActivityExecutionContext.Current != null;
    }

    public void NewCurrentActivity()
    {
        _currentActivity = CreateActivity();
    }

    public virtual Models.Activity GetCurrentActivity()
    {
        if (_currentActivity != null) {
            return _currentActivity;
        } else {
            return CreateActivity();
        }
    }

    private Models.Activity CreateActivity()
    {
        try {
            return new Models.Activity {
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
