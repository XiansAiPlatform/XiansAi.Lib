using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Xians.Lib.Common;
using Xians.Lib.Workflows.Models;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Common.MultiTenancy;
using Xians.Lib.Workflows.Messaging.Models;

namespace Xians.Lib.Workflows.Messaging;

/// <summary>
/// Helper class for sending message responses via Temporal activities.
/// </summary>
internal static class MessageResponseHelper
{
    /// <summary>
    /// Sends a simple text message via activity.
    /// </summary>
    public static async Task SendSimpleMessageAsync(
        string participantId,
        string text,
        string requestId,
        string scope,
        string? threadId,
        string? authorization,
        string hint,
        string tenantId,
        string workflowId,
        string workflowType)
    {
        var request = new SendMessageRequest
        {
            ParticipantId = participantId,
            WorkflowId = workflowId,
            WorkflowType = workflowType,
            Text = text,
            Data = null,
            RequestId = requestId,
            Scope = scope,
            ThreadId = threadId,
            Authorization = authorization,
            Hint = hint,
            Origin = null,
            Type = "Chat",
            TenantId = tenantId
        };

        await Workflow.ExecuteActivityAsync(
            (MessageActivities act) => act.SendMessageAsync(request),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(30),
                RetryPolicy = new()
                {
                    MaximumAttempts = 3,
                    InitialInterval = TimeSpan.FromSeconds(1),
                    MaximumInterval = TimeSpan.FromSeconds(10),
                    BackoffCoefficient = 2
                }
            });
    }

    /// <summary>
    /// Sends an error response back to the user via activity.
    /// </summary>
    public static async Task SendErrorResponseAsync(
        InboundMessage message,
        string errorMessage,
        string workflowTenantId,
        string workflowId,
        string workflowType)
    {
        await SendSimpleMessageAsync(
            message.Payload.ParticipantId,
            $"Error: {errorMessage}",
            message.Payload.RequestId,
            message.Payload.Scope,
            message.Payload.ThreadId,
            message.Payload.Authorization,
            message.Payload.Hint,
            workflowTenantId,
            workflowId,
            workflowType);
    }

    /// <summary>
    /// Sends an error response back to the user via activity.
    /// Extracts tenant ID from workflow context.
    /// </summary>
    public static async Task SendErrorResponseAsync(
        InboundMessage message,
        string errorMessage,
        ILogger logger)
    {
        // Extract tenant ID from WorkflowId using centralized utility
        var workflowId = Workflow.Info.WorkflowId;
        string workflowTenantId;
        try
        {
            workflowTenantId = TenantContext.ExtractTenantId(workflowId);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex,
                "Failed to extract tenant ID for error response. WorkflowId={WorkflowId}. Cannot send error response without valid tenant context.",
                workflowId);
            // Do not send error response if we can't determine tenant - security risk
            return;
        }

        await SendErrorResponseAsync(
            message,
            errorMessage,
            workflowTenantId,
            workflowId,
            Workflow.Info.WorkflowType);
    }
}
