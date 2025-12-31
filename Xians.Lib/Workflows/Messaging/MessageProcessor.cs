using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Temporalio.Exceptions;
using Temporalio.Workflows;
using Xians.Lib.Common;
using Xians.Lib.Workflows.Models;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Common.MultiTenancy;
using Xians.Lib.Workflows.Messaging.Models;

namespace Xians.Lib.Workflows.Messaging;

/// <summary>
/// Handles the processing of incoming chat messages in Temporal workflows.
/// </summary>
internal static class MessageProcessor
{
    /// <summary>
    /// Processes a single incoming message with full validation and error handling.
    /// </summary>
    public static async Task ProcessMessageAsync(
        InboundMessage message,
        ConcurrentDictionary<string, WorkflowHandlerMetadata> handlerRegistry,
        string workflowType,
        string workflowId,
        ILogger logger)
    {
        logger.LogDebug(
            "ProcessMessageAsync: Type={Type}, Text={TextPreview}",
            message.Payload.Type,
            message.Payload.Text.Length > 50
                ? $"{message.Payload.Text[..50]}..."
                : message.Payload.Text);

        // Only process Chat type messages (skip Data and Handoff for now)
        if (message.Payload.Type.ToLower() != "chat")
        {
            logger.LogWarning(
                "Skipping non-chat message: Type={Type}, RequestId={RequestId}",
                message.Payload.Type,
                message.Payload.RequestId);
            return;
        }

        // Extract tenant ID from WorkflowId using centralized utility
        string workflowTenantId;
        try
        {
            workflowTenantId = TenantContext.ExtractTenantId(workflowId);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex,
                "Failed to extract tenant ID from WorkflowId: {WorkflowId}",
                workflowId);
            return;
        }

        logger.LogDebug(
            "Processing message: ParticipantId={ParticipantId}, Scope={Scope}, Hint={Hint}, Tenant={Tenant}",
            message.Payload.ParticipantId,
            message.Payload.Scope,
            message.Payload.Hint,
            workflowTenantId);

        // Lookup handler metadata for this specific workflow type
        if (!handlerRegistry.TryGetValue(workflowType, out var metadata))
        {
            logger.LogWarning(
                "No message handler registered for WorkflowType={WorkflowType}, RequestId={RequestId}",
                workflowType,
                message.Payload.RequestId);

            await MessageResponseHelper.SendSimpleMessageAsync(
                message.Payload.ParticipantId,
                $"No message handler registered for workflow type '{workflowType}'.",
                message.Payload.RequestId,
                message.Payload.Scope,
                message.Payload.ThreadId,
                message.Payload.Authorization,
                message.Payload.Hint,
                workflowTenantId,
                workflowId,
                workflowType);
            return;
        }

        // Validate tenant isolation
        if (!MessageValidator.ValidateTenantIsolation(workflowTenantId, metadata, logger))
        {
            await MessageResponseHelper.SendSimpleMessageAsync(
                message.Payload.ParticipantId,
                "Error: Tenant isolation violation.",
                message.Payload.RequestId,
                message.Payload.Scope,
                message.Payload.ThreadId,
                message.Payload.Authorization,
                message.Payload.Hint,
                workflowTenantId,
                workflowId,
                workflowType);
            return;
        }

        // Validate agent name matches
        if (!MessageValidator.ValidateAgentName(message.Payload.Agent, metadata, message.Payload.RequestId, logger))
        {
            await MessageResponseHelper.SendSimpleMessageAsync(
                message.Payload.ParticipantId,
                $"Error: Message intended for agent '{message.Payload.Agent?.Trim()}' but received by '{metadata.AgentName}'.",
                message.Payload.RequestId,
                message.Payload.Scope,
                message.Payload.ThreadId,
                message.Payload.Authorization,
                message.Payload.Hint,
                workflowTenantId,
                workflowId,
                workflowType);
            return;
        }

        // All validations passed - process message and send responses via activity
        logger.LogInformation(
            "Processing message via activity: WorkflowType={WorkflowType}, Agent={Agent}, Tenant={Tenant}, SystemScoped={SystemScoped}",
            workflowType,
            metadata.AgentName,
            workflowTenantId,
            metadata.SystemScoped);

        // Execute handler in activity - encapsulates agent API calls and sending responses
        var activityRequest = new ProcessMessageActivityRequest
        {
            MessageText = message.Payload.Text,
            ParticipantId = message.Payload.ParticipantId,
            RequestId = message.Payload.RequestId,
            Scope = message.Payload.Scope,
            Hint = message.Payload.Hint,
            Data = message.Payload.Data,
            TenantId = workflowTenantId,
            WorkflowId = workflowId,
            WorkflowType = workflowType,
            Authorization = message.Payload.Authorization,
            ThreadId = message.Payload.ThreadId
        };

        await Workflow.ExecuteActivityAsync(
            (MessageActivities act) => act.ProcessAndSendMessageAsync(activityRequest),
            MessageActivityOptions.GetStandardOptions());

        logger.LogInformation(
            "Message processed and responses sent: RequestId={RequestId}",
            message.Payload.RequestId);
    }

    /// <summary>
    /// Extracts the most meaningful error message from an exception chain.
    /// Unwraps Temporal-specific exceptions to get to the root cause.
    /// </summary>
    public static string GetMeaningfulErrorMessage(Exception ex)
    {
        // Unwrap ActivityFailureException to get the actual error
        if (ex is ActivityFailureException activityEx && activityEx.InnerException != null)
        {
            ex = activityEx.InnerException;
        }

        // Unwrap ApplicationFailureException to get the actual error
        if (ex is ApplicationFailureException appEx && appEx.InnerException != null)
        {
            ex = appEx.InnerException;
        }

        // Return the message from the innermost exception, or walk the chain if needed
        var currentEx = ex;
        while (currentEx.InnerException != null &&
               (currentEx.Message == "Activity task failed" ||
                currentEx.Message == "Workflow task failed" ||
                string.IsNullOrWhiteSpace(currentEx.Message)))
        {
            currentEx = currentEx.InnerException;
        }

        return currentEx.Message;
    }
}
