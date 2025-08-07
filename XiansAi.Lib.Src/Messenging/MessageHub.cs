using System.Collections.Concurrent;
using System.Text.Json;
using Temporalio.Workflows;
using XiansAi.Logging;

namespace XiansAi.Messaging;

public delegate Task ConversationReceivedAsyncHandler(MessageThread conversation);
public delegate void ConversationReceivedHandler(MessageThread conversation);

public delegate Task FlowMessageReceivedAsyncHandler<T>(EventMetadata<T> metadata);
public delegate void FlowMessageReceivedHandler<T>(EventMetadata<T> metadata);

public interface IMessageHub
{
    // Flow message handlers
    void SubscribeAsyncFlowMessageHandler<T>(FlowMessageReceivedAsyncHandler<T> handler);
    void SubscribeFlowMessageHandler<T>(FlowMessageReceivedHandler<T> handler);
    void UnsubscribeAsyncFlowMessageHandler<T>(FlowMessageReceivedHandler<T> handler);
    void UnsubscribeFlowMessageHandler<T>(FlowMessageReceivedAsyncHandler<T> handler);

    // message handlers for incoming messages with text content
    void SubscribeAsyncChatHandler(ConversationReceivedAsyncHandler handler);
    void SubscribeChatHandler(ConversationReceivedHandler handler);
    void UnsubscribeChatHandler(ConversationReceivedHandler handler);
    void UnsubscribeAsyncChatHandler(ConversationReceivedAsyncHandler handler);

    // message handlers for incoming messages only data content
    void SubscribeAsyncDataHandler(ConversationReceivedAsyncHandler handler);
    void SubscribeDataHandler(ConversationReceivedHandler handler);
    void UnsubscribeDataHandler(ConversationReceivedHandler handler);
    void UnsubscribeAsyncDataHandler(ConversationReceivedAsyncHandler handler);

    // message handlers for incoming messages with only metadata
    Task ReceiveConversationChatOrData(MessageSignal messageSignal);
    Task ReceiveFlowMessage(EventSignal eventSignal);
}

class MessengerLog {}

public class MessageHub: IMessageHub
{
    private readonly ConcurrentBag<Func<MessageThread, Task>> _chatHandlers = new ConcurrentBag<Func<MessageThread, Task>>();
    private readonly ConcurrentBag<Func<MessageThread, Task>> _dataHandlers = new ConcurrentBag<Func<MessageThread, Task>>();
    private readonly ConcurrentBag<Func<EventMetadata, object?, Task>> _flowMessageHandlers = new ConcurrentBag<Func<EventMetadata, object?, Task>>();

    private static readonly Logger<MessengerLog> _logger = Logger<MessengerLog>.For();

    private readonly ConcurrentDictionary<Delegate, Func<MessageThread, Task>> _chatHandlerMappings = 
        new ConcurrentDictionary<Delegate, Func<MessageThread, Task>>();

    private readonly ConcurrentDictionary<Delegate, Func<MessageThread, Task>> _dataHandlerMappings = 
        new ConcurrentDictionary<Delegate, Func<MessageThread, Task>>();

    private readonly Dictionary<Delegate, Func<EventMetadata, object?, Task>> _handlerMappings =
        new Dictionary<Delegate, Func<EventMetadata, object?, Task>>();

    public static async Task<string?> SendConversationData(string participantId, string content, object? data = null, string? requestId = null, string? scope = null)
    {
        return await SendConversationChatOrData(MessageType.Data, participantId, content, data, requestId, scope);
    }
    public static async Task<string?> SendConversationChat(string participantId, string content, object? data = null, string? requestId = null, string? scope = null)
    {
        return await SendConversationChatOrData(MessageType.Chat, participantId, content, data, requestId, scope);
    }

    public static async Task SendFlowMessage(Type flowClassType, object? payload = null) {
        var targetWorkflowType = AgentContext.GetWorkflowTypeFor(flowClassType);
        var targetWorkflowId = AgentContext.GetSingletonWorkflowIdFor(flowClassType);

        try {
            var eventDto = new EventSignal
            {
                SourceWorkflowId = AgentContext.WorkflowId,
                SourceWorkflowType = AgentContext.WorkflowType,
                SourceAgent = AgentContext.AgentName,
                Payload = payload,
                TargetWorkflowType = targetWorkflowType,
                TargetWorkflowId = targetWorkflowId
            };

            if (Workflow.InWorkflow)
            {
                await Workflow.ExecuteLocalActivityAsync(
                    (SystemActivities a) => a.SendEvent(eventDto),
                    new SystemLocalActivityOptions());
            }
            else
            {
                await SystemActivities.SendEventStatic(eventDto);
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            throw;
        }
    }


    private static async Task<string?> SendConversationChatOrData(MessageType type, string participantId, string content, object? data = null, string? requestId = null, string? scope = null)
    {
        var outgoingMessage = new ChatOrDataRequest
        {
            Agent = AgentContext.AgentName,
            WorkflowId = AgentContext.WorkflowId,
            WorkflowType = AgentContext.WorkflowType,
            Text = content,
            Data = data,
            ParticipantId = participantId,
            RequestId = requestId,
            Scope = scope
        };

        if (Workflow.InWorkflow)
        {
            var success = await Workflow.ExecuteLocalActivityAsync(
                (SystemActivities a) => a.SendChatOrData(outgoingMessage, type),
                new SystemLocalActivityOptions());
            return success;
        }
        else
        {
            var success = await SystemActivities.SendChatOrDataStatic(outgoingMessage, type);
            return success;
        }

    }

    public void SubscribeAsyncFlowMessageHandler<T>(FlowMessageReceivedAsyncHandler<T> handler)
    {
        // Convert the delegate type with proper type casting
        Func<EventMetadata, object?, Task> funcHandler = (metadata, payload) =>
        {
            var typedPayload = payload != null ? CastPayload<T>(payload) : default;
            var typedMetadata = new EventMetadata<T>
            {
                SourceWorkflowId = metadata.SourceWorkflowId,
                SourceWorkflowType = metadata.SourceWorkflowType,
                SourceAgent = metadata.SourceAgent,
                Payload = typedPayload!
            };
            return handler(typedMetadata);
        };

        if (!_handlerMappings.ContainsKey(handler))
        {
            _handlerMappings[handler] = funcHandler;
            _flowMessageHandlers.Add(funcHandler);
        }
    }

    public void SubscribeFlowMessageHandler<T>(FlowMessageReceivedHandler<T> handler)
    {
        // Wrap the synchronous handler to return a completed task with proper type casting
        Func<EventMetadata, object?, Task> funcHandler = (metadata, payload) =>
        {
            var typedPayload = payload != null ? CastPayload<T>(payload) : default;
            var typedMetadata = new EventMetadata<T>
            {
                SourceWorkflowId = metadata.SourceWorkflowId,
                SourceWorkflowType = metadata.SourceWorkflowType,
                SourceAgent = metadata.SourceAgent,
                Payload = typedPayload!
            };
            handler(typedMetadata);
            return Task.CompletedTask;
        };

        if (!_handlerMappings.ContainsKey(handler))
        {
            _handlerMappings[handler] = funcHandler;
            _flowMessageHandlers.Add(funcHandler);
        }
    }

    public void UnsubscribeAsyncFlowMessageHandler<T>(FlowMessageReceivedHandler<T> handler)
    {
        if (_handlerMappings.TryGetValue(handler, out var funcHandler))
        {
            _handlerMappings.Remove(handler);
        }
    }

    public void UnsubscribeFlowMessageHandler<T>(FlowMessageReceivedAsyncHandler<T> handler)
    {
        if (_handlerMappings.TryGetValue(handler, out var funcHandler))
        {
            _handlerMappings.Remove(handler);
        }
    }
    public void SubscribeAsyncChatHandler(ConversationReceivedAsyncHandler handler)
    {
        // Convert the delegate type
        Func<MessageThread, Task> funcHandler = messageThread => handler(messageThread);
        
        if (_chatHandlerMappings.TryAdd(handler, funcHandler))
        {
            _chatHandlers.Add(funcHandler);
            _logger.LogInformation($"Registered async message handler: {handler.Method.Name}");
        }
        else
        {
            _logger.LogWarning($"Async message handler already registered: {handler.Method.Name}");
        }
    }

    public void SubscribeChatHandler(ConversationReceivedHandler handler)
    {
        // Wrap the synchronous handler to return a completed task
        Func<MessageThread, Task> funcHandler = messageThread => 
        {
            try
            {
                handler(messageThread);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in sync message handler {handler.Method.Name}: {ex.Message}", ex);
                return Task.CompletedTask;
            }
        };
        
        if (_chatHandlerMappings.TryAdd(handler, funcHandler))
        {
            _chatHandlers.Add(funcHandler);
            _logger.LogInformation($"Registered sync message handler: {handler.Method.Name}");
        }
        else
        {
            _logger.LogWarning($"Sync message handler already registered: {handler.Method.Name}");
        }
    }
    
    public void UnsubscribeChatHandler(ConversationReceivedHandler handler)
    {
        if (_chatHandlerMappings.TryRemove(handler, out var funcHandler))
        {
            // Note: ConcurrentBag doesn't support removal, so we'll mark handlers as removed
            // This is acceptable since we create new collections for iteration
            _logger.LogInformation($"Unregistered sync message handler: {handler.Method.Name}");
        }
        else
        {
            _logger.LogWarning($"Attempted to unregister non-existent sync message handler: {handler.Method.Name}");
        }
    }
    
    public void UnsubscribeAsyncChatHandler(ConversationReceivedAsyncHandler handler)
    {
        if (_chatHandlerMappings.TryRemove(handler, out var funcHandler))
        {
            _logger.LogInformation($"Unregistered async message handler: {handler.Method.Name}");
        }
        else
        {
            _logger.LogWarning($"Attempted to unregister non-existent async message handler: {handler.Method.Name}");
        }
    }

    public void SubscribeAsyncDataHandler(ConversationReceivedAsyncHandler handler)
    {
        // Convert the delegate type
        Func<MessageThread, Task> funcHandler = messageThread => handler(messageThread);
        
        if (_dataHandlerMappings.TryAdd(handler, funcHandler))
        {
            _dataHandlers.Add(funcHandler);
            _logger.LogInformation($"Registered async metadata handler: {handler.Method.Name}");
        }
        else
        {
            _logger.LogWarning($"Async metadata handler already registered: {handler.Method.Name}");
        }
    }

    public void SubscribeDataHandler(ConversationReceivedHandler handler)
    {
        // Wrap the synchronous handler to return a completed task
        Func<MessageThread, Task> funcHandler = messageThread => 
        {
            try
            {
                handler(messageThread);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in sync metadata handler {handler.Method.Name}: {ex.Message}", ex);
                return Task.CompletedTask;
            }
        };
        
        if (_dataHandlerMappings.TryAdd(handler, funcHandler))
        {
            _dataHandlers.Add(funcHandler);
            _logger.LogInformation($"Registered sync metadata handler: {handler.Method.Name}");
        }
        else
        {
            _logger.LogWarning($"Sync metadata handler already registered: {handler.Method.Name}");
        }
    }
    
    public void UnsubscribeDataHandler(ConversationReceivedHandler handler)
    {
        if (_dataHandlerMappings.TryRemove(handler, out var funcHandler))
        {
            _logger.LogInformation($"Unregistered sync metadata handler: {handler.Method.Name}");
        }
        else
        {
            _logger.LogWarning($"Attempted to unregister non-existent sync metadata handler: {handler.Method.Name}");
        }
    }
    
    public void UnsubscribeAsyncDataHandler(ConversationReceivedAsyncHandler handler)
    {
        if (_dataHandlerMappings.TryRemove(handler, out var funcHandler))
        {
            _logger.LogInformation($"Unregistered async metadata handler: {handler.Method.Name}");
        }
        else
        {
            _logger.LogWarning($"Attempted to unregister non-existent async metadata handler: {handler.Method.Name}");
        }
    }

    public async Task ReceiveConversationChatOrData(MessageSignal messageSignal)
    {
        _logger.LogDebug($"Received Signal Message: {JsonSerializer.Serialize(messageSignal)}");

        var messageType = Enum.Parse<MessageType>(messageSignal.Payload.Type);

        var messageThread = new MessageThread {
            WorkflowId = AgentContext.WorkflowId,
            WorkflowType = AgentContext.WorkflowType,
            Agent = AgentContext.AgentName,
            ThreadId = messageSignal.Payload.ThreadId,
            ParticipantId = messageSignal.Payload.ParticipantId,
            Authorization = messageSignal.Payload.Authorization,
            LatestMessage = new () {
                Content = messageSignal.Payload.Text,
                Data = messageSignal.Payload.Data,
                Type = messageType,
                RequestId = messageSignal.Payload.RequestId,
                Hint = messageSignal.Payload.Hint,
                Scope = messageSignal.Payload.Scope
            }
        };

        // Determine which handlers to call based on the message type
        if (messageType == MessageType.Data)
        {
            await ProcessConversationHandlers(_dataHandlerMappings.Values, messageThread);
        }
        else
        {
            await ProcessConversationHandlers(_chatHandlerMappings.Values, messageThread);
        }
    }

    private async Task ProcessConversationHandlers(IEnumerable<Func<MessageThread, Task>> handlers, MessageThread messageThread)
    {
        var handlerList = handlers.ToList();
        _logger.LogDebug($"New message received: {JsonSerializer.Serialize(messageThread)}");
        _logger.LogDebug($"Informing {handlerList.Count} handlers");

        var handlerTasks = new List<Task>();

        foreach (var handler in handlerList)
        {
            var handlerTask = InvokeHandlerSafely(handler, messageThread);
            handlerTasks.Add(handlerTask);
        }

        try
        {
            // Wait for all handlers to complete
            await Task.WhenAll(handlerTasks);
            _logger.LogDebug($"All handlers completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"One or more handlers failed: {ex.Message}", ex);
            // Continue execution even if some handlers failed
        }
    }

    private async Task InvokeHandlerSafely(Func<MessageThread, Task> handler, MessageThread messageThread)
    {
        try
        {
            _logger.LogDebug($"Calling handler: {handler.Method.Name}");
            await handler(messageThread);
            _logger.LogDebug($"Completed handler: {handler.Method.Name}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in handler {handler.Method.Name}: {ex.Message}", ex);
            // Don't rethrow - continue with other handlers
        }
    }

    public static T CastPayload<T>(object obj)
    {
        if (obj == null)
        {
            return default!;
        }
        try {
            return JsonSerializer.Deserialize<T>(obj.ToString()!)!;
        }
        catch (Exception)
        {
            throw new InvalidOperationException($"Failed to cast event payload `{obj}` to `{typeof(T).Name}`");
        }
    }


    public async Task ReceiveFlowMessage(EventSignal obj)
    {
        var metadata = new EventMetadata
        {
            SourceWorkflowId = obj.SourceWorkflowId,
            SourceWorkflowType = obj.SourceWorkflowType,
            SourceAgent = obj.SourceAgent
        };
        // Call all handlers uniformly
        foreach (var handler in _flowMessageHandlers.ToList())
        {
            await handler(metadata, obj.Payload);
        }
    }
}