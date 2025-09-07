using System.Text.Json;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using System.Text.Json.Serialization;
using Temporal;

namespace XiansAi.Messaging;
public interface IMessageThread
{
    Task<List<DbMessage>> FetchThreadHistory(int page = 1, int pageSize = 10);
    Task SendChat(string content, object? data = null);
    Task SendData(object data, string? content = null);
    Task<string?> SendHandoff(Type workflowType, string? message = null, object? metadata = null);
    Task<MessageResponse> ForwardMessage(Type targetWorkflowType, string? message = null, object? data = null, int timeoutSeconds = 60);
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
    public string? Hint { get; set; }
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
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

    [JsonPropertyName("skip_response")]
    public bool SkipResponse { get; set; } = false;

    public MessageThread()
    {
        _logger = Globals.LogFactory.CreateLogger<MessageThread>();
    }


    public async Task<List<DbMessage>> FetchThreadHistory(int page = 1, int pageSize = 10)
    {
        var scope = LatestMessage.Scope;
        return await new ThreadHistoryService().GetMessageHistory(WorkflowType, ParticipantId, scope, page, pageSize);;
    }

    public async Task SendData(object? data, string? content = null)
    {
        await new Agent2User().SendData(ParticipantId, content, data, LatestMessage.RequestId, LatestMessage.Scope);
    }

    public async Task SendChat(string content, object? data = null)
    {
        await new Agent2User().SendChat(ParticipantId, content, data, LatestMessage.RequestId, LatestMessage.Scope);
    }

    public async Task<string?> SendHandoff(Type targetWorkflowType, string? message = null, object? data = null)
    {
        message ??= LatestMessage.Content ?? throw new Exception("User request is required for handoff");
        var workflowId = WorkflowIdentifier.GetSingletonWorkflowIdFor(targetWorkflowType);
        var workflowTypeString = WorkflowIdentifier.GetWorkflowTypeFor(targetWorkflowType);
        data ??= LatestMessage.Data;
        return await Handoff(message, data, workflowTypeString, workflowId);
    }

    public async Task<MessageResponse> ForwardMessage(Type targetWorkflowType, string? message = null, object? data = null, int timeoutSeconds = 60)
    {
        message ??= LatestMessage.Content ?? throw new Exception("User request is required for SendBotToBotMessage");
        var targetWorkflowId = WorkflowIdentifier.GetSingletonWorkflowIdFor(targetWorkflowType);
        var targetWorkflowTypeString = WorkflowIdentifier.GetWorkflowTypeFor(targetWorkflowType);
        data ??= LatestMessage.Data;
        return await new Agent2Agent().BotToBotMessage(MessageType.Chat, ParticipantId, message, data, targetWorkflowTypeString, targetWorkflowId, LatestMessage.RequestId, LatestMessage.Scope, Authorization, LatestMessage.Hint, timeoutSeconds);
    }

    public async Task<string?> SendHandoff(string targetWorkflowId, string? message = null, object? data = null)
    {
        message ??= LatestMessage.Content ?? throw new Exception("User request is required for handoff");
        data ??= LatestMessage.Data;
        return await Handoff(message, data, null, targetWorkflowId);
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
            var success = await Workflow.ExecuteLocalActivityAsync(
                (SystemActivities a) => a.SendHandoff(outgoingMessage),
                new SystemLocalActivityOptions());
            return success;
        }
        else
        {
            var success = await SystemActivities.SendHandoffStatic(outgoingMessage);
            return success;
        }
    }
    
    
}
