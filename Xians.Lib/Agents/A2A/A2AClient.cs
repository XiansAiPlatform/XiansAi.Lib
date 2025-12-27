using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Messaging.Models;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Workflows;

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
    /// Creates a new A2A client for communicating with a target workflow.
    /// Source workflow context is automatically obtained from XiansContext.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <param name="targetWorkflow">The target workflow to send messages to.</param>
    /// <exception cref="ArgumentNullException">Thrown when targetWorkflow is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    public A2AClient(XiansWorkflow targetWorkflow)
    {
        _targetWorkflow = targetWorkflow ?? throw new ArgumentNullException(nameof(targetWorkflow));
        _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<A2AClient>();

        // Validate that we're in a workflow or activity context
        if (!XiansContext.InWorkflow && !XiansContext.InActivity)
        {
            throw new InvalidOperationException(
                $"A2AClient can only be used within a Temporal workflow or activity. " +
                $"Use XiansContext.InWorkflow or XiansContext.InActivity to check your context.");
        }
    }

    /// <summary>
    /// Sends a message to the target agent and waits for a response.
    /// Directly invokes the target workflow's handler in the same runtime.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <returns>The response message from the target agent.</returns>
    /// <exception cref="ArgumentNullException">Thrown when message is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request fails.</exception>
    public async Task<A2AMessage> SendMessageAsync(A2AMessage message)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        _logger.LogInformation(
            "Sending A2A message: Target={TargetWorkflow}, SourceAgent={SourceAgent}",
            _targetWorkflow.WorkflowType,
            XiansContext.AgentName);

        // Get the handler for the target workflow
        if (!DefaultWorkflow._handlersByWorkflowType.TryGetValue(
            _targetWorkflow.WorkflowType, out var handlerMetadata))
        {
            throw new InvalidOperationException(
                $"No message handler registered for workflow type '{_targetWorkflow.WorkflowType}'. " +
                $"Ensure the target workflow has called OnUserMessage().");
        }

        // Create A2A request for context
        var request = new A2ARequest
        {
            CorrelationId = Guid.NewGuid().ToString("N"),
            SourceWorkflowId = XiansContext.WorkflowId,
            SourceAgentName = XiansContext.AgentName,
            SourceWorkflowType = XiansContext.WorkflowType,
            Text = message.Text,
            Data = message.Data,
            TenantId = XiansContext.TenantId,
            Metadata = message.Metadata
        };

        // Create user message
        var userMessage = new UserMessage { Text = message.Text };

        // Create A2A message context with response capture
        // IMPORTANT: Use TARGET workflow context, not source
        var responseCapture = new A2AResponseCapture();
        var context = new A2AMessageContext(
            message: userMessage,
            request: request,
            workflowId: _targetWorkflow.WorkflowType,  // Target workflow type (acts as ID)
            workflowType: _targetWorkflow.WorkflowType, // Target workflow type
            responseCapture: responseCapture
        );

        try
        {
            // Directly invoke the handler (same runtime, synchronous call)
            await handlerMetadata.Handler(context);

            // Get the captured response
            if (!responseCapture.HasResponse)
            {
                throw new InvalidOperationException(
                    $"Target workflow handler did not send a response. " +
                    $"Ensure the handler calls context.ReplyAsync() or context.ReplyWithDataAsync().");
            }

            _logger.LogInformation(
                "A2A response received from {TargetWorkflow}",
                _targetWorkflow.WorkflowType);

            return new A2AMessage
            {
                Text = responseCapture.Text ?? string.Empty,
                Data = responseCapture.Data,
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
/// Internal class to capture the response from the A2A handler.
/// </summary>
internal class A2AResponseCapture
{
    public bool HasResponse { get; set; }
    public string? Text { get; set; }
    public object? Data { get; set; }
}

