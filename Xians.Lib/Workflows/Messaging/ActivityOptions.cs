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
    /// </summary>
    public static ActivityOptions GetStandardOptions()
    {
        return new ActivityOptions
        {
            StartToCloseTimeout = TimeSpan.FromMinutes(5),
            RetryPolicy = new()
            {
                MaximumAttempts = 3,
                InitialInterval = TimeSpan.FromSeconds(2),
                MaximumInterval = TimeSpan.FromSeconds(30),
                BackoffCoefficient = 2
            }
        };
    }
}

