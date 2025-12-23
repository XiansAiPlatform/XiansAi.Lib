using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Temporalio.Converters;
using Xians.Lib.Agents;
using Xians.Lib.Workflows.Models;

namespace Xians.Lib.Workflows;

/// <summary>
/// Default workflow that orchestrates various event types (chat messages, webhooks, scheduled events, etc.).
/// Acts as a central event router, delegating to specialized handlers for each event type.
/// </summary>
[Workflow(Dynamic = true)]
public class DefaultWorkflow
{
    private readonly Queue<InboundMessage> _messageQueue = new();
    
    // Metadata for each registered workflow handler including tenant isolation info
    // Made internal static to allow activities to access it
    // Using ConcurrentDictionary to eliminate lock contention during message processing
    internal static readonly ConcurrentDictionary<string, WorkflowHandlerMetadata> _handlersByWorkflowType = new();

    /// <summary>
    /// Main workflow execution method
    /// </summary>
    [WorkflowRun]
    public async Task RunAsync(IRawValue[] args)
    {
        // Start the message processing loop
        await ProcessMessagesLoopAsync();
    }

    /// <summary>
    /// Signal handler that receives chat or data messages from temporal.
    /// This matches the signal name used by the server.
    /// Returns Task.CompletedTask to satisfy Temporal's async requirement while keeping operation synchronous.
    /// </summary>
    [WorkflowSignal("HandleInboundChatOrData")]
    public Task HandleInboundChatOrData(InboundMessage message)
    {
        Workflow.Logger.LogDebug(
            "Signal received: Type={Type}, ParticipantId={ParticipantId}, RequestId={RequestId}, QueueDepth={QueueDepth}",
            message.Payload.Type,
            message.Payload.ParticipantId,
            message.Payload.RequestId,
            _messageQueue.Count);
        
        _messageQueue.Enqueue(message);
        
        Workflow.Logger.LogDebug(
            "Message enqueued: QueueDepth={QueueDepth}",
            _messageQueue.Count);
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Registers a user message handler for a specific workflow type with tenant isolation metadata.
    /// </summary>
    /// <param name="workflowType">The unique workflow type identifier.</param>
    /// <param name="handler">The handler function to register.</param>
    /// <param name="agentName">The agent name for validation.</param>
    /// <param name="tenantId">The tenant ID (null for system-scoped agents).</param>
    /// <param name="systemScoped">Whether this is a system-scoped agent.</param>
    public static void RegisterMessageHandler(
        string workflowType, 
        Func<UserMessageContext, Task> handler,
        string agentName,
        string? tenantId,
        bool systemScoped)
    {
        _handlersByWorkflowType[workflowType] = new WorkflowHandlerMetadata
        {
            Handler = handler,
            AgentName = agentName.Trim(), // Trim to handle whitespace variations
            TenantId = tenantId,
            SystemScoped = systemScoped
        };
    }

    /// <summary>
    /// Main message processing loop - waits for signals and processes them.
    /// This is the top-level event loop where exceptions are caught to prevent workflow crashes.
    /// </summary>
    private async Task ProcessMessagesLoopAsync()
    {
        Workflow.Logger.LogInformation("Message processing loop started");
        
        while (true)
        {
            // Wait for a message to arrive in the queue
            Workflow.Logger.LogDebug("Waiting for messages... QueueDepth={QueueDepth}", _messageQueue.Count);
            await Workflow.WaitConditionAsync(() => _messageQueue.Count > 0);

            // Dequeue and process the message
            if (_messageQueue.TryDequeue(out var message))
            {
                Workflow.Logger.LogDebug(
                    "Dequeued message: Type={Type}, ParticipantId={ParticipantId}, RequestId={RequestId}, RemainingInQueue={RemainingInQueue}",
                    message.Payload.Type,
                    message.Payload.ParticipantId,
                    message.Payload.RequestId,
                    _messageQueue.Count);
                
                // Process message in background to avoid blocking the loop
                _ = Workflow.RunTaskAsync(async () =>
                {
                    try
                    {
                        Workflow.Logger.LogDebug(
                            "Starting message processing: RequestId={RequestId}",
                            message.Payload.RequestId);
                        
                        await MessageProcessor.ProcessMessageAsync(
                            message,
                            _handlersByWorkflowType,
                            Workflow.Info.WorkflowType,
                            Workflow.Info.WorkflowId,
                            Workflow.Info.TaskQueue,
                            Workflow.Logger);
                        
                        Workflow.Logger.LogDebug(
                            "Message processing completed: RequestId={RequestId}",
                            message.Payload.RequestId);
                    }
                    catch (Exception ex)
                    {
                        // Extract meaningful error message from exception chain
                        var errorMessage = MessageProcessor.GetMeaningfulErrorMessage(ex);
                        
                        // Top-level exception handler - safe to catch here to prevent workflow crash
                        Workflow.Logger.LogError(ex, 
                            "Error processing message from {ParticipantId}: {ErrorMessage}", 
                            message.Payload.ParticipantId, 
                            errorMessage);
                        
                        // Attempt to send error response to user
                        // If this fails, we log but don't rethrow as we're already in error state
                        try
                        {
                            Workflow.Logger.LogDebug(
                                "Attempting to send error response: RequestId={RequestId}",
                                message.Payload.RequestId);
                            
                            await MessageResponseHelper.SendErrorResponseAsync(
                                message, 
                                errorMessage, 
                                Workflow.Logger);
                            
                            Workflow.Logger.LogDebug(
                                "Error response sent: RequestId={RequestId}",
                                message.Payload.RequestId);
                        }
                        catch (Exception errorEx)
                        {
                            Workflow.Logger.LogError(errorEx, 
                                "Failed to send error response to {ParticipantId}", 
                                message.Payload.ParticipantId);
                        }
                    }
                });
            }
        }
    }
}