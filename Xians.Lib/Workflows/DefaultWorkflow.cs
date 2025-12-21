using Temporalio.Workflows;
using Xians.Lib.Agents;

namespace Xians.Lib.Workflows;

/// <summary>
/// Default workflow that handles user chat messages via temporal signals
/// </summary>
[Workflow]
public class DefaultWorkflow
{
    private readonly Queue<MessageSignalData> _messageQueue = new();
    private Func<UserMessageContext, Task>? _userMessageHandler;
    private bool _continueAsNew = false;

    /// <summary>
    /// Main workflow execution method
    /// </summary>
    [WorkflowRun]
    public async Task RunAsync()
    {
        // Start the message processing loop
        await ProcessMessagesAsync();
    }

    /// <summary>
    /// Signal handler that receives chat messages from temporal
    /// </summary>
    [WorkflowSignal]
    public async Task HandleInboundChatOrData(MessageSignalData messageSignal)
    {
        await Task.Run(() => _messageQueue.Enqueue(messageSignal));
    }

    /// <summary>
    /// Registers a user message handler (called from XiansWorkflow.OnUserMessage)
    /// </summary>
    [WorkflowQuery]
    public void RegisterUserMessageHandler(Func<UserMessageContext, Task> handler)
    {
        _userMessageHandler = handler;
    }

    /// <summary>
    /// Main message processing loop
    /// </summary>
    private async Task ProcessMessagesAsync()
    {
        while (!_continueAsNew)
        {
            // Wait for a message to arrive
            await Workflow.WaitConditionAsync(() => _messageQueue.Count > 0);

            // Dequeue and process the message
            if (_messageQueue.TryDequeue(out var messageSignal))
            {
                // Process message in background task to avoid blocking the loop
                _ = Workflow.RunTaskAsync(async () =>
                {
                    try
                    {
                        await ProcessSingleMessageAsync(messageSignal);
                    }
                    catch (Exception ex)
                    {
                        // Log error and send error response to user
                        await SendErrorResponseAsync(messageSignal, ex);
                    }
                });
            }
        }
    }

    /// <summary>
    /// Processes a single message
    /// </summary>
    private async Task ProcessSingleMessageAsync(MessageSignalData messageSignal)
    {
        // Parse message type
        var messageType = ParseMessageType(messageSignal.Payload.Type);
        
        // Only process Chat messages (not Data messages)
        if (messageType != MessageType.Chat)
        {
            return;
        }

        // Create UserMessageContext from the signal
        var userMessage = new UserMessage
        {
            Text = messageSignal.Payload.Text ?? string.Empty
        };

        var context = new UserMessageContext(
            userMessage,
            messageSignal.Payload.ParticipantId,
            messageSignal.Payload.RequestId,
            messageSignal.Payload.Scope
        );

        // Invoke the registered user message handler
        if (_userMessageHandler != null)
        {
            await _userMessageHandler(context);
        }
        else
        {
            // No handler registered - send a default response
            await context.ReplyAsync("No message handler registered.");
        }
    }

    /// <summary>
    /// Sends an error response back to the user
    /// </summary>
    private async Task SendErrorResponseAsync(MessageSignalData messageSignal, Exception ex)
    {
        var errorContext = new UserMessageContext(
            new UserMessage { Text = messageSignal.Payload.Text ?? string.Empty },
            messageSignal.Payload.ParticipantId,
            messageSignal.Payload.RequestId,
            messageSignal.Payload.Scope
        );

        await errorContext.ReplyAsync($"Error processing message: {ex.Message}");
    }

    /// <summary>
    /// Parses message type from string
    /// </summary>
    private MessageType ParseMessageType(string type)
    {
        return type.ToLower() switch
        {
            "chat" => MessageType.Chat,
            "data" => MessageType.Data,
            "handoff" => MessageType.Handoff,
            _ => MessageType.Chat
        };
    }
}

/// <summary>
/// Simplified message signal structure for the new workflow
/// </summary>
public class MessageSignalData
{
    public required MessagePayloadData Payload { get; set; }
    public required string SourceAgent { get; set; }
    public required string SourceWorkflowId { get; set; }
    public required string SourceWorkflowType { get; set; }
}

/// <summary>
/// Simplified message payload structure
/// </summary>
public class MessagePayloadData
{
    public required string Agent { get; set; }
    public required string ThreadId { get; set; }
    public required string ParticipantId { get; set; }
    public string? Authorization { get; set; }
    public string? Text { get; set; }
    public required string RequestId { get; set; }
    public string? Hint { get; set; }
    public string? Scope { get; set; }
    public object? Data { get; set; }
    public required string Type { get; set; }
}

/// <summary>
/// Message type enum
/// </summary>
public enum MessageType
{
    Chat,
    Data,
    Handoff
}