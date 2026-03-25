using Temporalio.Workflows;
using Xians.Lib.Agents.Workflows.Models;

namespace Xians.Lib.Temporal.Workflows.Messaging;

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
    public static ActivityOptions GetStandardOptions(string? workflowType = null)
    {
        var resolved = ResolveExecutionOptions(workflowType);
        return new ActivityOptions
        {
            StartToCloseTimeout = resolved.StartToCloseTimeout,
            RetryPolicy = new()
            {
                MaximumAttempts = resolved.Retry.MaximumAttempts,
                InitialInterval = resolved.Retry.InitialInterval,
                MaximumInterval = resolved.Retry.MaximumInterval, // Allow longer intervals for rate limits
                BackoffCoefficient = resolved.Retry.BackoffCoefficient
            }
        };
    }

    private static MessageActivityExecutionOptions ResolveExecutionOptions(string? workflowType)
    {
        if (!string.IsNullOrWhiteSpace(workflowType) &&
            BuiltinWorkflow.TryGetWorkflowOptions(workflowType, out var options))
        {
            return options.MessageActivityExecution;
        }

        return new MessageActivityExecutionOptions();
    }
}

