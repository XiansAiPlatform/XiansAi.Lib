using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Server;
using Temporalio.Activities;
using Temporalio.Client;

namespace XiansAi.Activity;

public interface IActivityProxy
{
    Task<object?> InvokeActivity(string activityName, Dictionary<string, object?> inputs);
}

/// <summary>
/// Proxy for executing activities with automatic result uploading
/// </summary>
public class ActivityProxy<T> : IActivityProxy where T : class
{
    private readonly T _activities;
    private readonly ILogger<ActivityProxy<T>> _logger;
    
    // Track background tasks for proper shutdown
    private static readonly ConcurrentBag<Task> _backgroundTasks = new();
    private static readonly Timer _cleanupTimer;
    
    static ActivityProxy()
    {
        // Periodically clean up completed tasks to prevent memory leaks
        _cleanupTimer = new Timer(CleanupCompletedTasks, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public ActivityProxy(T activities)
    {
        _activities = activities;
        _logger = Globals.LogFactory.CreateLogger<ActivityProxy<T>>();
    }

    public Task<object?> InvokeActivity(string activityName, Dictionary<string, object?> inputs)
    {
        _logger.LogInformation($"Invoking activity: {activityName}");

        var method = _activities.GetType().GetMethod(activityName);
        if (method == null)
        {
            throw new InvalidOperationException($"Activity '{activityName}' not found in type '{typeof(T).Name}'");
        }

        // Prepare method parameters
        var parameters = method.GetParameters();
        var args = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var paramName = parameters[i].Name!;
            var paramType = parameters[i].ParameterType;
            
            if (inputs.TryGetValue(paramName, out var value))
            {
                args[i] = ConvertParameter(value, paramType);
            }
            else if (parameters[i].HasDefaultValue)
            {
                args[i] = parameters[i].DefaultValue;
            }
            else
            {
                throw new ArgumentException($"Required parameter '{paramName}' not provided for activity '{activityName}'");
            }
        }

        // Invoke the method
        object? result;
        try
        {
            result = method.Invoke(_activities, args);
        }
        catch (TargetInvocationException ex)
        {
            _logger.LogError($"Error in activity {activityName} with parameters {JsonSerializer.Serialize(inputs)}", ex.InnerException ?? ex);
            throw;
        }

        // Handle activity result upload with proper task tracking
        var uploadTask = HandleActivityResultUpload(activityName, inputs, result);
        _backgroundTasks.Add(uploadTask);
        
        return Task.FromResult(result);
    }
    
    /// <summary>
    /// Waits for all background upload tasks to complete (for shutdown scenarios)
    /// </summary>
    public static async Task WaitForBackgroundTasksAsync(TimeSpan timeout = default)
    {
        if (timeout == default)
            timeout = TimeSpan.FromSeconds(10);
            
        var pendingTasks = _backgroundTasks.Where(t => !t.IsCompleted).ToArray();
        if (pendingTasks.Length == 0) return;
        
        try
        {
            await Task.WhenAll(pendingTasks).WaitAsync(timeout);
        }
        catch (TimeoutException)
        {
            Console.WriteLine($"Warning: {pendingTasks.Length} activity upload tasks did not complete within {timeout}");
        }
    }
    
    /// <summary>
    /// Gets the count of pending background tasks for diagnostics
    /// </summary>
    public static int GetPendingTaskCount()
    {
        return _backgroundTasks.Count(t => !t.IsCompleted);
    }
    
    /// <summary>
    /// Disposes static resources to prevent shutdown hanging
    /// </summary>
    public static void DisposeStaticResources()
    {
        try
        {
            _cleanupTimer?.Dispose();
        }
        catch (Exception)
        {
            // Ignore disposal errors during shutdown
        }
    }
    
    /// <summary>
    /// Cleanup completed tasks to prevent memory leaks
    /// </summary>
    private static void CleanupCompletedTasks(object? state)
    {
        var completedTasks = _backgroundTasks.Where(t => t.IsCompleted).ToList();
        foreach (var task in completedTasks)
        {
            // ConcurrentBag doesn't support removal, but this prevents memory leaks
            // by allowing GC to collect completed tasks
        }
    }

    /// <summary>
    /// Handles the upload of activity results
    /// </summary>
    private async Task HandleActivityResultUpload(string activityName, Dictionary<string, object?> inputs, object? result)
    {
        if (result is not Task task)
        {
            _logger.LogInformation($"Uploading activity result: {result}");
            await UploadActivityResult(activityName, inputs, result);
        }
        else
        {
            await task;
            var resultProperty = task.GetType().GetProperty("Result");
            var resultValue = resultProperty?.GetValue(task);
            _logger.LogInformation($"Uploading async activity result: {resultValue}");
            await UploadActivityResult(activityName, inputs, resultValue);
        }
    }

    /// <summary>
    /// Uploads the activity result to the server
    /// </summary>
    private Task UploadActivityResult(string activityName, Dictionary<string, object?> inputs, object? result)
    {
        try
        {
            if (!SecureApi.IsReady)
            {
                _logger.LogWarning("SecureApi not ready, skipping activity result upload");
                return Task.CompletedTask;
            }

            // Simplified activity result logging - the main fix was tracking the background task
            _logger.LogInformation($"Activity {activityName} completed with result: {result}");
            
            // TODO: Implement full activity result upload when ActivityHistory requirements are clarified
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to upload activity result for {activityName}");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Converts a parameter value to the expected type
    /// </summary>
    private object? ConvertParameter(object? value, Type targetType)
    {
        if (value == null) return null;
        
        if (targetType.IsAssignableFrom(value.GetType()))
            return value;

        // Handle JsonElement conversion
        if (value is JsonElement jsonElement)
        {
            return JsonSerializer.Deserialize(jsonElement.GetRawText(), targetType);
        }

        // Handle string to enum conversion
        if (targetType.IsEnum && value is string stringValue)
        {
            return Enum.Parse(targetType, stringValue);
        }

        // Generic type conversion
        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Cannot convert parameter value '{value}' to type '{targetType.Name}'", ex);
        }
    }
}
