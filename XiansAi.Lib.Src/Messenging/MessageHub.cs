using System.Collections.Concurrent;
using System.Text.Json;
using Temporalio.Workflows;
using XiansAi.Logging;

namespace XiansAi.Messaging;

public delegate Task MessageReceivedAsyncHandler(MessageThread messageThread);
public delegate void MessageReceivedHandler(MessageThread messageThread);

public interface IMessageHub
{
    // message handlers for incoming messages with content
    void RegisterAsyncMessageHandler(MessageReceivedAsyncHandler handler);
    void RegisterMessageHandler(MessageReceivedHandler handler);
    void UnregisterMessageHandler(MessageReceivedHandler handler);
    void UnregisterAsyncMessageHandler(MessageReceivedAsyncHandler handler);

    // message handlers for incoming messages with only content
    void RegisterAsyncMetadataHandler(MessageReceivedAsyncHandler handler);
    void RegisterMetadataHandler(MessageReceivedHandler handler);
    void UnregisterMetadataHandler(MessageReceivedHandler handler);
    void UnregisterAsyncMetadataHandler(MessageReceivedAsyncHandler handler);

    // message handlers for incoming messages with only metadata
    Task ReceiveMessage(MessageSignal messageSignal);
}

class MessengerLog {}

public class MessageHub: IMessageHub
{
    private readonly ConcurrentBag<Func<MessageThread, Task>> _messageHandlers = new ConcurrentBag<Func<MessageThread, Task>>();
    private readonly ConcurrentBag<Func<MessageThread, Task>> _metadataHandlers = new ConcurrentBag<Func<MessageThread, Task>>();
    
    private static readonly Logger<MessengerLog> _logger = Logger<MessengerLog>.For();

    private readonly ConcurrentDictionary<Delegate, Func<MessageThread, Task>> _messageHandlerMappings = 
        new ConcurrentDictionary<Delegate, Func<MessageThread, Task>>();

    private readonly ConcurrentDictionary<Delegate, Func<MessageThread, Task>> _metadataHandlerMappings = 
        new ConcurrentDictionary<Delegate, Func<MessageThread, Task>>();

    public static async Task<string?> Send(string content, string participantId, object? metadata = null)
    {
        var outgoingMessage = new OutgoingMessage
        {
            Content = content,
            Metadata = metadata,
            ParticipantId = participantId,
            WorkflowId = AgentContext.WorkflowId,
            WorkflowType = AgentContext.WorkflowType,
            Agent = AgentContext.AgentName
        };

        if (Workflow.InWorkflow)
        {
            var success = await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.SendMessage(outgoingMessage),
                new SystemActivityOptions());
            return success;
        }
        else
        {
            var success = await SystemActivities.SendMessageStatic(outgoingMessage);
            return success;
        }

    }

    public void RegisterAsyncMessageHandler(MessageReceivedAsyncHandler handler)
    {
        // Convert the delegate type
        Func<MessageThread, Task> funcHandler = messageThread => handler(messageThread);
        
        if (_messageHandlerMappings.TryAdd(handler, funcHandler))
        {
            _messageHandlers.Add(funcHandler);
            _logger.LogInformation($"Registered async message handler: {handler.Method.Name}");
        }
        else
        {
            _logger.LogWarning($"Async message handler already registered: {handler.Method.Name}");
        }
    }

    public void RegisterMessageHandler(MessageReceivedHandler handler)
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
        
        if (_messageHandlerMappings.TryAdd(handler, funcHandler))
        {
            _messageHandlers.Add(funcHandler);
            _logger.LogInformation($"Registered sync message handler: {handler.Method.Name}");
        }
        else
        {
            _logger.LogWarning($"Sync message handler already registered: {handler.Method.Name}");
        }
    }
    
    public void UnregisterMessageHandler(MessageReceivedHandler handler)
    {
        if (_messageHandlerMappings.TryRemove(handler, out var funcHandler))
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
    
    public void UnregisterAsyncMessageHandler(MessageReceivedAsyncHandler handler)
    {
        if (_messageHandlerMappings.TryRemove(handler, out var funcHandler))
        {
            _logger.LogInformation($"Unregistered async message handler: {handler.Method.Name}");
        }
        else
        {
            _logger.LogWarning($"Attempted to unregister non-existent async message handler: {handler.Method.Name}");
        }
    }

    public void RegisterAsyncMetadataHandler(MessageReceivedAsyncHandler handler)
    {
        // Convert the delegate type
        Func<MessageThread, Task> funcHandler = messageThread => handler(messageThread);
        
        if (_metadataHandlerMappings.TryAdd(handler, funcHandler))
        {
            _metadataHandlers.Add(funcHandler);
            _logger.LogInformation($"Registered async metadata handler: {handler.Method.Name}");
        }
        else
        {
            _logger.LogWarning($"Async metadata handler already registered: {handler.Method.Name}");
        }
    }

    public void RegisterMetadataHandler(MessageReceivedHandler handler)
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
        
        if (_metadataHandlerMappings.TryAdd(handler, funcHandler))
        {
            _metadataHandlers.Add(funcHandler);
            _logger.LogInformation($"Registered sync metadata handler: {handler.Method.Name}");
        }
        else
        {
            _logger.LogWarning($"Sync metadata handler already registered: {handler.Method.Name}");
        }
    }
    
    public void UnregisterMetadataHandler(MessageReceivedHandler handler)
    {
        if (_metadataHandlerMappings.TryRemove(handler, out var funcHandler))
        {
            _logger.LogInformation($"Unregistered sync metadata handler: {handler.Method.Name}");
        }
        else
        {
            _logger.LogWarning($"Attempted to unregister non-existent sync metadata handler: {handler.Method.Name}");
        }
    }
    
    public void UnregisterAsyncMetadataHandler(MessageReceivedAsyncHandler handler)
    {
        if (_metadataHandlerMappings.TryRemove(handler, out var funcHandler))
        {
            _logger.LogInformation($"Unregistered async metadata handler: {handler.Method.Name}");
        }
        else
        {
            _logger.LogWarning($"Attempted to unregister non-existent async metadata handler: {handler.Method.Name}");
        }
    }

    public async Task ReceiveMessage(MessageSignal messageSignal)
    {
        _logger.LogInformation($"Received Signal Message: {JsonSerializer.Serialize(messageSignal)}");

        var messageThread = new MessageThread {
            WorkflowId = AgentContext.WorkflowId,
            WorkflowType = AgentContext.WorkflowType,
            Agent = AgentContext.AgentName,
            ThreadId = messageSignal.Payload.ThreadId,
            ParticipantId = messageSignal.Payload.ParticipantId,
            LatestMessage = new Message {
                Content = messageSignal.Payload.Content,
                Metadata = messageSignal.Payload.Metadata,
            }
        };

        // If content is null, call metadata handlers; otherwise call message handlers
        if (string.IsNullOrEmpty(messageSignal.Payload.Content?.Trim()))
        {
            await ProcessHandlers(_metadataHandlerMappings.Values, messageThread, "metadata");
        }
        else
        {
            await ProcessHandlers(_messageHandlerMappings.Values, messageThread, "message");
        }
    }

    private async Task ProcessHandlers(IEnumerable<Func<MessageThread, Task>> handlers, MessageThread messageThread, string handlerType)
    {
        var handlerList = handlers.ToList();
        _logger.LogInformation($"New {handlerType} received: {JsonSerializer.Serialize(messageThread)}");
        _logger.LogInformation($"Informing {handlerList.Count} {handlerType} handlers");

        var handlerTasks = new List<Task>();

        foreach (var handler in handlerList)
        {
            var handlerTask = InvokeHandlerSafely(handler, messageThread, handlerType);
            handlerTasks.Add(handlerTask);
        }

        try
        {
            // Wait for all handlers to complete
            await Task.WhenAll(handlerTasks);
            _logger.LogInformation($"All {handlerType} handlers completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"One or more {handlerType} handlers failed: {ex.Message}", ex);
            // Continue execution even if some handlers failed
        }
    }

    private async Task InvokeHandlerSafely(Func<MessageThread, Task> handler, MessageThread messageThread, string handlerType)
    {
        try
        {
            _logger.LogDebug($"Calling {handlerType} handler: {handler.Method.Name}");
            await handler(messageThread);
            _logger.LogDebug($"Completed {handlerType} handler: {handler.Method.Name}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in {handlerType} handler {handler.Method.Name}: {ex.Message}", ex);
            // Don't rethrow - continue with other handlers
        }
    }
}