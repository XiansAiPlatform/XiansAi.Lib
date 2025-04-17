namespace XiansAi.Flow;

public delegate Task MessageReceivedHandler(MessageThread messageThread);

public interface IMessenger
{
    void RegisterHandler(MessageReceivedHandler handler);
    void UnregisterHandler(MessageReceivedHandler handler);
}

public class Messenger : IMessenger
{
    private readonly List<MessageReceivedHandler> _handlers = new List<MessageReceivedHandler>();
    private readonly string _workflowId;

    public Messenger(string workflowId)
    {
        _workflowId = workflowId;
    }

    public void RegisterHandler(MessageReceivedHandler handler)
    {
        if (!_handlers.Contains(handler))
        {
            _handlers.Add(handler);
        }
    }
    
    public void UnregisterHandler(MessageReceivedHandler handler)
    {
        _handlers.Remove(handler);
    }

    internal async Task ReceiveMessage(MessageSignal messageSignal)
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
        
        // Call registered handler methods
        foreach (var handler in _handlers)
        {
            await handler(messageThread);
        }
    }
}