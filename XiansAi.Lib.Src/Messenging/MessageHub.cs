using System.Net.Mime;
using System.Text.Json;
using Temporalio.Workflows;
using XiansAi.Logging;

namespace XiansAi.Messaging;

public delegate Task MessageReceivedAsyncHandler(MessageThread messageThread);
public delegate void MessageReceivedHandler(MessageThread messageThread);

public interface IMessageHub
{
    void RegisterAsyncHandler(MessageReceivedAsyncHandler handler);
    void RegisterHandler(MessageReceivedHandler handler);
    void UnregisterHandler(MessageReceivedHandler handler);
    void UnregisterAsyncHandler(MessageReceivedAsyncHandler handler);
    Task ReceiveMessage(MessageSignal messageSignal);
}

class MessengerLog {}

public class MessageHub: IMessageHub
{
    private readonly List<Func<MessageThread, Task>> _messageHandlers = new List<Func<MessageThread, Task>>();
    
    private static readonly Logger<MessengerLog> _logger = Logger<MessengerLog>.For();

    private readonly Dictionary<Delegate, Func<MessageThread, Task>> _handlerMappings = 
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

    public void RegisterAsyncHandler(MessageReceivedAsyncHandler handler)
    {
        // Convert the delegate type
        Func<MessageThread, Task> funcHandler = messageThread => handler(messageThread);
        
        if (!_handlerMappings.ContainsKey(handler))
        {
            _handlerMappings[handler] = funcHandler;
            _messageHandlers.Add(funcHandler);
        }
    }

    public void RegisterHandler(MessageReceivedHandler handler)
    {
        // Wrap the synchronous handler to return a completed task
        Func<MessageThread, Task> funcHandler = messageThread => 
        {
            handler(messageThread);
            return Task.CompletedTask;
        };
        
        if (!_handlerMappings.ContainsKey(handler))
        {
            _handlerMappings[handler] = funcHandler;
            _messageHandlers.Add(funcHandler);
        }
    }
    
    public void UnregisterHandler(MessageReceivedHandler handler)
    {
        if (_handlerMappings.TryGetValue(handler, out var funcHandler))
        {
            _messageHandlers.Remove(funcHandler);
            _handlerMappings.Remove(handler);
        }
    }
    
    public void UnregisterAsyncHandler(MessageReceivedAsyncHandler handler)
    {
        if (_handlerMappings.TryGetValue(handler, out var funcHandler))
        {
            _messageHandlers.Remove(funcHandler);
            _handlerMappings.Remove(handler);
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

        _logger.LogInformation($"New MessageThread created: {JsonSerializer.Serialize(messageThread)}");

        
        // Call all handlers uniformly
        foreach (var handler in _messageHandlers.ToList())
        {
            await handler(messageThread);
        }
    }

}