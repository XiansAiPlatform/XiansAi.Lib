using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common.MultiTenancy;
using Xians.Lib.Workflows.Messaging;
using Xians.Lib.Workflows.Messaging.Models;

namespace Xians.Lib.Agents.Messaging;

/// <summary>
/// Static helper for sending agent-initiated messages to users.
/// Use this when the agent needs to proactively send messages without a user message context to reply to.
/// 
/// For replying to user messages, use <see cref="UserMessageContext.ReplyAsync"/> instead.
/// </summary>
/// <example>
/// // Send a proactive notification to a user
/// await UserMessaging.SendChatAsync("user-123", "Your order has shipped!");
/// 
/// // Send data to a user
/// await UserMessaging.SendDataAsync("user-123", "Order update", new { Status = "Shipped" });
/// 
/// // Send with custom scope
/// await UserMessaging.SendChatAsync("user-123", "Hello!", scope: "notifications");
/// </example>
public static class UserMessaging
{
    /// <summary>
    /// Sends a chat message to a participant using the current workflow context.
    /// </summary>
    /// <param name="participantId">The ID of the participant (user) to send the message to.</param>
    /// <param name="text">The chat message content.</param>
    /// <param name="data">Optional data object to include with the message.</param>
    /// <param name="scope">Optional scope for the message (e.g., "notifications", "alerts").</param>
    /// <param name="hint">Optional hint for message processing.</param>
    /// <returns>A task representing the async operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not in a workflow or activity context.</exception>
    public static async Task SendChatAsync(
        string participantId,
        string text,
        object? data = null,
        string? scope = null,
        string? hint = null)
    {
        await SendMessageAsync(participantId, text, data, scope, hint, "chat");
    }

    /// <summary>
    /// Sends a data message to a participant using the current workflow context.
    /// Data messages are typically used for structured data that may be processed differently than chat messages.
    /// </summary>
    /// <param name="participantId">The ID of the participant (user) to send the data to.</param>
    /// <param name="text">The message content/description.</param>
    /// <param name="data">The data object to send.</param>
    /// <param name="scope">Optional scope for the message.</param>
    /// <param name="hint">Optional hint for message processing.</param>
    /// <returns>A task representing the async operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not in a workflow or activity context.</exception>
    public static async Task SendDataAsync(
        string participantId,
        string text,
        object data,
        string? scope = null,
        string? hint = null)
    {
        await SendMessageAsync(participantId, text, data, scope, hint, "data");
    }

    /// <summary>
    /// Sends a chat message to a participant while impersonating a different workflow as the sender.
    /// Useful when one workflow needs to send messages on behalf of another.
    /// </summary>
    /// <param name="workflowType">The workflow type to impersonate as the sender (format: "AgentName:WorkflowName").</param>
    /// <param name="participantId">The ID of the participant (user) to send the message to.</param>
    /// <param name="text">The chat message content.</param>
    /// <param name="data">Optional data object to include with the message.</param>
    /// <param name="scope">Optional scope for the message.</param>
    /// <param name="hint">Optional hint for message processing.</param>
    /// <returns>A task representing the async operation.</returns>
    public static async Task SendChatAsAsync(
        string workflowType,
        string participantId,
        string text,
        object? data = null,
        string? scope = null,
        string? hint = null)
    {
        await SendMessageAsAsync(workflowType, participantId, text, data, scope, hint, "chat");
    }

    /// <summary>
    /// Sends a data message to a participant while impersonating a different workflow as the sender.
    /// </summary>
    /// <param name="workflowType">The workflow type to impersonate as the sender (format: "AgentName:WorkflowName").</param>
    /// <param name="participantId">The ID of the participant (user) to send the data to.</param>
    /// <param name="text">The message content/description.</param>
    /// <param name="data">The data object to send.</param>
    /// <param name="scope">Optional scope for the message.</param>
    /// <param name="hint">Optional hint for message processing.</param>
    /// <returns>A task representing the async operation.</returns>
    public static async Task SendDataAsAsync(
        string workflowType,
        string participantId,
        string text,
        object data,
        string? scope = null,
        string? hint = null)
    {
        await SendMessageAsAsync(workflowType, participantId, text, data, scope, hint, "data");
    }

    /// <summary>
    /// Internal method for sending messages as a different workflow.
    /// </summary>
    private static async Task SendMessageAsAsync(
        string targetWorkflowType,
        string participantId,
        string text,
        object? data,
        string? scope,
        string? hint,
        string messageType)
    {
        if (string.IsNullOrWhiteSpace(targetWorkflowType))
        {
            throw new ArgumentException("Workflow type cannot be null or empty.", nameof(targetWorkflowType));
        }

        if (string.IsNullOrWhiteSpace(participantId))
        {
            throw new ArgumentException("Participant ID cannot be null or empty.", nameof(participantId));
        }

        // Get tenant from current context (the impersonated workflow still uses current tenant)
        var tenantId = XiansContext.TenantId;
        
        // Generate a workflow ID for the impersonated workflow
        var workflowId = TenantContext.BuildWorkflowId(targetWorkflowType, tenantId, participantId, scope);

        await SendMessageInternalAsync(
            workflowId, targetWorkflowType, tenantId,
            participantId, text, data, scope, hint, messageType);
    }

    /// <summary>
    /// Internal method that handles sending both chat and data messages.
    /// Uses Temporal activities for workflow context, or direct HTTP for activity context.
    /// </summary>
    private static async Task SendMessageAsync(
        string participantId,
        string text,
        object? data,
        string? scope,
        string? hint,
        string messageType)
    {
        if (string.IsNullOrWhiteSpace(participantId))
        {
            throw new ArgumentException("Participant ID cannot be null or empty.", nameof(participantId));
        }

        var workflowId = XiansContext.WorkflowId;
        var workflowType = XiansContext.WorkflowType;
        var tenantId = XiansContext.TenantId;
        
        await SendMessageInternalAsync(
            workflowId, workflowType, tenantId,
            participantId, text, data, scope, hint, messageType);
    }

    /// <summary>
    /// Core message sending implementation shared by all public methods.
    /// </summary>
    private static async Task SendMessageInternalAsync(
        string workflowId,
        string workflowType,
        string tenantId,
        string participantId,
        string text,
        object? data,
        string? scope,
        string? hint,
        string messageType)
    {

        var request = new SendMessageRequest
        {
            ParticipantId = participantId,
            WorkflowId = workflowId,
            WorkflowType = workflowType,
            Text = text,
            Data = data,
            RequestId = Guid.NewGuid().ToString(),
            Scope = scope,
            Hint = hint,
            Origin = "agent-initiated",
            Type = messageType,
            TenantId = tenantId
        };

        if (Workflow.InWorkflow)
        {
            // Execute via Temporal activity for proper determinism and retry handling
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
        else if (XiansContext.InActivity)
        {
            // Direct HTTP call when in activity context
            var agent = XiansContext.CurrentAgent;
            if (agent.HttpService == null)
            {
                throw new InvalidOperationException(
                    "HTTP service not available for message operations. Ensure the agent is properly configured.");
            }

            var logger = Common.Infrastructure.LoggerFactory.CreateLogger<MessageService>();
            var messageService = new MessageService(agent.HttpService.Client, logger);

            await messageService.SendAsync(
                request.ParticipantId,
                request.WorkflowId,
                request.WorkflowType,
                request.RequestId,
                request.Scope ?? string.Empty,
                request.Text ?? string.Empty,
                request.Data,
                request.TenantId,
                authorization: null,
                threadId: null,
                request.Hint ?? string.Empty,
                request.Origin,
                request.Type);
        }
        else
        {
            throw new InvalidOperationException(
                "UserMessaging can only be used within a Temporal workflow or activity context.");
        }
    }
}

