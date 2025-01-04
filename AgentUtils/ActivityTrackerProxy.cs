using System.Reflection;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using System.Text.Json;

public class ActivityTrackerProxy<I, T> : DispatchProxy where T : BaseAgent, I
{
    private T? _target;
    private static readonly ILogger<ActivityTrackerProxy<I, T>> _logger = Globals.LogFactory.CreateLogger<ActivityTrackerProxy<I, T>>();

    public static I Create(T target)
    {
        object proxy = Create<I, ActivityTrackerProxy<I, T>>() 
            ?? throw new InvalidOperationException("Failed to create proxy");
        ((ActivityTrackerProxy<I, T>)proxy)._target = target;
        return (I)proxy;
    }


    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method == null) throw new Exception("Method not found");

        // if the method is not an activity, just call it
        var attribute = method?.GetCustomAttribute<ActivityAttribute>();
        if (attribute == null) return method!.Invoke(_target, args)!;

        //method.

        // get the activity name
        var activityName = attribute.Name ?? method!.Name;

        // get the parameters
        var parameters = method!.GetParameters();
        var inputs = new Dictionary<string, object?>();
        for (int i = 0; i < parameters.Length; i++)
        {
            inputs[parameters[i].Name!] = args?[i];
        }

        // call the activity
        var result = method.Invoke(_target, args);

        if (result is not Task task)
        {
            UploadActivityResult(activityName, inputs, result).ConfigureAwait(false);
        } else {
            task.ContinueWith(t =>
            {
                var resultProperty = t.GetType().GetProperty("Result");
                var resultValue = resultProperty?.GetValue(t);
                UploadActivityResult(activityName, inputs, resultValue).ConfigureAwait(false);
                return t;
            });
        }


        return result;
    }

    private async Task UploadActivityResult(string activityName, Dictionary<string, object?> inputs, object? result)
    {
        try {
            if (ActivityExecutionContext.Current == null) throw new Exception("ActivityExecutionContext.Current is null");
            if (!activityName.Equals(ActivityExecutionContext.Current.Info.ActivityType)) throw new Exception("Activity name does not match");
            
            var activity = new Activity {
                ActivityId = ActivityExecutionContext.Current.Info.ActivityId,
                ActivityName = ActivityExecutionContext.Current.Info.ActivityType,
                StartedTime = ActivityExecutionContext.Current.Info.StartedTime,
                WorkflowId = ActivityExecutionContext.Current.Info.WorkflowId,
                WorkflowType = ActivityExecutionContext.Current.Info.WorkflowType,
                TaskQueue = ActivityExecutionContext.Current.Info.TaskQueue,
                Inputs = inputs,
                Result = result,
                EndedTime = DateTime.UtcNow
            };
            await Task.Delay(100);

            var json = JsonSerializer.Serialize(activity);

            _logger.LogInformation($"******* Activity Completed: {activityName} - {json}");
        } catch (Exception e) {
            _logger.LogError(e, "Failed to upload activity result");
            throw;
        }
    }

}
