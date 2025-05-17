using System.Reflection;
using System.Text.Json;
using Temporalio.Activities;
using Server;
using XiansAi.Logging;

namespace XiansAi.Activity;

/// <summary>
/// Activity tracker proxy that intercepts activity method calls to track and log them
/// </summary>
public class ActivityProxy<I, T> : DispatchProxy where T : ActivityBase, I
{
    private T? _target;
    private static readonly Logger<ActivityProxyLogger> _logger = ActivityProxyFactory.CreateLogger();

    /// <summary>
    /// Creates a proxy instance for the specified target
    /// </summary>
    /// <param name="target">The target activity instance</param>
    /// <returns>A proxy wrapping the target activity</returns>
    public static I Create(T target)
    {
        object proxy = Create<I, ActivityProxy<I, T>>()
            ?? throw new InvalidOperationException("Failed to create proxy");
        ((ActivityProxy<I, T>)proxy)._target = target;
        return (I)proxy;
    }

    /// <summary>
    /// Invokes the method with activity tracking
    /// </summary>
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method == null || _target == null)
            throw new Exception("Method not found or target is null");

        // Check if the method is an activity and if we are in a workflow
        var attribute = method.GetCustomAttribute<ActivityAttribute>();
        if (attribute == null || !_target.IsInWorkflow())
            return method.Invoke(_target, args);

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
            _logger.LogInformation($"Calling activity {activityName} with parameters {JsonSerializer.Serialize(inputs)}");

            // Call the activity
            result = method.Invoke(_target, args);
        }
        catch (TargetInvocationException ex)
        {
            _logger.LogError($"Error in activity {activityName} with parameters {JsonSerializer.Serialize(inputs)}", ex.InnerException ?? ex);
            throw;
        }

        // Handle activity result upload
        HandleActivityResultUpload(activityName, inputs, result);
        
        return result;
    }

    /// <summary>
    /// Handles the upload of activity results
    /// </summary>
    private void HandleActivityResultUpload(string activityName, Dictionary<string, object?> inputs, object? result)
    {
        if (result is not Task task)
        {
            _logger.LogInformation($"Uploading activity result: {result}");
            UploadActivityResult(activityName, inputs, result).ConfigureAwait(false);
        }
        else
        {
            task.ContinueWith(t =>
            {
                var resultProperty = t.GetType().GetProperty("Result");
                var resultValue = resultProperty?.GetValue(t);
                _logger.LogInformation($"Uploading async activity result: {resultValue}");
                UploadActivityResult(activityName, inputs, resultValue).ConfigureAwait(false);
                return t;
            });
        }
    }

    /// <summary>
    /// Uploads the activity result to MongoDB
    /// </summary>
    private async Task UploadActivityResult(string activityName, Dictionary<string, object?> inputs, object? result)
    {
        try
        {
            if (ActivityExecutionContext.Current == null) throw new Exception("ActivityExecutionContext.Current is null");

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

}
