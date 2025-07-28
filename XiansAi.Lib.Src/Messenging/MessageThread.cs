using System.Text.Json;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using System.Text.Json.Serialization;

namespace XiansAi.Messaging;
public interface IMessageThread
{
    Task<string?> SendChat(string content, object? data = null);
    Task<string?> SendData(object data, string? content = null);
    Task<string?> SendHandoff(Type workflowType, string? message = null, object? metadata = null);

}

public class Message
{
    [JsonPropertyName("content")]
    public required string Content { get; set; }
    [JsonPropertyName("data")]
    public required object? Data { get; set; }
    [JsonPropertyName("type")]
    public required MessageType Type { get; set; }
    [JsonPropertyName("request_id")]
    public required string RequestId { get; set; }
    [JsonPropertyName("hint")]
    public required string Hint { get; set; }
    [JsonPropertyName("scope")]
    public required string Scope { get; set; }
    [JsonPropertyName("origin")]
    public string? Origin { get; set; }
}


public class MessageThread : IMessageThread
{
    [JsonPropertyName("participant_id")]
    public required string ParticipantId { get; set; }
    [JsonPropertyName("workflow_id")]
    public required string WorkflowId { get; set; }
    [JsonPropertyName("workflow_type")]
    public required string WorkflowType { get; set; }
    [JsonPropertyName("agent")]
    public required string Agent { get; set; }
    [JsonPropertyName("thread_id")]
    public string? ThreadId { get; set; }
    [JsonPropertyName("authorization")]
    public string? Authorization { get; set; }
    [JsonPropertyName("latest_message")]
    public required Message LatestMessage { get; set; }
    [JsonIgnore]
    private readonly ILogger<MessageThread> _logger;
    public List<DbMessage>? History { get; set; }

    public MessageThread()
    {
        _logger = Globals.LogFactory.CreateLogger<MessageThread>();
    }


    public async Task<List<DbMessage>> FetchThreadHistory(int page = 1, int pageSize = 10)
    {
        if (History == null || History.Count == 0)
        {
            History = await new ThreadHistoryService().GetMessageHistory(WorkflowType, ParticipantId, page, pageSize);
        }
        return History;
    }


    public async Task<string?> SendData(object data, string? content = null)
    {
        _logger.LogDebug("Sending data message: {Content}", content);
        return await SendChatOrData(content, data, MessageType.Data);
    }

    public async Task<string?> SendChat(string content, object? data = null)
    {
        _logger.LogDebug("Sending chat message: {Content}", content);
        return await SendChatOrData(content, data, MessageType.Chat);
    }

    public async Task<string?> SendHandoff(Type targetWorkflowType, string? message = null, object? data = null)
    {
        message ??= LatestMessage.Content ?? throw new Exception("User request is required for handoff");
        var workflowId = AgentContext.GetSingletonWorkflowIdFor(targetWorkflowType);
        var workflowTypeString = AgentContext.GetWorkflowTypeFor(targetWorkflowType);
        data ??= LatestMessage.Data;
        return await Handoff(message, data, workflowTypeString, workflowId);
    }


    public async Task<string?> ForwardMessage(Type targetWorkflowType, string? message = null, object? data = null)
    {
        message ??= LatestMessage.Content ?? throw new Exception("User request is required for SendBotToBotMessage");
        var workflowId = AgentContext.GetSingletonWorkflowIdFor(targetWorkflowType);
        var workflowTypeString = AgentContext.GetWorkflowTypeFor(targetWorkflowType);
        var agent = AgentContext.AgentName;
        data ??= LatestMessage.Data;
        return await BotToBotMessage(message, data, workflowTypeString, workflowId, agent);
    }

    public async Task<string?> SendHandoff(string targetWorkflowId, string? message = null, object? data = null)
    {
        message ??= LatestMessage.Content ?? throw new Exception("User request is required for handoff");
        data ??= LatestMessage.Data;
        return await Handoff(message, data, null, targetWorkflowId);
    }

    private async Task<string?> SendChatOrData(string? content, object? data, MessageType type)
    {
        var outgoingMessage = new ChatOrDataRequest
        {
            Text = content,
            Data = data,
            RequestId = LatestMessage.RequestId,
            Scope = LatestMessage.Scope,
            ParticipantId = ParticipantId,
            WorkflowId = WorkflowId,
            WorkflowType = WorkflowType,
            Agent = Agent
        };

        _logger.LogDebug("Sending message: {Message}", JsonSerializer.Serialize(outgoingMessage));

        if (Workflow.InWorkflow)
        {
            var success = await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.SendChatOrData(outgoingMessage, type),
                new SystemActivityOptions());
            return success;
        }
        else
        {
            var success = await SystemActivities.SendChatOrDataStatic(outgoingMessage, type);
            return success;
        }
    }


    private async Task<string?> Handoff(string userRequest, object? data, string? targetWorkflowType, string? targetWorkflowId)
    {
        if (string.IsNullOrEmpty(userRequest))
        {
            throw new Exception("User request is required for handoff");
        }

        if (targetWorkflowId == null && targetWorkflowType == null)
        {
            throw new Exception("Target workflowId or workflowType is required for handoff");
        }

        var outgoingMessage = new HandoffRequest
        {
            TargetWorkflowId = targetWorkflowId,
            TargetWorkflowType = targetWorkflowType,
            SourceAgent = Agent,
            SourceWorkflowId = WorkflowId,
            ThreadId = ThreadId ?? throw new Exception("ThreadId is required for handoff"),
            ParticipantId = ParticipantId,
            SourceWorkflowType = WorkflowType,
            Text = userRequest,
            Data = data,
            Authorization = Authorization
        };

        _logger.LogDebug("Handing over thread: {Message}", JsonSerializer.Serialize(outgoingMessage));

        if (Workflow.InWorkflow)
        {
            var success = await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.SendHandoff(outgoingMessage),
                new SystemActivityOptions());
            return success;
        }
        else
        {
            var success = await SystemActivities.SendHandoffStatic(outgoingMessage);
            return success;
        }
    }
    
    private async Task<string?> BotToBotMessage(string userRequest, object? data, string? targetWorkflowType, string? targetWorkflowId, string? agent)
    {
        if(string.IsNullOrEmpty(userRequest)) 
        {
            throw new Exception("User request is required for bot to bot messaging");
        }

        if (string.IsNullOrEmpty(targetWorkflowId) && string.IsNullOrEmpty(targetWorkflowType) && string.IsNullOrEmpty(agent))
        {
            throw new Exception("Target workflowId or workflowType or agent is required for bot to bot messaging");
        }

        if (string.IsNullOrEmpty(targetWorkflowId)) throw new Exception("WorkflowId is required");
        if (string.IsNullOrEmpty(targetWorkflowType)) throw new Exception("WorkflowType is required");
        if (string.IsNullOrEmpty(agent)) throw new Exception("Agent name is required");

        var outgoingChatOrDataMessage = new ChatOrDataRequest
        {
            WorkflowId = targetWorkflowId,
            WorkflowType = targetWorkflowType,
            Agent = agent,
            Text = userRequest,
            Data = data,
            RequestId = LatestMessage.RequestId,
            Scope = LatestMessage.Scope,
            ParticipantId = ParticipantId,
            Authorization=Authorization,
            Hint = LatestMessage.Hint,
            Origin = WorkflowId
        };

        _logger.LogDebug("Sending over thread: {Message}", JsonSerializer.Serialize(outgoingChatOrDataMessage));
        if (Workflow.InWorkflow) {
            var success = await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.SendBotToBotMessage(outgoingChatOrDataMessage),
                new SystemActivityOptions());
            return success;
        } else {
            var success = await SystemActivities.SendBotToBotMessageStatic(outgoingChatOrDataMessage);
            return success;
        }
    }
    
}
