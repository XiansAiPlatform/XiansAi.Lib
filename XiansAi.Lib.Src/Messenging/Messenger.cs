using System.Text.Json;
using Temporalio.Workflows;
using XiansAi.Logging;

namespace XiansAi.Messaging;

public delegate Task MessageReceivedAsyncHandler(MessageThread messageThread);
public delegate void MessageReceivedHandler(MessageThread messageThread);

public interface IMessenger
{
    void RegisterAsyncHandler(MessageReceivedAsyncHandler handler);
    void RegisterHandler(MessageReceivedHandler handler);
    void UnregisterHandler(MessageReceivedHandler handler);
    void UnregisterAsyncHandler(MessageReceivedAsyncHandler handler);
    Task ReceiveMessage(MessageSignal messageSignal);
}

class MessengerLog {}

public class Messenger: IMessenger
{
    private readonly List<Func<MessageThread, Task>> _handlers = new List<Func<MessageThread, Task>>();
    
    private static readonly Logger<MessengerLog> _logger = Logger<MessengerLog>.For();

    private readonly Dictionary<Delegate, Func<MessageThread, Task>> _handlerMappings = 
        new Dictionary<Delegate, Func<MessageThread, Task>>();

    public static async Task<string?> SendMessageAsync(string content, string participantId, object? metadata = null)
    {
        var agentContext = AgentContext.Instance;
        var outgoingMessage = new OutgoingMessage
        {
            Content = content,
            Metadata = metadata,
            ParticipantId = participantId,
            WorkflowId = agentContext.WorkflowId,
            WorkflowType = agentContext.WorkflowType,
            Agent = agentContext.Agent,
            QueueName = agentContext.QueueName,
            Assignment = agentContext.Assignment
        };

        var success = await Workflow.ExecuteActivityAsync(
            (SystemActivities a) => a.SendMessage(outgoingMessage),
            new SystemActivityOptions());

        return success;
    }

    public void RegisterAsyncHandler(MessageReceivedAsyncHandler handler)
    {
        // Convert the delegate type
        Func<MessageThread, Task> funcHandler = messageThread => handler(messageThread);
        
        if (!_handlerMappings.ContainsKey(handler))
        {
            _handlerMappings[handler] = funcHandler;
            _handlers.Add(funcHandler);
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
            _handlers.Add(funcHandler);
        }
    }
    
    public void UnregisterHandler(MessageReceivedHandler handler)
    {
        if (_handlerMappings.TryGetValue(handler, out var funcHandler))
        {
            _handlers.Remove(funcHandler);
            _handlerMappings.Remove(handler);
        }
    }
    
    public void UnregisterAsyncHandler(MessageReceivedAsyncHandler handler)
    {
        if (_handlerMappings.TryGetValue(handler, out var funcHandler))
        {
            _handlers.Remove(funcHandler);
            _handlerMappings.Remove(handler);
        }
    }

    public async Task ReceiveMessage(MessageSignal messageSignal)
    {
        _logger.LogInformation($"Received Signal Message: {JsonSerializer.Serialize(messageSignal)}");

        var agentContext = AgentContext.Instance;

        var messageThread = new MessageThread {
            ParticipantId = messageSignal.ParticipantId,
            WorkflowId = agentContext.WorkflowId,
            WorkflowType = agentContext.WorkflowType,
            ThreadId = messageSignal.ThreadId,
            Agent = agentContext.Agent,
            QueueName = agentContext.QueueName,
            Assignment = agentContext.Assignment,
            Metadata = messageSignal.Metadata,
            LatestContent = messageSignal.Content
        };

        _logger.LogInformation($"New MessageThread created: {JsonSerializer.Serialize(messageThread)}");

        
        // Call all handlers uniformly
        foreach (var handler in _handlers.ToList())
        {
            await handler(messageThread);
        }
    }

}