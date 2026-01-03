using Temporalio.Workflows;

namespace Xians.Lib.Workflows.Scheduling;

/// <summary>
/// Centralized activity options for scheduling operations.
/// Ensures consistent timeout and retry behavior across all scheduling activities.
/// </summary>
internal static class ScheduleActivityOptions
{
    /// <summary>
    /// Standard options for schedule creation and management activities.
    /// </summary>
    public static ActivityOptions GetStandardOptions()
    {
        return new ActivityOptions
        {
            StartToCloseTimeout = TimeSpan.FromMinutes(2),
            RetryPolicy = new()
            {
                MaximumAttempts = 3,
                InitialInterval = TimeSpan.FromSeconds(5),
                BackoffCoefficient = 2
            }
        };
    }

    /// <summary>
    /// Options for quick schedule existence checks.
    /// </summary>
    public static ActivityOptions GetQuickCheckOptions()
    {
        return new ActivityOptions
        {
            StartToCloseTimeout = TimeSpan.FromSeconds(30)
        };
    }
}



