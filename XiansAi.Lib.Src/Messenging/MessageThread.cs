using Temporalio.Workflows;

namespace XiansAi.Messaging;

public interface IMessageThread
{
    Task<List<HistoricalMessage>> GetThreadHistory(int page = 1, int pageSize = 10);
}

public class MessageThread : IMessageThread
{
    public required IncomingMessage IncomingMessage { get; set; }
    public required string ParticipantId { get; set; }
    public required string WorkflowId { get; set; }
    public string? HandedOverBy { get; set; }
    

    public async Task<List<HistoricalMessage>> GetThreadHistory(int page = 1, int pageSize = 10)
    {
        var history = await new ThreadHistoryService().GetMessageHistory(WorkflowId, ParticipantId, page, pageSize);
        return history;
    }

    public async Task<SendMessageResponse?> Respond(string content, string? metadata = null)
    {
        var outgoingMessage = new OutgoingMessage
        {
            Content = content,
            Metadata = metadata ?? IncomingMessage.Metadata,
            ParticipantId = ParticipantId,
            // if the message is handed over, we will use the handover workflow id
            WorkflowIds = string.IsNullOrEmpty(HandedOverBy) ? [WorkflowId] : [HandedOverBy, WorkflowId]
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
            Content = content,
            Metadata = metadata,
            ParticipantId = participantId,
            WorkflowIds = [WorkflowId],
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
    public required string ParticipantId { get; set; }
    public required string Content { get; set; }
    public required object Metadata { get; set; }
    public required string CreatedAt { get; set; }
    public required string CreatedBy { get; set; }
    public string? HandedOverBy { get; set; }
}

public class OutgoingMessage
{
    public required string Content { get; set; }
    public object? Metadata { get; set; }
    public required string ParticipantId { get; set; }

    // if the message is handed over, we will use both the original and the handed over to workflow id
    public required string[] WorkflowIds { get; set; }
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