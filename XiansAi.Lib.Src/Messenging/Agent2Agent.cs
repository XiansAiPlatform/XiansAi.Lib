using Temporal;
using Temporalio.Workflows;

namespace XiansAi.Messaging;

/// <summary>
/// Interface for agent-to-agent communication within the XiansAi workflow system.
/// Provides methods for sending data and chat messages between workflow agents.
/// </summary>
interface IAgent2Agent {
    /// <summary>
    /// Sends data to another agent identified by workflow ID or type.
    /// </summary>
    /// <param name="workflowIdOrType">The target workflow ID or type identifier</param>
    /// <param name="data">The data object to send</param>
    /// <param name="methodName">The method name to invoke on the target agent</param>
    /// <param name="requestId">Optional request identifier for tracking</param>
    /// <param name="scope">Optional scope for the request</param>
    /// <param name="authorization">Optional authorization token</param>
    /// <param name="hint">Optional hint for processing</param>
    /// <param name="timeoutSeconds">Timeout in seconds (default: 300)</param>
    /// <returns>Response from the target agent, or null if no response</returns>
    public Task<MessageResponse> SendData(string workflowIdOrType, object data, string methodName, string? requestId = null, string? scope = null, string? authorization = null, string? hint = null, int timeoutSeconds = 300);
    
    /// <summary>
    /// Sends a chat message to another agent identified by workflow ID or type.
    /// </summary>
    /// <param name="workflowIdOrType">The target workflow ID or type identifier</param>
    /// <param name="message">The chat message to send</param>
    /// <param name="data">Optional additional data to include with the message</param>
    /// <param name="requestId">Optional request identifier for tracking</param>
    /// <param name="scope">Optional scope for the request</param>
    /// <param name="authorization">Optional authorization token</param>
    /// <param name="hint">Optional hint for processing</param>
    /// <param name="timeoutSeconds">Timeout in seconds (default: 300)</param>
    /// <returns>Response from the target agent, or null if no response</returns>
    public Task<MessageResponse> SendChat(string workflowIdOrType, string message, object? data = null, string? requestId = null, string? scope = null, string? authorization = null, string? hint = null, int timeoutSeconds = 300);
    
    /// <summary>
    /// Sends data to another agent identified by workflow type.
    /// </summary>
    /// <param name="targetWorkflowType">The target workflow type</param>
    /// <param name="data">The data object to send</param>
    /// <param name="methodName">The method name to invoke on the target agent</param>
    /// <param name="requestId">Optional request identifier for tracking</param>
    /// <param name="scope">Optional scope for the request</param>
    /// <param name="authorization">Optional authorization token</param>
    /// <param name="hint">Optional hint for processing</param>
    /// <param name="timeoutSeconds">Timeout in seconds (default: 300)</param>
    /// <returns>Response from the target agent, or null if no response</returns>
    public Task<MessageResponse> SendData(Type targetWorkflowType, object data, string methodName, string? requestId = null, string? scope = null, string? authorization = null, string? hint = null, int timeoutSeconds = 300);
    
    /// <summary>
    /// Sends a chat message to another agent identified by workflow type.
    /// </summary>
    /// <param name="targetWorkflowType">The target workflow type</param>
    /// <param name="message">The chat message to send</param>
    /// <param name="data">Optional additional data to include with the message</param>
    /// <param name="requestId">Optional request identifier for tracking</param>
    /// <param name="scope">Optional scope for the request</param>
    /// <param name="authorization">Optional authorization token</param>
    /// <param name="hint">Optional hint for processing</param>
    /// <param name="timeoutSeconds">Timeout in seconds (default: 300)</param>
    /// <returns>Response from the target agent, or null if no response</returns>
    public Task<MessageResponse> SendChat(Type targetWorkflowType, string message, object? data = null, string? requestId = null, string? scope = null, string? authorization = null, string? hint = null, int timeoutSeconds = 300);
}

/// <summary>
/// Provides agent-to-agent communication capabilities within the XiansAi workflow system.
/// This class enables workflows to send data and chat messages to other workflow agents,
/// supporting both direct workflow ID targeting and type-based singleton targeting.
/// </summary>
/// <remarks>
/// The Agent2Agent class automatically handles workflow identification and routing,
/// using the current workflow's ID as the participant identifier for all outgoing messages.
/// It supports both in-workflow and out-of-workflow execution contexts.
/// </remarks>
public class Agent2Agent : IAgent2Agent {

    /// <inheritdoc />
    public async Task<MessageResponse> SendData(string workflowIdOrType, object data, string methodName, string? requestId = null, string? scope = null, string? authorization = null, string? hint = null, int timeoutSeconds = 300)
    {
        var targetWorkflowId = new WorkflowIdentifier(workflowIdOrType).WorkflowId;
        var targetWorkflowTypeString = new WorkflowIdentifier(workflowIdOrType).WorkflowType;

        // Use the current workflow's id as the participant id
        var participantId = AgentContext.WorkflowId;

        return await new Agent2Agent().BotToBotMessage(MessageType.Data, participantId, methodName, data, targetWorkflowTypeString, targetWorkflowId, requestId, scope, authorization, hint, timeoutSeconds);

    }
    
    /// <inheritdoc />
    public async Task<MessageResponse> SendChat(string workflowIdOrType, string message, object? data = null, string? requestId = null, string? scope = null, string? authorization = null, string? hint = null, int timeoutSeconds = 300)
    {
        var targetWorkflowId = new WorkflowIdentifier(workflowIdOrType).WorkflowId;
        var targetWorkflowTypeString = new WorkflowIdentifier(workflowIdOrType).WorkflowType;

        // Use the current workflow's id as the participant id
        var participantId = AgentContext.WorkflowId;

        return await new Agent2Agent().BotToBotMessage(MessageType.Chat, participantId, message, data, targetWorkflowTypeString, targetWorkflowId, requestId, scope, authorization, hint, timeoutSeconds);

    }

    /// <inheritdoc />
     public async Task<MessageResponse> SendData(Type targetWorkflowType, object data, string methodName, string? requestId = null, string? scope = null, string? authorization = null, string? hint = null, int timeoutSeconds = 300)
    {
        var targetWorkflowId = WorkflowIdentifier.GetSingletonWorkflowIdFor(targetWorkflowType);
        var targetWorkflowTypeString = WorkflowIdentifier.GetWorkflowTypeFor(targetWorkflowType);

        // Use the current workflow's id as the participant id
        var participantId = AgentContext.WorkflowId;

        return await new Agent2Agent().BotToBotMessage(MessageType.Data, participantId, methodName, data, targetWorkflowTypeString, targetWorkflowId, requestId, scope, authorization, hint, timeoutSeconds);
    }

    /// <inheritdoc />
    public async Task<MessageResponse> SendChat(Type targetWorkflowType, string message, object? data = null, string? requestId = null, string? scope = null, string? authorization = null, string? hint = null, int timeoutSeconds = 300)
    {
        var targetWorkflowId = WorkflowIdentifier.GetSingletonWorkflowIdFor(targetWorkflowType);
        var targetWorkflowTypeString = WorkflowIdentifier.GetWorkflowTypeFor(targetWorkflowType);

        // Use the current workflow's id as the participant id
        var participantId = AgentContext.WorkflowId;

        return await new Agent2Agent().BotToBotMessage(MessageType.Chat, participantId, message, data, targetWorkflowTypeString, targetWorkflowId, requestId, scope, authorization, hint, timeoutSeconds);
    }

    /// <summary>
    /// Core method that handles the actual bot-to-bot message transmission.
    /// This method constructs the message payload and routes it through the appropriate execution context.
    /// </summary>
    /// <param name="type">The type of message (Data or Chat)</param>
    /// <param name="participantId">The ID of the sending participant (current workflow)</param>
    /// <param name="userRequest">The request content (method name for data, message text for chat)</param>
    /// <param name="data">Optional data payload to include with the message</param>
    /// <param name="targetWorkflowType">The target workflow type string</param>
    /// <param name="targetWorkflowId">The target workflow ID</param>
    /// <param name="requestId">Optional request identifier for tracking</param>
    /// <param name="scope">Optional scope context</param>
    /// <param name="authorization">Optional authorization token</param>
    /// <param name="hint">Optional processing hint</param>
    /// <param name="timeoutSeconds">Request timeout in seconds</param>
    /// <returns>A task that represents the asynchronous operation, containing the response or null</returns>
    /// <exception cref="Exception">Thrown when required parameters are missing or invalid</exception>
    /// <remarks>
    /// This method automatically detects whether it's running within a workflow context and uses
    /// the appropriate execution path (local activity for in-workflow, static method for out-of-workflow).
    /// </remarks>
    public async Task<MessageResponse> BotToBotMessage(
        MessageType type,
        string participantId,
        string userRequest, 
        object? data, 
        string? targetWorkflowType, 
        string? targetWorkflowId,
        string? requestId,
        string? scope,
        string? authorization,
        string? hint,
        int timeoutSeconds
    )
    {
        if(string.IsNullOrEmpty(userRequest)) 
        {
            throw new Exception("User request is required for bot to bot messaging");
        }

        if (string.IsNullOrEmpty(targetWorkflowId) || string.IsNullOrEmpty(targetWorkflowType))
        {
            throw new Exception("Target workflowId or workflowType is required for bot to bot messaging");
        }

        var outgoingChatOrDataMessage = new ChatOrDataRequest
        {
            WorkflowId = targetWorkflowId,
            WorkflowType = targetWorkflowType,
            Type = type,
            Text = userRequest,
            Data = data,
            RequestId = requestId,
            Scope = scope,
            ParticipantId = participantId,
            Authorization=authorization,
            Hint = hint,
            Origin = AgentContext.WorkflowId
        };

        if (Workflow.InWorkflow) {
            var success = await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.SendBotToBotMessage(outgoingChatOrDataMessage, type, timeoutSeconds),
                new SystemActivityOptions());
            return success;
        } else {
            var success = await SystemActivities.SendBotToBotMessageStatic(outgoingChatOrDataMessage, type, timeoutSeconds);
            return success;
        }
    }
}