namespace Xians.Lib.Common.Exceptions;

/// <summary>
/// Exception thrown when a Temporal activity execution fails.
/// Provides context about which activity failed and why.
/// </summary>
public class ActivityExecutionException : XiansException
{
    /// <summary>
    /// Gets the name of the activity that failed.
    /// </summary>
    public string? ActivityName { get; }

    /// <summary>
    /// Gets the tenant ID context when the activity failed.
    /// </summary>
    public string? TenantId { get; }

    public ActivityExecutionException(string message) : base(message) { }

    public ActivityExecutionException(string message, Exception innerException) 
        : base(message, innerException) { }

    public ActivityExecutionException(
        string message, 
        string activityName, 
        string? tenantId = null, 
        Exception? innerException = null) 
        : base(message, innerException)
    {
        ActivityName = activityName;
        TenantId = tenantId;
    }
}
