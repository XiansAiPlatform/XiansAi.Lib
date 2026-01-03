using Temporalio.Workflows;

namespace Xians.Lib.Workflows.Messaging;

/// <summary>
/// Centralized activity options for message processing activities.
/// Ensures consistent timeout and retry behavior across all message handling.
/// </summary>
internal static class MessageActivityOptions
{
    /// <summary>
    /// Standard options for message processing activities.
    /// Updated to handle rate limiting with longer retry intervals.
    /// </summary>
    public static ActivityOptions GetStandardOptions()
    {
        return new ActivityOptions
        {
            StartToCloseTimeout = TimeSpan.FromMinutes(10),
            RetryPolicy = new()
            {
                MaximumAttempts = 5,
                InitialInterval = TimeSpan.FromSeconds(5),
                MaximumInterval = TimeSpan.FromMinutes(3), // Allow up to 3 minutes between retries for rate limits
                BackoffCoefficient = 2
            }
        };
    }
}

