using Temporalio.Activities;
using XiansAi.Models;
using System.Reflection;
using System.Diagnostics;
using XiansAi.Logging;

namespace XiansAi.Activity;
public class AbstractActivity
{
    private static readonly Logger<AbstractActivity> _logger = Logger<AbstractActivity>.For();
    private FlowActivityHistory? _currentActivity;
    public MethodInfo? _currentActivityMethod { get; internal set; }
    public Type? _currentActivityInterfaceType { get; internal set; }

    public AbstractActivity()
    {
    }

    public bool IsInWorkflow()
    {
        return ActivityExecutionContext.HasCurrent;
    }

    public MethodInfo? CurrentActivityMethod
    {
        get
        {
            if (!IsInWorkflow() || _currentActivityMethod == null)
            {
                // Iterate through the stack frames to find a method in a class derived from ActivityBase
                var stackTrace = new StackTrace();
                for (int i = 1; i < stackTrace.FrameCount; i++)
                {
                    var method = stackTrace.GetFrame(i)?.GetMethod();
                    if (method != null && typeof(ActivityBase).IsAssignableFrom(method.DeclaringType))
                    {
                        _currentActivityMethod = (MethodInfo?)method;
                        break;
                    }
                }
            }
            return _currentActivityMethod;
        }
        internal set
        {
            _currentActivityMethod = value;
        }
    }
    public Type? CurrentActivityClass { get; internal set; }
    //public Type? CurrentActivityInterfaceType { get; internal set; }
    public void NewCurrentActivity()
    {
        try
        {
            _currentActivity = CreateActivity();
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to create new activity", ex);
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
        _logger.LogInformation(message);
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
        _logger.LogError(message, exception);
    }

    public virtual FlowActivityHistory? GetCurrentActivity()
    {
        if (!IsInWorkflow())
        {
            _logger.LogWarning("Not in workflow, skipping activity retrieval");
            return null;
        }
        if (_currentActivity != null)
        {
            return _currentActivity;
        }
        else
        {
            return CreateActivity();
        }
    }

    protected FlowActivityHistory CreateActivity()
    {
        if (ActivityExecutionContext.Current == null)
        {
            throw new InvalidOperationException("Cannot create activity outside of workflow context");
        }

        try
        {
            var context = ActivityExecutionContext.Current;
            return new FlowActivityHistory
            {
                ActivityId = context.Info.ActivityId ?? throw new InvalidOperationException("ActivityId is null"),
                ActivityName = context.Info.ActivityType ?? throw new InvalidOperationException("ActivityType is null"),
                StartedTime = context.Info.StartedTime,
                Attempt = context.Info.Attempt,
                WorkflowNamespace = context.Info.WorkflowNamespace ?? throw new InvalidOperationException("WorkflowNamespace is null"),
                WorkflowId = context.Info.WorkflowId ?? throw new InvalidOperationException("WorkflowId is null"),
                WorkflowType = context.Info.WorkflowType ?? throw new InvalidOperationException("WorkflowType is null"),
                TaskQueue = context.Info.TaskQueue ?? throw new InvalidOperationException("TaskQueue is null"),
                WorkflowRunId = context.Info.WorkflowRunId ?? throw new InvalidOperationException("WorkflowRunId is null")
            };
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to create activity", e);
            throw;
        }
    }

    public Type? CurrentActivityInterfaceType
    {
        get
        {
            if (!IsInWorkflow() || _currentActivityInterfaceType == null)
            {
                // Iterate through the stack frames to find an interface in a class derived from ActivityBase
                var stackTrace = new StackTrace();
                for (int i = 1; i < stackTrace.FrameCount; i++)
                {
                    var method = stackTrace.GetFrame(i)?.GetMethod();
                    if (method != null && typeof(ActivityBase).IsAssignableFrom(method.DeclaringType))
                    {
                        // Get the interfaces implemented by the declaring type of the method
                        var interfaces = method.DeclaringType?.GetInterfaces();
                        if (interfaces != null && interfaces.Length > 0)
                        {
                            _currentActivityInterfaceType = interfaces[0]; // Assuming the first interface is the desired one
                            break;
                        }
                    }
                }
            }
            return _currentActivityInterfaceType;
        }
        internal set
        {
            _currentActivityInterfaceType = value;
        }
    }
}
