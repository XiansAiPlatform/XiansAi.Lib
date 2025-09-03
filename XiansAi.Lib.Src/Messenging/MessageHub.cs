using System.Collections.Concurrent;
using System.Text.Json;
using Temporal;
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

}

class MessengerLog {}

public class MessageHub: IMessageHub
{
    public static Agent2User Agent2User() { return new Agent2User(); }
    public static Agent2Agent Agent2Agent() { return new Agent2Agent(); }

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

    public static async Task<TResult?> SendFlowUpdate<TResult>(Type flowClassType, string update, int timeoutSeconds, params object?[] args) 
    {
        return await UpdateService.SendUpdateWithStart<TResult>(flowClassType, update, timeoutSeconds, args);
    }

    public static async Task SendFlowMessage(Type flowClassType, object? payload = null) {
        var targetWorkflowType = WorkflowIdentifier.GetWorkflowTypeFor(flowClassType);
        var targetWorkflowId = WorkflowIdentifier.GetSingletonWorkflowIdFor(flowClassType);

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

    [Obsolete("Use Agent2Agent.SendChat or Agent2Agent.SendData instead")]
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

    [Obsolete("Use Agent2Agent.SendChat or Agent2Agent.SendData instead")]
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
    [Obsolete("Use Agent2Agent.SendChat or Agent2Agent.SendData instead")]
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
    [Obsolete("Use Agent2Agent.SendChat or Agent2Agent.SendData instead")]
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