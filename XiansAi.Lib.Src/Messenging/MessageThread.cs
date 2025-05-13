using System.Text.Json;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace XiansAi.Messaging;

public interface IMessageThread
{
    Task<List<HistoricalMessage>> GetThreadHistory(int page = 1, int pageSize = 10);
    Task<string?> Respond(string content, string? metadata = null);
    Task<string?> HandoverTo(string userRequest, string workflowType, string workflowId);
}

public class MessageThread : IMessageThread
{
    public required string ParticipantId { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
    public required string Agent { get; set; }
    public string? QueueName { get; set; }
    public string? Assignment { get; set; }
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
            QueueName = QueueName,
            Assignment = Assignment
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

    /// <summary>
    /// Handover the thread to another workflow.
    /// </summary>
    /// <param name="userRequest">The user request.</param>
    /// <param name="workflowType">The workflow type.</param>
    /// <param name="workflowIdwithoutTenantPortion">The workflow id without the tenant portion.
    /// e.g. if "tenant:workflow-1234567890" is the workflow id, 
    /// then "workflow-1234567890" is the workflow id without the tenant portion.</param>
    public async Task<string?> HandoverTo(string userRequest, string workflowType, string workflowIdwithoutTenantPortion)
    {
        var outgoingMessage = new HandoverMessage
        {
            WorkflowId = workflowIdwithoutTenantPortion,
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

public class MessageSignal
{
    public required string Agent { get; set; }
    public required string ThreadId { get; set; }
    public required string ParticipantId { get; set; }
    public required string Content { get; set; }
    public required object Metadata { get; set; }
}

public class OutgoingMessage
{
    public required string ParticipantId { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
    public required string Agent { get; set; }
    public string? QueueName { get; set; }
    public string? Assignment { get; set; }
    public required string Content { get; set; }
    public object? Metadata { get; set; }
    public string? ThreadId { get; set; }
}


public class HandoverMessage 
{    
    public string? WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
    public required string Agent { get; set; }
    public required string ThreadId { get; set; }
    public required string ParticipantId { get; set; }
    public required string FromWorkflowType { get; set; }
    public string? UserRequest { get; set; }
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
    public required string ParticipantId { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
}
