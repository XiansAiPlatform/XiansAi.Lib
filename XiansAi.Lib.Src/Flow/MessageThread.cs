using Temporalio.Workflows;

namespace XiansAi.Flow;
public class MessageThread
{
    public required string ThreadId { get; set; }
    public required IncomingMessage IncomingMessage { get; set; }
    public required string ParticipantId { get; set; }
    public required string WorkflowId { get; set; }


    public async Task<List<IncomingMessage>> GetHistory(int page = 1, int pageSize = 10)
    {
        var history = await Workflow.ExecuteActivityAsync(
            (SystemActivities a) => a.GetMessageHistory(ThreadId, page, pageSize),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(60) });

        return history;
    }

    public async Task<bool> Respond(string content, string? metadata = null)
    {
        var outgoingMessage = new OutgoingMessage
        {
            ThreadId = ThreadId,
            Content = content,
            Metadata = metadata ?? IncomingMessage.Metadata,
            ParticipantId = ParticipantId,
            WorkflowId = WorkflowId
        };

        var success = await Workflow.ExecuteActivityAsync(
            (SystemActivities a) => a.SendMessage(outgoingMessage),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(60) });

        return success;
    }
}

public class IncomingMessage {
    public required string Content { get; set; }
    public required object Metadata { get; set; }
    public required string CreatedAt { get; set; }
    public required string CreatedBy { get; set; }
}

public class MessageSignal {
    public required string ThreadId { get; set; }
    public required string ParticipantId { get; set; }
    public required string Content { get; set; }
    public required object Metadata { get; set; }
    public required string CreatedAt { get; set; }
    public required string CreatedBy { get; set; }
}

public class OutgoingMessage
{
    public required string ThreadId { get; set; }
    public required string Content { get; set; }
    public required object Metadata { get; set; }
    public required string ParticipantId { get; set; }
    public required string WorkflowId { get; set; }
}