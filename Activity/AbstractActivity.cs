using Temporalio.Activities;
using Microsoft.Extensions.Logging;
using XiansAi.Models;
using System.Reflection;

namespace XiansAi.Activity;
public class AbstractActivity
{
    private readonly ILogger<AbstractActivity> _logger;
    private FlowActivity? _currentActivity;

    public AbstractActivity()
    {
        _logger = Globals.LogFactory.CreateLogger<AbstractActivity>();
    }

    public bool IsInWorkflow()
    {
        try 
        {
            return ActivityExecutionContext.Current != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking workflow context");
            return false;
        }
    }

    public MethodInfo? CurrentActivityMethod { get; internal set; }
    public Type? CurrentActivityClass { get; internal set; }
    public Type? CurrentActivityInterfaceType { get; internal set; }
    public void NewCurrentActivity()
    {
        try 
        {
            _currentActivity = CreateActivity();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create new activity");
            throw;
        }
    }

    public void LogInfo(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            _logger.LogWarning("Attempted to log empty or null message");
            return;
        }
        ActivityLogger.LogInformation(message);
    }

    public void LogError(string message, Exception exception)
    {
        if (exception == null)
        {
            throw new ArgumentNullException(nameof(exception));
        }
        if (string.IsNullOrEmpty(message))
        {
            message = "An error occurred";
        }
        ActivityLogger.LogError(message, exception);
    }

    public virtual FlowActivity? GetCurrentActivity()
    {
        if (!IsInWorkflow()) {
            _logger.LogWarning("Not in workflow, skipping activity retrieval");
            return null;
        }
        if (_currentActivity != null) {
            return _currentActivity;
        } else {
            return CreateActivity();
        }
    }

    private FlowActivity CreateActivity()
    {
        if (ActivityExecutionContext.Current == null)
        {
            throw new InvalidOperationException("Cannot create activity outside of workflow context");
        }

        try 
        {
            var context = ActivityExecutionContext.Current;
            return new FlowActivity 
            {
                ActivityId = context.Info.ActivityId ?? throw new InvalidOperationException("ActivityId is null"),
                ActivityName = context.Info.ActivityType ?? throw new InvalidOperationException("ActivityType is null"),
                StartedTime = context.Info.StartedTime,
                WorkflowId = context.Info.WorkflowId ?? throw new InvalidOperationException("WorkflowId is null"),
                WorkflowType = context.Info.WorkflowType ?? throw new InvalidOperationException("WorkflowType is null"),
                TaskQueue = context.Info.TaskQueue ?? throw new InvalidOperationException("TaskQueue is null")
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to create activity");
            throw;
        }
    }
}
