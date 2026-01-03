using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Agents.Messaging.Models;
using Xians.Lib.Workflows;
using Xians.Lib.Workflows.Messaging.Models;
using Xians.Lib.Workflows.Models;

namespace Xians.Lib.Agents.A2A;

/// <summary>
/// Service for direct A2A message processing (activity context).
/// Handles invocation of target workflow handlers without Temporal activities.
/// Shared by both MessageActivities (activity context) and A2AClient (direct call context).
/// </summary>
internal class A2AService
{
    private readonly string _targetWorkflowType;
    private readonly ILogger<A2AService> _logger;

    public A2AService(string targetWorkflowType)
    {
        _targetWorkflowType = targetWorkflowType ?? throw new ArgumentNullException(nameof(targetWorkflowType));
        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<A2AService>();
    }

    public A2AService(XiansWorkflow targetWorkflow)
        : this(targetWorkflow?.WorkflowType ?? throw new ArgumentNullException(nameof(targetWorkflow)))
    {
    }

    /// <summary>
    /// Processes an A2A message by directly invoking the target workflow's handler.
    /// Used when calling from activity context to avoid nested activities.
    /// Shared by MessageActivities and A2AClient for consistent behavior.
    /// </summary>
    public async Task<A2AActivityResponse> ProcessDirectAsync(ProcessMessageActivityRequest request)
    {
        _logger.LogDebug(
            "Processing A2A message directly: Target={TargetWorkflow}, RequestId={RequestId}",
            _targetWorkflowType,
            request.RequestId);

        // Get the handler for the target workflow
        var handlerMetadata = GetHandlerMetadata(request.WorkflowType);
        var handler = GetHandler(handlerMetadata, request.MessageType);

        // Create response capture
        var responseCapture = new A2AResponseCapture();

        // Create A2A request for context
        // Use information from the request (which was populated by A2AClient)
        var a2aRequest = new A2ARequest
        {
            CorrelationId = request.RequestId,
            SourceWorkflowId = request.WorkflowId,  // From request, not context
            SourceWorkflowType = request.WorkflowType,  // SourceAgentName is computed from this
            Text = request.MessageText,
            Data = request.Data,
            TenantId = request.TenantId,
            Authorization = request.Authorization,
            Metadata = request.Metadata,
            ParticipantId = request.ParticipantId,
            RequestId = request.RequestId,
            Scope = request.Scope,
            Hint = request.Hint,
            ThreadId = request.ThreadId
        };

        // Create A2A message context for the handler
        var userMessage = new UserMessage { Text = request.MessageText };
        var context = new A2AMessageContext(
            userMessage,
            a2aRequest,
            request.WorkflowId,
            request.WorkflowType,
            responseCapture);

        // Invoke handler directly
        await handler(context);

        // Check response was captured
        if (!responseCapture.HasResponse)
        {
            throw new InvalidOperationException(
                $"Target workflow handler did not send a response. " +
                $"Ensure the handler calls context.Messages.ReplyAsync() or context.Messages.ReplyWithDataAsync().");
        }

        _logger.LogInformation(
            "A2A message processed directly: Target={TargetWorkflow}",
            _targetWorkflowType);

        return new A2AActivityResponse
        {
            Text = responseCapture.Text ?? string.Empty,
            Data = responseCapture.Data
        };
    }

    private static WorkflowHandlerMetadata GetHandlerMetadata(string workflowType)
    {
        if (!BuiltinWorkflow._handlersByWorkflowType.TryGetValue(
            workflowType, out var handlerMetadata))
        {
            throw new InvalidOperationException(
                $"No message handler registered for workflow type '{workflowType}'. " +
                $"Ensure the target workflow has called OnUserMessage().");
        }

        return handlerMetadata;
    }

    private static Func<UserMessageContext, Task> GetHandler(WorkflowHandlerMetadata metadata, string messageType)
    {
        var handler = messageType?.ToLower() == "data"
            ? metadata.DataHandler
            : metadata.ChatHandler;

        if (handler == null)
        {
            var type = messageType ?? "Chat";
            throw new InvalidOperationException(
                $"No {type} handler registered. " +
                $"Use OnUser{type}Message() to register a handler.");
        }

        return handler;
    }
}

