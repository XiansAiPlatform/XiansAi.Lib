using Temporalio.Activities;
using Microsoft.Extensions.Logging;

public class BaseAgent
{
    private readonly ILogger<BaseAgent> _logger;
    public BaseAgent()
    {
        _logger = Globals.LogFactory.CreateLogger<BaseAgent>();
    }
    public Activity? GetActivity()
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
            return null;
        }
    }


}
