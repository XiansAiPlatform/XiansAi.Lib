using Temporalio.Workflows;

namespace XiansAi.Messaging;

public interface IMessageThread
{
    Task<List<HistoricalMessage>> GetThreadHistory(int page = 1, int pageSize = 10);
}

public class MessageThread : IMessageThread
{
    public required string ThreadId { get; set; }
    public required IncomingMessage IncomingMessage { get; set; }
    public required string ParticipantId { get; set; }
    public required string WorkflowId { get; set; }
    public string? HandedOverBy { get; set; }
    

    public async Task<List<HistoricalMessage>> GetThreadHistory(int page = 1, int pageSize = 10)
    {
        var history = await new ThreadHistoryService().GetMessageHistory(ThreadId, page, pageSize);
        return history;
    }

    public async Task<SendMessageResponse?> Respond(string content, string? metadata = null)
    {
        var outgoingMessage = new OutgoingMessage
        {
            ThreadId = ThreadId,
            Content = content,
            Metadata = metadata ?? IncomingMessage.Metadata,
            ParticipantId = ParticipantId,
            // if the message is handed over, we will use the handover workflow id
            WorkflowId = string.IsNullOrEmpty(HandedOverBy) ? WorkflowId : HandedOverBy,
            HandedOverTo = WorkflowId
        };

        var success = await Workflow.ExecuteActivityAsync(
            (SystemActivities a) => a.SendMessage(outgoingMessage),
            new SystemActivityOptions());

        return success;
    }


    public async Task<SendMessageResponse> Handover(string handoverTo, string content, string participantId, string? metadata = null)
    {

        var outgoingMessage = new OutgoingMessage
        {
            ThreadId = ThreadId,
            Content = content,
            Metadata = metadata,
            ParticipantId = participantId,
            WorkflowId = WorkflowId,
            // set the handover to the participant id of the new thread
            HandedOverTo = handoverTo
        };

        var success = await Workflow.ExecuteActivityAsync(
            (SystemActivities a) => a.SendMessage(outgoingMessage),
            new SystemActivityOptions());

        return success;
    }
}

public class IncomingMessage {
    public required string Content { get; set; }
    public required object Metadata { get; set; }
    public required string CreatedAt { get; set; }
    public required string CreatedBy { get; set; }
    public string? HandedOverBy { get; set; }
}

public class MessageSignal {
    public required string ThreadId { get; set; }
    public required string ParticipantId { get; set; }
    public required string Content { get; set; }
    public required object Metadata { get; set; }
    public required string CreatedAt { get; set; }
    public required string CreatedBy { get; set; }
    public string? HandedOverBy { get; set; }
}

public class OutgoingMessage
{
    public string? ThreadId { get; set; }
    public required string Content { get; set; }
    public object? Metadata { get; set; }
    public required string ParticipantId { get; set; }
    public required string WorkflowId { get; set; }
    public string? HandedOverTo { get; set; }
}


public class HistoricalMessage {
    public string Id { get; set; } = null!;
    public required string ThreadId { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public required string Direction { get; set; }
    public required string Content { get; set; }
    public string? Status { get; set; }
    public object? Metadata { get; set; }
    public List<MessageLogEvent>? Logs { get; set; }
}


public class MessageLogEvent
{
    public required DateTime Timestamp { get; set; }
    
    public required string Event { get; set; }
    
    public object? Details { get; set; }
}