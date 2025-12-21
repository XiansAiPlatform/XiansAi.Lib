using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Temporalio.Converters;
using Xians.Lib.Agents;

namespace Xians.Lib.Workflows;

/// <summary>
/// Default workflow that handles user chat messages via temporal signals.
/// Simplified design that receives messages via signals and delegates to user-defined handlers.
/// </summary>
[Workflow(Dynamic = true)]
public class DefaultWorkflow
{
    private readonly Queue<InboundMessage> _messageQueue = new();
    
    // Dictionary to store handlers per WorkflowType instead of a single static handler
    // This allows multiple default workflows to each have their own handler
    private static readonly Dictionary<string, Func<UserMessageContext, Task>> _handlersByWorkflowType = new();

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
    /// Registers a user message handler for a specific workflow type.
    /// Uses WorkflowType as key to support multiple default workflows.
    /// </summary>
    /// <param name="workflowType">The unique workflow type identifier.</param>
    /// <param name="handler">The handler function to register.</param>
    public static void RegisterMessageHandler(string workflowType, Func<UserMessageContext, Task> handler)
    {
        lock (_handlersByWorkflowType)
        {
            _handlersByWorkflowType[workflowType] = handler;
        }
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
                        
                        await ProcessMessageAsync(message);
                        
                        Workflow.Logger.LogDebug(
                            "Message processing completed: RequestId={RequestId}",
                            message.Payload.RequestId);
                    }
                    catch (Exception ex)
                    {
                        // Top-level exception handler - safe to catch here to prevent workflow crash
                        Workflow.Logger.LogError(ex, 
                            "Error processing message from {ParticipantId}: {ErrorMessage}", 
                            message.Payload.ParticipantId, 
                            ex.Message);
                        
                        // Attempt to send error response to user
                        // If this fails, we log but don't rethrow as we're already in error state
                        try
                        {
                            Workflow.Logger.LogDebug(
                                "Attempting to send error response: RequestId={RequestId}",
                                message.Payload.RequestId);
                            
                            await SendErrorResponseAsync(message, ex.Message);
                            
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

    /// <summary>
    /// Processes a single incoming message
    /// </summary>
    private async Task ProcessMessageAsync(InboundMessage message)
    {
        Workflow.Logger.LogDebug(
            "ProcessMessageAsync: Type={Type}, Text={TextPreview}",
            message.Payload.Type,
            message.Payload.Text.Length > 50 
                ? message.Payload.Text.Substring(0, 50) + "..." 
                : message.Payload.Text);
        
        // Only process Chat type messages (skip Data and Handoff for now)
        if (message.Payload.Type.ToLower() != "chat")
        {
            Workflow.Logger.LogWarning(
                "Skipping non-chat message: Type={Type}, RequestId={RequestId}",
                message.Payload.Type,
                message.Payload.RequestId);
            return;
        }

        // Create user message context with all fields from the incoming message
        var userMessage = new UserMessage
        {
            Text = message.Payload.Text
        };

        Workflow.Logger.LogDebug(
            "Creating UserMessageContext: ParticipantId={ParticipantId}, Scope={Scope}, Hint={Hint}",
            message.Payload.ParticipantId,
            message.Payload.Scope,
            message.Payload.Hint);

        var context = new UserMessageContext(
            userMessage,
            message.Payload.ParticipantId,
            message.Payload.RequestId,
            message.Payload.Scope,
            message.Payload.Hint,
            message.Payload.Data,
            message.Payload.Authorization,
            message.Payload.ThreadId
        );

        // Get the workflow type from Workflow.Info
        var workflowType = Workflow.Info.WorkflowType;
        
        // Lookup handler for this specific workflow type
        Func<UserMessageContext, Task>? handler;
        lock (_handlersByWorkflowType)
        {
            _handlersByWorkflowType.TryGetValue(workflowType, out handler);
        }
        
        // Invoke the registered user handler
        if (handler != null)
        {
            Workflow.Logger.LogDebug(
                "Invoking user message handler for WorkflowType={WorkflowType}",
                workflowType);
            await handler(context);
            Workflow.Logger.LogDebug("User message handler completed");
        }
        else
        {
            Workflow.Logger.LogWarning(
                "No message handler registered for WorkflowType={WorkflowType}, RequestId={RequestId}",
                workflowType,
                message.Payload.RequestId);
            
            // No handler registered - send default message
            await context.ReplyAsync($"No message handler registered for workflow type '{workflowType}'.");
        }
    }

    /// <summary>
    /// Sends an error response back to the user
    /// </summary>
    private async Task SendErrorResponseAsync(InboundMessage message, string errorMessage)
    {
        var context = new UserMessageContext(
            new UserMessage { Text = message.Payload.Text },
            message.Payload.ParticipantId,
            message.Payload.RequestId,
            message.Payload.Scope,
            message.Payload.Hint,
            message.Payload.Data,
            message.Payload.Authorization,
            message.Payload.ThreadId
        );

        await context.ReplyAsync($"Error: {errorMessage}");
    }
}

/// <summary>
/// Represents an inbound message signal from the Xians platform.
/// Matches the structure sent by the server.
/// </summary>
public class InboundMessage
{
    public required InboundMessagePayload Payload { get; set; }
    public required string SourceAgent { get; set; }
    public required string SourceWorkflowId { get; set; }
    public required string SourceWorkflowType { get; set; }
}

/// <summary>
/// Payload of an inbound message.
/// Must match MessagePayload structure from XiansAi.Lib.Src/Messenging/Models.cs
/// </summary>
public class InboundMessagePayload
{
    public required string Agent { get; set; }
    public required string ThreadId { get; set; }
    public required string ParticipantId { get; set; }
    public string? Authorization { get; set; }
    public required string Text { get; set; }
    public required string RequestId { get; set; }
    public required string Hint { get; set; }
    public required string Scope { get; set; }
    public required object Data { get; set; }
    public required string Type { get; set; }
    public List<DbMessage>? History { get; set; }
}

/// <summary>
/// Database message structure for conversation history.
/// Matches DbMessage from XiansAi.Lib.Src/Messenging/Models.cs
/// </summary>
public class DbMessage
{
    public string Id { get; set; } = null!;
    public required string ThreadId { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public required string Direction { get; set; }
    public string? Text { get; set; }
    public string? Status { get; set; }
    public object? Data { get; set; }
    public required string ParticipantId { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
}