using System.Text.Json;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Server;

namespace XiansAi.Messaging;

public interface IMessageThread
{
    Task<string?> SendChat(string content, object? data = null);
    Task<string?> SendData(object data, string? content = null);
    Task<string?> SendHandoff(Type workflowType, string? message = null, object? metadata = null);

    Task<string?> GetAuthorization();
}

public class Message
{
    public required string Content { get; set; }
    public required object? Data { get; set; }
}


public class MessageThread : IMessageThread
{
    public required string ParticipantId { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
    public required string Agent { get; set; }
    public string? ThreadId { get; set; }
    public string? Authorization { get; set; }
    public required Message LatestMessage { get; set; }

    private readonly ILogger<MessageThread> _logger;

    public MessageThread()
    {
        _logger = Globals.LogFactory.CreateLogger<MessageThread>();
    }


    public async Task<List<DbMessage>> GetThreadHistory(int page = 1, int pageSize = 10)
    {
        var history = await new ThreadHistoryService().GetMessageHistory(Agent, WorkflowType, ParticipantId, page, pageSize);
        return history;
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
            ParticipantId = ParticipantId,
            WorkflowId = WorkflowId,
            WorkflowType = WorkflowType,
            Agent = Agent
        };

        _logger.LogInformation("Sending message: {Message}", JsonSerializer.Serialize(outgoingMessage));

        if(Workflow.InWorkflow) {
            var success = await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.SendChatOrData(outgoingMessage, type),
                new SystemActivityOptions());
            return success;
        } else {
            var success = await SystemActivities.SendChatOrDataStatic(outgoingMessage, type);
            return success;
        }
    }


    private async Task<string?> Handoff(string userRequest, object? data, string? targetWorkflowType, string? targetWorkflowId)
    {
        if(string.IsNullOrEmpty(userRequest)) {
            throw new Exception("User request is required for handoff");
        }

        if(targetWorkflowId == null && targetWorkflowType == null) {
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
            Data = data
        };

        _logger.LogInformation("Handing over thread: {Message}", JsonSerializer.Serialize(outgoingMessage));

        if(Workflow.InWorkflow) {
            var success = await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.SendHandoff(outgoingMessage),
                new SystemActivityOptions());
            return success;
        } else {
            var success = await SystemActivities.SendHandoffStatic(outgoingMessage);
            return success;
        }
    }
    
     public async Task<string?> GetAuthorization()
    {
        try
        {
            if (!SecureApi.IsReady)
            {
                _logger.LogError("Secure API is not ready");
                return null;
            }
            var client = SecureApi.Instance.Client;
            var response = await client.GetAsync($"{PlatformConfig.APP_SERVER_URL}/api/agent/conversation/authorization/{Authorization}");
         
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get authorization. Status code: {StatusCode}", response.StatusCode);
                return null;
            }

            var token = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Empty response received from authorization endpoint");
                return null;
            }

            return token.Trim('"');
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving authorization for GUID: {Authorization}", Authorization);
            return null;
        }
    }
}
