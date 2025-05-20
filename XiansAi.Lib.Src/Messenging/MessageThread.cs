using System.Text.Json;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace XiansAi.Messaging;

public interface IMessageThread
{
    Task<string?> Respond(string content, string? metadata = null);
    Task<string?> Handoff(string userRequest, string workflowType, string workflowId);
    Task<string?> Handoff(string userRequest, Type workflowType);
}

public class MessageThread : IMessageThread
{
    public required string ParticipantId { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
    public required string Agent { get; set; }
    public string? QueueName { get; set; }
    public string? ThreadId { get; set; }
    public object? Metadata { get; set; }
    public string? LatestContent { get; set; }

    private readonly ILogger<MessageThread> _logger;

    public MessageThread()
    {
        _logger = Globals.LogFactory.CreateLogger<MessageThread>();
    }


    public async Task<List<HistoricalMessage>> GetThreadHistory(int page = 1, int pageSize = 10)
    {
        var history = await new ThreadHistoryService().GetMessageHistory(Agent, ParticipantId, page, pageSize);
        return history;
    }

    public async Task<string?> Respond(string content, string? metadata = null)
    {
        var outgoingMessage = new OutgoingMessage
        {
            Content = content,
            Metadata = metadata,
            ParticipantId = ParticipantId,
            WorkflowId = WorkflowId,
            WorkflowType = WorkflowType,
            Agent = Agent,
            QueueName = QueueName
        };

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

    public async Task<string?> Handoff(string userRequest, Type workflowType)
    {
        var workflowId = AgentContext.GetSingletonWorkflowIdFor(workflowType);
        var workflowTypeString = AgentContext.GetWorkflowTypeFor(workflowType);
        return await Handoff(userRequest, workflowTypeString, workflowId);
    }

    public async Task<string?> Handoff(string userRequest, string workflowType, string workflowId)
    {
        var outgoingMessage = new HandoverMessage
        {
            WorkflowId = workflowId,
            WorkflowType = workflowType,
            Agent = Agent,
            ThreadId = ThreadId ?? throw new Exception("ThreadId is required for handover"),
            ParticipantId = ParticipantId,
            FromWorkflowType = WorkflowType,
            UserRequest = userRequest
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
