using Temporal;
using Temporalio.Workflows;

namespace XiansAi.Messaging;

interface IAgent2User {
    /// <summary>
    /// Sends data to a participant using the current workflow as the sender.
    /// </summary>
    /// <param name="participantId">The ID of the participant to send data to</param>
    /// <param name="content">The content/message to send</param>
    /// <param name="data">The data object to send</param>
    /// <param name="requestId">Optional request ID for tracking</param>
    /// <param name="scope">Optional scope for the message</param>
    /// <returns>A task that represents the asynchronous operation, containing the message ID if successful</returns>
    public Task SendData(string participantId, string content, object data, string? requestId = null, string? scope = null);
    
    /// <summary>
    /// Sends a chat message to a participant using the current workflow as the sender.
    /// </summary>
    /// <param name="participantId">The ID of the participant to send the chat message to</param>
    /// <param name="content">The chat message content</param>
    /// <param name="data">Optional data object to include with the message</param>
    /// <param name="requestId">Optional request ID for tracking</param>
    /// <param name="scope">Optional scope for the message</param>
    /// <returns>A task that represents the asynchronous operation, containing the message ID if successful</returns>
    public  Task SendChat(string participantId, string content, object? data = null, string? requestId = null, string? scope = null);
    
    /// <summary>
    /// Sends a chat message to a participant while impersonating a different workflow as the sender.
    /// </summary>
    /// <param name="flowClassType">The workflow type to impersonate as the sender</param>
    /// <param name="participantId">The ID of the participant to send the chat message to</param>
    /// <param name="content">The chat message content</param>
    /// <param name="data">Optional data object to include with the message</param>
    /// <param name="requestId">Optional request ID for tracking</param>
    /// <param name="scope">Optional scope for the message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public Task SendChatAs(Type flowClassType, string participantId, string content, object? data = null, string? requestId = null, string? scope = null);
    
    /// <summary>
    /// Sends data to a participant while impersonating a different workflow as the sender.
    /// </summary>
    /// <param name="flowClassType">The workflow type to impersonate as the sender</param>
    /// <param name="participantId">The ID of the participant to send data to</param>
    /// <param name="content">The content/message to send</param>
    /// <param name="data">The data object to send</param>
    /// <param name="requestId">Optional request ID for tracking</param>
    /// <param name="scope">Optional scope for the message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public Task SendDataAs(Type flowClassType, string participantId, string content, object data, string? requestId = null, string? scope = null);


    /// <summary>
    /// Sends a chat message to a participant while impersonating a different workflow as the sender.
    /// </summary>
    /// <param name="workflowIdOrType">The workflow ID or type to impersonate as the sender</param>
    /// <param name="participantId">The ID of the participant to send the chat message to</param>
    /// <param name="content">The chat message content</param>
    /// <param name="data">Optional data object to include with the message</param>
    /// <param name="requestId">Optional request ID for tracking</param>
    /// <param name="scope">Optional scope for the message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public Task SendChatAs(string workflowIdOrType, string participantId, string content, object? data = null, string? requestId = null, string? scope = null);

    /// <summary>
    /// Sends data to a participant while impersonating a different workflow as the sender.
    /// </summary>
    /// <param name="workflowIdOrType">The workflow ID or type to impersonate as the sender</param>
    /// <param name="participantId">The ID of the participant to send data to</param>
    /// <param name="content">The content/message to send</param>
    /// <param name="data">The data object to send</param>
    /// <param name="requestId">Optional request ID for tracking</param>
    /// <param name="scope">Optional scope for the message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public Task SendDataAs(string workflowIdOrType, string participantId, string content, object data, string? requestId = null, string? scope = null);
}

public class Agent2User : IAgent2User {

    /// <inheritdoc />
    public async Task SendChatAs(string workflowTypeOrId, string participantId, string content, object? data = null, string? requestId = null, string? scope = null)
    {
        var workflowId = new WorkflowIdentifier(workflowTypeOrId).WorkflowId;
        var workflowType = new WorkflowIdentifier(workflowTypeOrId).WorkflowType;
        await SendConversationChatOrData(workflowId, workflowType, MessageType.Chat, participantId, content, data, requestId, scope);
    }
    
    /// <inheritdoc />
    public async Task SendDataAs(string workflowIdOrType, string participantId, string content, object data, string? requestId = null, string? scope = null)
    {
        var workflowId = new WorkflowIdentifier(workflowIdOrType).WorkflowId;
        var workflowType = new WorkflowIdentifier(workflowIdOrType).WorkflowType;
        await SendConversationChatOrData(workflowId, workflowType, MessageType.Data, participantId, content, data, requestId, scope);
    }

    /// <inheritdoc />
    public async Task SendData(string participantId, string? content, object? data, string? requestId = null, string? scope = null)
    {
        var workflowId = AgentContext.WorkflowId;
        var workflowType = AgentContext.WorkflowType;
        await SendConversationChatOrData(workflowId, workflowType, MessageType.Data, participantId, content, data, requestId, scope);
    }
    
    /// <inheritdoc />
    public async Task SendChat(string participantId, string? content, object? data = null, string? requestId = null, string? scope = null)
    {
        var workflowId = AgentContext.WorkflowId;
        var workflowType = AgentContext.WorkflowType;
        await SendConversationChatOrData(workflowId, workflowType, MessageType.Chat, participantId, content, data, requestId, scope);
    }

    /// <inheritdoc />
    public async Task SendChatAs(Type flowClassType, string participantId, string content, object? data = null, string? requestId = null, string? scope = null) {
        var workflowId = WorkflowIdentifier.GetWorkflowIdFor(flowClassType);
        var workflowType = WorkflowIdentifier.GetWorkflowTypeFor(flowClassType);
        await SendConversationChatOrData(workflowId, workflowType, MessageType.Chat, participantId, content, data, requestId, scope);
    }

    /// <inheritdoc />
    public async Task SendDataAs(Type flowClassType, string participantId, string? content, object data, string? requestId = null, string? scope = null) {
        var workflowId = WorkflowIdentifier.GetWorkflowIdFor(flowClassType);
        var workflowType = WorkflowIdentifier.GetWorkflowTypeFor(flowClassType);
        await SendConversationChatOrData(workflowId, workflowType, MessageType.Data, participantId, content, data, requestId, scope);
    }

    /// <summary>
    /// Internal helper method that handles the actual sending of chat or data messages.
    /// </summary>
    /// <param name="workflowId">The workflow ID to use as the sender</param>
    /// <param name="workflowType">The workflow type to use as the sender</param>
    /// <param name="type">The type of message (Chat or Data)</param>
    /// <param name="participantId">The ID of the participant to send the message to</param>
    /// <param name="content">The message content</param>
    /// <param name="data">Optional data object to include</param>
    /// <param name="requestId">Optional request ID for tracking</param>
    /// <param name="scope">Optional scope for the message</param>
    /// <returns>A task that represents the asynchronous operation, containing the message ID if successful</returns>
    private static async Task<string?> SendConversationChatOrData(string workflowId, string workflowType, MessageType type, string participantId, string? content, object? data = null, string? requestId = null, string? scope = null)
    {

        var outgoingMessage = new ChatOrDataRequest
        {
            WorkflowId = workflowId,
            WorkflowType = workflowType,
            Text = content,
            Data = data,
            ParticipantId = participantId,
            RequestId = requestId ?? Guid.NewGuid().ToString(),
            Scope = scope
        };

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

}