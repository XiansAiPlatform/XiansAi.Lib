using System.Text.Json;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace XiansAi.Messaging;

public interface IMessageThread
{
    Task<List<HistoricalMessage>> GetThreadHistory(int page = 1, int pageSize = 10);
}

public class MessageThread : IMessageThread
{
    public required string ParticipantId { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
    public string? ParentWorkflowId { get; set; }
    public string? Agent { get; set; }
    public string? QueueName { get; set; }
    public string? Assignment { get; set; }

    private readonly ILogger<MessageThread> _logger;

    public MessageThread()
    {
        _logger = Globals.LogFactory.CreateLogger<MessageThread>();
    }


    public async Task<List<HistoricalMessage>> GetThreadHistory(int page = 1, int pageSize = 10)
    {
        var history = await new ThreadHistoryService().GetMessageHistory(WorkflowId, ParticipantId, page, pageSize);
        return history;
    }

    public async Task<SendMessageResponse?> Respond(string content, string? metadata = null)
    {
        if (ParentWorkflowId != null)
        {
            var outgoingMessage = new HandoverMessage
            {
                Content = content,
                Metadata = metadata,
                ParticipantId = ParticipantId,
                WorkflowId = WorkflowId,
                WorkflowType = WorkflowType,
                ParentWorkflowId = ParentWorkflowId
            };
            var success = await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.SendHandOverResponse(outgoingMessage),
                new SystemActivityOptions());

            return success;
        }
        else
        {
            var outgoingMessage = new OutgoingMessage
            {
                Content = content,
                Metadata = metadata,
                ParticipantId = ParticipantId,
                WorkflowId = WorkflowId,
                WorkflowType = WorkflowType
            };
            var success = await Workflow.ExecuteActivityAsync(
             (SystemActivities a) => a.SendMessage(outgoingMessage),
             new SystemActivityOptions());

            return success;
        }
    }

    public async Task<SendMessageResponse> StartAndHandover(string handoverWorkflowType, string content, string participantId, object? metadata = null)
    {

        var outgoingMessage = new StartAndHandoverMessage
        {
            Content = content,
            Metadata = metadata,
            ParticipantId = participantId,
            WorkflowId = WorkflowId,
            WorkflowType = WorkflowType,
            WorkflowTypeToStart = handoverWorkflowType,
            ParentWorkflowId = WorkflowId, // same as the current workflow id
            // optional fields
            Agent = Agent,
            QueueName = QueueName,
            Assignment = Assignment
        };

        _logger.LogInformation($"Start and handover message: {JsonSerializer.Serialize(outgoingMessage)}");

        var success = await new SystemActivities().StartAndHandoverMessage(outgoingMessage);
        return success;
    }
    public async Task<SendMessageResponse> Handover(string childWorkflowId, string content, string participantId, object? metadata = null)
    {

        var outgoingMessage = new HandoverMessage
        {
            Content = content,
            Metadata = metadata,
            ParticipantId = participantId,
            WorkflowId = WorkflowId,
            WorkflowType = WorkflowType,
            ChildWorkflowId = childWorkflowId,
            ParentWorkflowId = WorkflowId // same as the current workflow id
        };

        _logger.LogInformation($"Handover message: {JsonSerializer.Serialize(outgoingMessage)}");

        var success = await new SystemActivities().HandOverMessage(outgoingMessage);
        return success;
    }
}

public class IncomingMessage
{
    public required string Content { get; set; }
    public required object Metadata { get; set; }
    //public string? ParentWorkflowId { get; set; }
}

public class MessageSignal
{
    public required string ParticipantId { get; set; }
    public required string Content { get; set; }
    public required object Metadata { get; set; }
    public string? ParentWorkflowId { get; set; }
}

public class OutgoingMessage
{
    public required string Content { get; set; }
    public object? Metadata { get; set; }
    public required string ParticipantId { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }

}

public class HandoverResponseMessage : OutgoingMessage
{    
    public string? ParentWorkflowId { get; set; }
}

public class HandoverMessage : OutgoingMessage
{    
    public string? ChildWorkflowId { get; set; }
    public required string ParentWorkflowId { get; set; }
}

public class StartAndHandoverMessage : HandoverMessage
{
    public required string WorkflowTypeToStart { get; set; }
    public string? Agent { get; set; }
    public string? QueueName { get; set; }
    public string? Assignment { get; set; }
}

public class HistoricalMessage
{
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