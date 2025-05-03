using System.Reflection;
using System.Text.Json;
using Temporalio.Activities;
using Server;
using XiansAi.Logging;

namespace XiansAi.Activity;

// Simple class for less verbose logging
internal class ActivityProxy { }

class ActivityTrackerProxy<I, T> : DispatchProxy where T : ActivityBase, I
{
    private T? _target;
    
    // Use a simpler type parameter for the logger
    private static readonly Logger<ActivityProxy> _logger = Logger<ActivityProxy>.For();

    public static I Create(T target)
    {
        object proxy = Create<I, ActivityTrackerProxy<I, T>>()
            ?? throw new InvalidOperationException("Failed to create proxy");
        ((ActivityTrackerProxy<I, T>)proxy)._target = target;
        return (I)proxy;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method == null || _target == null)
            throw new Exception("Method not found or target is null");

        // Check if the method is an activity and if we are in a workflow
        var attribute = method.GetCustomAttribute<ActivityAttribute>();
        if (attribute == null || !_target.IsInWorkflow())
            return method.Invoke(_target, args);

        // Clear the explicit instance of the agent context, if it exists
        AgentContext.ClearExplicitInstance();
        // Create a new activity 
        _target.NewCurrentActivity();
        _target.CurrentActivityMethod = method;
        _target.CurrentActivityClass = _target.GetType();
        _target.CurrentActivityInterfaceType = typeof(I);

        // Get the activity name
        var activityName = attribute.Name ?? method.Name;

        // Get the parameters and their values
        var inputs = method.GetParameters()
                           .Select((param, index) => new { param.Name, Value = args?[index] })
                           .ToDictionary(p => p.Name!, p => p.Value);

        object? result = null;

        try
        {
            // Call the activity
            result = method.Invoke(_target, args);
        }
        catch (TargetInvocationException ex)
        {
            _logger.LogError($"Error in activity {activityName} with parameters {JsonSerializer.Serialize(inputs)}", ex.InnerException ?? ex);
            throw;
        }

        // Handle the result
        if (result is not Task task)
        {
            _logger.LogInformation($"Activity result is not a task, uploading result: {result}");
            UploadActivityResult(activityName, inputs, result).ConfigureAwait(false);
        }
        else
        {
            task.ContinueWith(t =>
            {
                var resultProperty = t.GetType().GetProperty("Result");
                var resultValue = resultProperty?.GetValue(t);
                _logger.LogInformation($"Activity result is a task, uploading result: {resultValue}");
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
            ValidateActivityName(activityName);

            var activity = _target?.GetCurrentActivity();
            if (activity != null)
            {
                // Set the activity properties
                activity.Inputs = inputs;
                activity.Result = result;
                activity.EndedTime = DateTime.UtcNow;

                // Upload to server
                await new ActivityUploader().UploadActivity(activity);

                _logger.LogInformation($"Activity Completed: {activityName}");
            }
            else
            {
                _logger.LogInformation("No current activity found, skipping activity result upload, ignore this warning if you are not running in a flow");
            }
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to upload activity result", e);
            throw;
        }
    }

    private void ValidateActivityName(string activityName)
    {
        string normalizedActivityName = activityName.EndsWith("Async") ? activityName[..^5] : activityName;
        string normalizedActivityType = ActivityExecutionContext.Current.Info.ActivityType.EndsWith("Async")
            ? ActivityExecutionContext.Current.Info.ActivityType[..^5]
            : ActivityExecutionContext.Current.Info.ActivityType;

        if (!normalizedActivityName.Equals(normalizedActivityType))
        {
            throw new Exception($"Activity name does not match {normalizedActivityName} != {normalizedActivityType}");
        }
    }
}
