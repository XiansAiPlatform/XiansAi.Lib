using System.Reflection;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using System.Text.Json;
using XiansAi.Server;

namespace XiansAi.Activity;

public class ActivityTrackerProxy<I, T> : DispatchProxy where T : ActivityBase, I
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
        if (method == null || _target == null) throw new Exception("Method not found or target is null");

        // if the method is not an activity, or we are not in a workflow, just call it
        var attribute = method?.GetCustomAttribute<ActivityAttribute>();
        if (attribute == null || _target.IsInWorkflow() == false) return method!.Invoke(_target, args)!;

        //Create a new activity on the BaseAgent
        _target.NewCurrentActivity();

        // get the activity name
        var activityName = attribute.Name ?? method!.Name;

        // get the parameters
        var parameters = method!.GetParameters();
        var inputs = new Dictionary<string, object?>();
        for (int i = 0; i < parameters.Length; i++)
        {
            inputs[parameters[i].Name!] = args?[i];
        }

        object? result = null;

        try
        {
            // call the activity
            result = method.Invoke(_target, args);
        }
        catch (TargetInvocationException ex)
        {
            // exception occurred in the activity
            ActivityLogger.LogError($"Error in activity {activityName}", ex.InnerException?? ex);
            throw;
        }
        if (result is not Task task)
        {
            UploadActivityResult(activityName, inputs, result).ConfigureAwait(false);
        }
        else
        {
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
        try
        {
            if (ActivityExecutionContext.Current == null) throw new Exception("ActivityExecutionContext.Current is null");
            if (!activityName.Equals(ActivityExecutionContext.Current.Info.ActivityType)) throw new Exception("Activity name does not match");

            var activity = _target?.GetCurrentActivity();
            if (activity != null)
            {
                // Set the activity properties
                activity.Inputs = inputs;
                activity.Result = result;
                activity.EndedTime = DateTime.UtcNow;

                // Upload to server
                await new ActivityUploader().UploadActivity(activity);

                _logger.LogInformation($"Activity Completed: {activityName} - {JsonSerializer.Serialize(activity)}");
            }
            else
            {
                _logger.LogWarning("No current activity found, skipping activity result upload, ignore this warning if you are not running in a flow");
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to upload activity result");
            throw;
        }
    }

}
