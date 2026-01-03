using Temporalio.Workflows;

namespace Xians.Lib.Workflows.Documents;

/// <summary>
/// Centralized activity options for document operations.
/// Ensures consistent timeout and retry behavior across all document handling.
/// </summary>
internal static class DocumentActivityOptions
{
    /// <summary>
    /// Standard options for document operations (save, get, query, update, delete).
    /// </summary>
    public static ActivityOptions GetStandardOptions()
    {
        return new ActivityOptions
        {
            StartToCloseTimeout = TimeSpan.FromSeconds(30),
            RetryPolicy = new()
            {
                MaximumAttempts = 3,
                InitialInterval = TimeSpan.FromSeconds(1),
                MaximumInterval = TimeSpan.FromSeconds(10),
                BackoffCoefficient = 2
            }
        };
    }
}



