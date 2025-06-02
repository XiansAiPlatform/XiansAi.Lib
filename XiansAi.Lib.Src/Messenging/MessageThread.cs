using System.Text.Json;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace XiansAi.Messaging;

public interface IMessageThread
{
    Task<string?> Respond(string content, object metadata);
    Task<string?> Respond(string content);
    Task<string?> RespondWithMetadata(object metadata);
    Task<string?> Handoff(string userRequest, object? metadata, string workflowType, string workflowId);
    Task<string?> Handoff(Type workflowType, string? userRequest = null);
}

public class Message {
    public string? Content { get; set; }
    public object? Metadata { get; set; }
}

public class MessageThread : IMessageThread
{
    public required string ParticipantId { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
    public required string Agent { get; set; }
    public string? ThreadId { get; set; }
    public required Message LatestMessage { get; set; }

    private readonly ILogger<MessageThread> _logger;

    public MessageThread()
    {
        _logger = Globals.LogFactory.CreateLogger<MessageThread>();
    }


    public async Task<List<HistoricalMessage>> GetThreadHistory(int page = 1, int pageSize = 10)
    {
        var history = await new ThreadHistoryService().GetMessageHistory(Agent, WorkflowType, ParticipantId, page, pageSize);
        return history;
    }

    public async Task<string?> Respond(string content)
    {
        return await RespondInternal(content, null);
    }

    public async Task<string?> RespondWithMetadata(object metadata)
    {
        _logger.LogInformation("Sending message with metadata: {Metadata}", JsonSerializer.Serialize(metadata));
        return await RespondInternal(null, metadata);
    }

    public async Task<string?> Respond(string content, object metadata)
    {
        return await RespondInternal(content, metadata);
    }

    private async Task<string?> RespondInternal(string? content, object? metadata)
    {
        var outgoingMessage = new OutgoingMessage
        {
            Content = content,
            Metadata = metadata,
            ParticipantId = ParticipantId,
            WorkflowId = WorkflowId,
            WorkflowType = WorkflowType,
            Agent = Agent
        };

        _logger.LogInformation("Sending message: {Message}", JsonSerializer.Serialize(outgoingMessage));

        if(Workflow.InWorkflow) {
            var success = await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.SendMessage(outgoingMessage),
                new SystemActivityOptions());
            return success;
        } else {
            var success = await SystemActivities.SendMessageStatic(outgoingMessage);
            return success;
        }
    }

    public async Task<string?> Handoff(Type workflowType, string? message = null)
    {
        var userRequest = message ?? LatestMessage.Content ?? throw new Exception("User request is required for handoff");
        var workflowId = AgentContext.GetSingletonWorkflowIdFor(workflowType);
        var workflowTypeString = AgentContext.GetWorkflowTypeFor(workflowType);
        var metadata = LatestMessage.Metadata;
        return await Handoff(userRequest, metadata, workflowTypeString, workflowId);
    }

    public async Task<string?> Handoff(string userRequest, object? metadata, string workflowType, string workflowId)
    {
        var outgoingMessage = new HandoverMessage
        {
            WorkflowId = workflowId,
            WorkflowType = workflowType,
            Agent = Agent,
            ThreadId = ThreadId ?? throw new Exception("ThreadId is required for handover"),
            ParticipantId = ParticipantId,
            FromWorkflowType = WorkflowType,
            Content = userRequest,
            Metadata = metadata
        };

        _logger.LogInformation("Handing over thread: {Message}", JsonSerializer.Serialize(outgoingMessage));

        if(Workflow.InWorkflow) {
            var success = await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.HandOverThread(outgoingMessage),
                new SystemActivityOptions());
            return success;
        } else {
            var success = await SystemActivities.HandOverThreadStatic(outgoingMessage);
            return success;
        }
    }
}
