using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Xians.Lib.Agents.A2A;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common.Infrastructure;

namespace Xians.Lib.Agents.Messaging;

/// <summary>
/// Provides Agent-to-Agent (A2A) messaging operations.
/// Accessible via MessageCollection.A2A for sending messages to other agents.
/// </summary>
public class A2AOperations
{
    private readonly ILogger<A2AOperations> _logger;

    internal A2AOperations()
    {
        _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<A2AOperations>();
    }

    /// <summary>
    /// Sends a chat message to another agent and waits for a response.
    /// Uses Agent-to-Agent (A2A) communication for synchronous request-response.
    /// Must be called from workflow context.
    /// </summary>
    /// <param name="targetWorkflow">The target workflow to send the message to.</param>
    /// <param name="message">The message text to send.</param>
    /// <param name="data">Optional data to send. If null, uses the original context data.</param>
    /// <returns>The response from the target agent.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow context or when the target workflow is not found.</exception>
    public async Task<A2AMessage> SendChatAsync(XiansWorkflow targetWorkflow, string message, object? data = null)
    {
        if (!Workflow.InWorkflow)
        {
            throw new InvalidOperationException(
                "A2A operations can only be called from within a workflow context. " +
                "A2A messaging requires workflow orchestration.");
        }

        _logger.LogDebug(
            "Sending A2A chat message: Target={TargetWorkflow}, MessageLength={MessageLength}",
            targetWorkflow.WorkflowType,
            message?.Length ?? 0);

        var client = new A2AClient(targetWorkflow);
        var a2aMessage = new A2AMessage
        {
            Text = message ?? string.Empty,
            Data = data
        };

        return await client.SendMessageAsync(a2aMessage);
    }

    /// <summary>
    /// Sends data to another agent and waits for a response.
    /// Uses Agent-to-Agent (A2A) communication for synchronous request-response.
    /// Must be called from workflow context.
    /// </summary>
    /// <param name="targetWorkflow">The target workflow to send the data to.</param>
    /// <param name="data">The data object to send.</param>
    /// <param name="message">Optional message text to accompany the data.</param>
    /// <returns>The response from the target agent.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow context or when the target workflow is not found.</exception>
    public async Task<A2AMessage> SendDataAsync(XiansWorkflow targetWorkflow, object data, string? message = null)
    {
        if (!Workflow.InWorkflow)
        {
            throw new InvalidOperationException(
                "A2A operations can only be called from within a workflow context. " +
                "A2A messaging requires workflow orchestration.");
        }

        _logger.LogDebug(
            "Sending A2A data message: Target={TargetWorkflow}",
            targetWorkflow.WorkflowType);

        var client = new A2AClient(targetWorkflow);
        var a2aMessage = new A2AMessage
        {
            Text = message ?? string.Empty,
            Data = data
        };

        return await client.SendMessageAsync(a2aMessage);
    }

}

