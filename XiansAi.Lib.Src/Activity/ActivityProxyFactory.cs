using XiansAi.Logging;

namespace XiansAi.Activity;

/// <summary>
/// Non-static class just for logger type reference
/// </summary>
internal class ActivityProxyLogger { }

/// <summary>
/// Provides utilities for activity proxy creation and logging
/// </summary>
internal static class ActivityProxyFactory 
{
    /// <summary>
    /// Creates a logger for activity tracking
    /// </summary>
    internal static Logger<ActivityProxyLogger> CreateLogger()
    {
        return Logger<ActivityProxyLogger>.For();
    }
    
    /// <summary>
    /// Creates a proxy instance for any interface and activity type using reflection
    /// </summary>
    /// <param name="interfaceType">The activity interface type</param>
    /// <param name="activityInstance">The activity instance</param>
    /// <returns>A proxy instance of the specified interface type</returns>
    /// <exception cref="InvalidOperationException">Thrown when proxy creation fails</exception>
    public static object CreateProxyFor(Type interfaceType, object activityInstance)
    {
        if (!interfaceType.IsInterface)
        {
            throw new InvalidOperationException($"Type parameter {interfaceType.Name} must be an interface");
        }

        var activityType = activityInstance.GetType();
        
        // Get the generic proxy type for the specified interface and activity
        var proxyType = typeof(ActivityProxy<,>).MakeGenericType(interfaceType, activityType);
        
        // Get the Create method from the proxy type
        var createMethod = proxyType.GetMethod("Create") 
            ?? throw new InvalidOperationException("Failed to find Create method on ActivityTrackerProxy");
        
        // Invoke the Create method to get the proxy instance
        var proxy = createMethod.Invoke(null, new[] { activityInstance })
            ?? throw new InvalidOperationException("Failed to create activity proxy");
            
        return proxy;
    }
} 