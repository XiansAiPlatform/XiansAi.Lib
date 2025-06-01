using System.Text.Json;
using Temporalio.Workflows;
using XiansAi.Logging;

namespace XiansAi.Messaging;

public delegate Task MessageReceivedAsyncHandler(MessageThread messageThread);
public delegate void MessageReceivedHandler(MessageThread messageThread);

public interface IMessageHub
{
    // message handlers for incoming messages with content
    void RegisterAsyncMessageHandler(MessageReceivedAsyncHandler handler);
    void RegisterMessageHandler(MessageReceivedHandler handler);
    void UnregisterMessageHandler(MessageReceivedHandler handler);
    void UnregisterAsyncMessageHandler(MessageReceivedAsyncHandler handler);

    // message handlers for incoming messages with only content
    void RegisterAsyncMetadataHandler(MessageReceivedAsyncHandler handler);
    void RegisterMetadataHandler(MessageReceivedHandler handler);
    void UnregisterMetadataHandler(MessageReceivedHandler handler);
    void UnregisterAsyncMetadataHandler(MessageReceivedAsyncHandler handler);

    // message handlers for incoming messages with only metadata
    Task ReceiveMessage(MessageSignal messageSignal);
}

class MessengerLog {}

public class MessageHub: IMessageHub
{
    private readonly List<Func<MessageThread, Task>> _messageHandlers = new List<Func<MessageThread, Task>>();
    private readonly List<Func<MessageThread, Task>> _metadataHandlers = new List<Func<MessageThread, Task>>();
    
    private static readonly Logger<MessengerLog> _logger = Logger<MessengerLog>.For();

    private readonly Dictionary<Delegate, Func<MessageThread, Task>> _messageHandlerMappings = 
        new Dictionary<Delegate, Func<MessageThread, Task>>();

    private readonly Dictionary<Delegate, Func<MessageThread, Task>> _metadataHandlerMappings = 
        new Dictionary<Delegate, Func<MessageThread, Task>>();

    public static async Task<string?> Send(string content, string participantId, object? metadata = null)
    {
        var outgoingMessage = new OutgoingMessage
        {
            Content = content,
            Metadata = metadata,
            ParticipantId = participantId,
            WorkflowId = AgentContext.WorkflowId,
            WorkflowType = AgentContext.WorkflowType,
            Agent = AgentContext.AgentName
        };

        if (Workflow.InWorkflow)
        {
            var success = await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.SendMessage(outgoingMessage),
                new SystemActivityOptions());
            return success;
        }
        else
        {
            var success = await SystemActivities.SendMessageStatic(outgoingMessage);
            return success;
        }

    }

    public void RegisterAsyncMessageHandler(MessageReceivedAsyncHandler handler)
    {
        // Convert the delegate type
        Func<MessageThread, Task> funcHandler = messageThread => handler(messageThread);
        
        if (!_messageHandlerMappings.ContainsKey(handler))
        {
            _messageHandlerMappings[handler] = funcHandler;
            _messageHandlers.Add(funcHandler);
        }
    }

    public void RegisterMessageHandler(MessageReceivedHandler handler)
    {
        // Wrap the synchronous handler to return a completed task
        Func<MessageThread, Task> funcHandler = messageThread => 
        {
            handler(messageThread);
            return Task.CompletedTask;
        };
        
        if (!_messageHandlerMappings.ContainsKey(handler))
        {
            _messageHandlerMappings[handler] = funcHandler;
            _messageHandlers.Add(funcHandler);
        }
    }
    
    public void UnregisterMessageHandler(MessageReceivedHandler handler)
    {
        if (_messageHandlerMappings.TryGetValue(handler, out var funcHandler))
        {
            _messageHandlers.Remove(funcHandler);
            _messageHandlerMappings.Remove(handler);
        }
    }
    
    public void UnregisterAsyncMessageHandler(MessageReceivedAsyncHandler handler)
    {
        if (_messageHandlerMappings.TryGetValue(handler, out var funcHandler))
        {
            _messageHandlers.Remove(funcHandler);
            _messageHandlerMappings.Remove(handler);
        }
    }

    public void RegisterAsyncMetadataHandler(MessageReceivedAsyncHandler handler)
    {
        // Convert the delegate type
        Func<MessageThread, Task> funcHandler = messageThread => handler(messageThread);
        
        if (!_metadataHandlerMappings.ContainsKey(handler))
        {
            _metadataHandlerMappings[handler] = funcHandler;
            _metadataHandlers.Add(funcHandler);
        }
    }

    public void RegisterMetadataHandler(MessageReceivedHandler handler)
    {
        // Wrap the synchronous handler to return a completed task
        Func<MessageThread, Task> funcHandler = messageThread => 
        {
            handler(messageThread);
            return Task.CompletedTask;
        };
        
        if (!_metadataHandlerMappings.ContainsKey(handler))
        {
            _metadataHandlerMappings[handler] = funcHandler;
            _metadataHandlers.Add(funcHandler);
        }
    }
    
    public void UnregisterMetadataHandler(MessageReceivedHandler handler)
    {
        if (_metadataHandlerMappings.TryGetValue(handler, out var funcHandler))
        {
            _metadataHandlers.Remove(funcHandler);
            _metadataHandlerMappings.Remove(handler);
        }
    }
    
    public void UnregisterAsyncMetadataHandler(MessageReceivedAsyncHandler handler)
    {
        if (_metadataHandlerMappings.TryGetValue(handler, out var funcHandler))
        {
            _metadataHandlers.Remove(funcHandler);
            _metadataHandlerMappings.Remove(handler);
        }
    }

    public async Task ReceiveMessage(MessageSignal messageSignal)
    {
        _logger.LogInformation($"Received Signal Message: {JsonSerializer.Serialize(messageSignal)}");

        var messageThread = new MessageThread {
            WorkflowId = AgentContext.WorkflowId,
            WorkflowType = AgentContext.WorkflowType,
            Agent = AgentContext.AgentName,
            ThreadId = messageSignal.Payload.ThreadId,
            ParticipantId = messageSignal.Payload.ParticipantId,
            LatestMessage = new Message {
                Content = messageSignal.Payload.Content,
                Metadata = messageSignal.Payload.Metadata,
            }
        };


        // If content is null, call metadata handlers; otherwise call message handlers
        if (string.IsNullOrEmpty(messageSignal.Payload.Content?.Trim()))
        {
            _logger.LogInformation($"New Metadata Message received: {JsonSerializer.Serialize(messageThread)}");
            _logger.LogInformation($"Informing {_metadataHandlers.Count} metadata handlers");

            // Call all metadata handlers
            foreach (var handler in _metadataHandlers.ToList())
            {
                await handler(messageThread);
            }
        }
        else
        {
            _logger.LogInformation($"New Message received: {JsonSerializer.Serialize(messageThread)}");
            _logger.LogInformation($"Informing {_messageHandlers.Count} message handlers");
            // Call all message handlers
            foreach (var handler in _messageHandlers.ToList())
            {
                await handler(messageThread);
            }
        }
    }

}