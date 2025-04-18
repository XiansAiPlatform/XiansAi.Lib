namespace XiansAi.Messaging;

public delegate Task MessageReceivedAsyncHandler(MessageThread messageThread);
public delegate void MessageReceivedHandler(MessageThread messageThread);

public interface IMessenger
{
    void RegisterAsyncHandler(MessageReceivedAsyncHandler handler);
    void RegisterHandler(MessageReceivedHandler handler);
    void UnregisterHandler(MessageReceivedHandler handler);
    void UnregisterAsyncHandler(MessageReceivedAsyncHandler handler);
    internal Task ReceiveMessage(MessageSignal messageSignal);
}

public class Messenger : IMessenger
{
    private readonly List<Func<MessageThread, Task>> _handlers = new List<Func<MessageThread, Task>>();
    private readonly string _workflowId;
    
    // Dictionary to keep track of handler references for unregistration
    private readonly Dictionary<Delegate, Func<MessageThread, Task>> _handlerMappings = 
        new Dictionary<Delegate, Func<MessageThread, Task>>();

    public Messenger(string workflowId)
    {
        _workflowId = workflowId;
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
        var incomingMessage = new IncomingMessage {
            Content = messageSignal.Content,
            Metadata = messageSignal.Metadata,
            CreatedAt = messageSignal.CreatedAt,
            CreatedBy = messageSignal.CreatedBy
        };

        var messageThread = new MessageThread {
            ThreadId = messageSignal.ThreadId,
            ParticipantId = messageSignal.ParticipantId,
            IncomingMessage = incomingMessage,
            WorkflowId = _workflowId
        };
        
        // Call all handlers uniformly
        foreach (var handler in _handlers.ToList())
        {
            await handler(messageThread);
        }
    }
}