using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Temporal.Workflows;

namespace Xians.Lib.Agents.A2A;

/// <summary>
/// Client for sending Agent-to-Agent messages and receiving responses.
/// Enables synchronous request-response communication between agents in the same runtime.
/// </summary>
public class A2AClient
{
    private readonly XiansWorkflow _targetWorkflow;
    private readonly ILogger _logger;

    /// <summary>
    /// Gets whether A2A messages can be sent from the current context.
    /// Always returns true now that A2A is context-aware.
    /// - From workflow: Handler runs in isolated activity
    /// - From activity: Handler runs directly (no nested activity)
    /// </summary>
    public static bool CanSendFromCurrentContext => true;

    /// <summary>
    /// Creates a new A2A client for communicating with a target workflow.
    /// Source workflow context is automatically obtained from XiansContext.
    /// 
    /// CONTEXT-AWARE BEHAVIOR:
    /// - From workflow: Handler executes in isolated activity (retryable, non-deterministic ops allowed)
    /// - From activity: Handler executes directly (no nested activity, shares retry behavior)
    /// 
    /// A2A message RECEIVING (handling) works in both workflow and activity contexts.
    /// </summary>
    /// <param name="targetWorkflow">The target workflow to send messages to.</param>
    /// <exception cref="ArgumentNullException">Thrown when targetWorkflow is null.</exception>
    public A2AClient(XiansWorkflow targetWorkflow)
    {
        _targetWorkflow = targetWorkflow ?? throw new ArgumentNullException(nameof(targetWorkflow));
        _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<A2AClient>();
    }

    /// <summary>
    /// Attempts to send a chat message to the target agent.
    /// Returns success/failure without throwing exceptions.
    /// </summary>
    public Task<(bool Success, A2AMessage? Response, string? ErrorMessage)> TrySendMessageAsync(A2AMessage message)
        => TrySendMessageInternalAsync(message, "Chat");

    /// <summary>
    /// Attempts to send a data message to the target agent.
    /// Returns success/failure without throwing exceptions.
    /// </summary>
    public Task<(bool Success, A2AMessage? Response, string? ErrorMessage)> TrySendDataMessageAsync(A2AMessage message)
        => TrySendMessageInternalAsync(message, "Data");

    private async Task<(bool Success, A2AMessage? Response, string? ErrorMessage)> TrySendMessageInternalAsync(
        A2AMessage message, string messageType)
    {
        try
        {
            var response = await SendMessageInternalAsync(message, messageType);
            return (true, response, null);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "A2A send attempt failed");
            return (false, null, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in A2A send");
            return (false, null, $"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends a chat message to the target agent and waits for a response.
    /// </summary>
    public Task<A2AMessage> SendMessageAsync(A2AMessage message)
        => SendMessageInternalAsync(message, "Chat");

    /// <summary>
    /// Sends a data message to the target agent and waits for a response.
    /// Data messages are routed to OnUserDataMessage handlers.
    /// </summary>
    public Task<A2AMessage> SendDataMessageAsync(A2AMessage message)
        => SendMessageInternalAsync(message, "Data");

    /// <summary>
    /// Sends a message to the target agent and waits for a response.
    /// Directly invokes the target workflow's handler in the same runtime.
    /// Uses context-aware execution pattern via A2AActivityExecutor.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="messageType">The message type: "Chat" or "Data".</param>
    /// <returns>The response message from the target agent.</returns>
    /// <exception cref="ArgumentNullException">Thrown when message is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request fails.</exception>
    private async Task<A2AMessage> SendMessageInternalAsync(A2AMessage message, string messageType)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        // Get source agent name, tenant ID, and workflow ID (with fallbacks for non-workflow context)
        string sourceAgentName;
        string tenantId;
        string sourceWorkflowId;

        sourceAgentName = XiansContext.AgentName;
        tenantId = XiansContext.TenantId;
        sourceWorkflowId = XiansContext.WorkflowId;


        _logger.LogDebug(
            "Sending A2A message: Target={TargetWorkflow}, SourceAgent={SourceAgent}",
            _targetWorkflow.WorkflowType,
            sourceAgentName);

        // Validate that target workflow has a handler
        if (!BuiltinWorkflow._handlersByWorkflowType.TryGetValue(
            _targetWorkflow.WorkflowType, out var handlerMetadata))
        {
            throw new InvalidOperationException(
                $"No message handler registered for workflow type '{_targetWorkflow.WorkflowType}'. " +
                $"Ensure the target workflow has called OnUserMessage().");
        }

        // Validate the appropriate handler exists for the message type
        var isDataMessage = messageType.Equals("Data", StringComparison.OrdinalIgnoreCase);
        if (isDataMessage)
        {
            if (handlerMetadata?.DataHandler == null)
            {
                throw new InvalidOperationException(
                    $"No data message handler registered for workflow type '{_targetWorkflow.WorkflowType}'. " +
                    $"Ensure the target workflow has called OnUserDataMessage().");
            }
        }
        else
        {
            if (handlerMetadata?.ChatHandler == null)
            {
                throw new InvalidOperationException(
                    $"No chat message handler registered for workflow type '{_targetWorkflow.WorkflowType}'. " +
                    $"Ensure the target workflow has called OnUserChatMessage() or OnUserMessage().");
            }
        }

        // Create A2A request
        var activityRequest = new Xians.Lib.Temporal.Workflows.Messaging.Models.ProcessMessageActivityRequest
        {
            MessageText = message.Text,
            ParticipantId = string.IsNullOrEmpty(message.ParticipantId) ? sourceWorkflowId : message.ParticipantId,
            RequestId = string.IsNullOrEmpty(message.RequestId) ? Guid.NewGuid().ToString("N") : message.RequestId,
            Scope = string.IsNullOrEmpty(message.Scope) ? "A2A" : message.Scope,
            Hint = message.Hint ?? string.Empty,  // Hint is for message processing, not agent name
            Data = message.Data ?? new object(),
            TenantId = tenantId,
            WorkflowId = _targetWorkflow.WorkflowType,
            WorkflowType = _targetWorkflow.WorkflowType,
            Authorization = message.Authorization,
            ThreadId = string.IsNullOrEmpty(message.ThreadId) ? sourceWorkflowId : message.ThreadId,
            Metadata = message.Metadata,
            MessageType = messageType
        };

        try
        {
            // Use executor for context-aware execution
            // - From workflow: Execute handler in isolated activity
            // - From activity: Execute handler directly to avoid nested activities
            var executor = new A2AActivityExecutor(_targetWorkflow, _logger);
            var response = await executor.ProcessA2AMessageAsync(activityRequest);

            _logger.LogInformation(
                "A2A response received from {TargetWorkflow}",
                _targetWorkflow.WorkflowType);

            return new A2AMessage
            {
                Text = response.Text,
                Data = response.Data,
                Metadata = message.Metadata
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "A2A request failed: Target={TargetWorkflow}",
                _targetWorkflow.WorkflowType);

            throw new InvalidOperationException(
                $"A2A request failed: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// Class to capture the response from the A2A handler.
/// </summary>
public class A2AResponseCapture
{
    public bool HasResponse { get; set; }
    public string? Text { get; set; }
    public object? Data { get; set; }
}
