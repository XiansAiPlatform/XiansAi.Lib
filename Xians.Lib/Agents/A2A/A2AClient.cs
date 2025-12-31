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
    /// Must be used from workflow context only (not activity context).
    /// </summary>
    /// <param name="targetWorkflow">The target workflow to send messages to.</param>
    /// <exception cref="ArgumentNullException">Thrown when targetWorkflow is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow context.</exception>
    public A2AClient(XiansWorkflow targetWorkflow)
    {
        _targetWorkflow = targetWorkflow ?? throw new ArgumentNullException(nameof(targetWorkflow));
        _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<A2AClient>();

        // Validate that we're in a workflow context
        if (!XiansContext.InWorkflow)
        {
            throw new InvalidOperationException(
                $"A2AClient can only be used within a Temporal workflow context. " +
                $"Current context: InWorkflow={XiansContext.InWorkflow}, InActivity={XiansContext.InActivity}. " +
                $"A2A calls from activities are not supported to prevent nested activity dependencies.");
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
        if (!BuiltinWorkflow._handlersByWorkflowType.TryGetValue(
            _targetWorkflow.WorkflowType, out var handlerMetadata))
        {
            throw new InvalidOperationException(
                $"No message handler registered for workflow type '{_targetWorkflow.WorkflowType}'. " +
                $"Ensure the target workflow has called OnUserMessage().");
        }

        if (handlerMetadata?.Handler == null)
        {
            throw new InvalidOperationException(
                $"Message handler for workflow type '{_targetWorkflow.WorkflowType}' is null. " +
                $"This indicates a registration error.");
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

        try
        {
            // A2A calls must be made from workflow context
            // Activity-to-activity A2A is not supported as it would create nested activity dependencies
            if (!XiansContext.InWorkflow)
            {
                throw new InvalidOperationException(
                    "A2A calls can only be made from workflow context, not from activity context. " +
                    "This prevents nested activity dependencies and ensures proper workflow orchestration.");
            }

            _logger.LogDebug(
                "Executing A2A handler via activity: Target={TargetWorkflow}",
                _targetWorkflow.WorkflowType);

            // Create activity request
            var activityRequest = new Xians.Lib.Workflows.Messaging.Models.ProcessMessageActivityRequest
            {
                MessageText = message.Text,
                ParticipantId = request.CorrelationId, // Use correlation ID as participant
                RequestId = request.CorrelationId,
                Scope = "A2A",
                Hint = request.SourceAgentName,
                Data = message.Data ?? new object(),
                TenantId = request.TenantId,
                WorkflowId = _targetWorkflow.WorkflowType,
                WorkflowType = _targetWorkflow.WorkflowType,
                Authorization = null,
                ThreadId = request.CorrelationId
            };

            // Execute handler in activity to avoid non-determinism
            var activityResponse = await Workflow.ExecuteActivityAsync(
                (Xians.Lib.Workflows.Messaging.MessageActivities act) => act.ProcessA2AMessageAsync(activityRequest),
                Xians.Lib.Workflows.Messaging.MessageActivityOptions.GetStandardOptions());

            _logger.LogInformation(
                "A2A response received from {TargetWorkflow}",
                _targetWorkflow.WorkflowType);

            return new A2AMessage
            {
                Text = activityResponse.Text,
                Data = activityResponse.Data,
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
