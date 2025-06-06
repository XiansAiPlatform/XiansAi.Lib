using System.Text.Json;
using Temporalio.Workflows;
using XiansAi.Logging;
using XiansAi.Messaging;

namespace XiansAi.Events;

public delegate Task EventReceivedAsyncHandler<T>(EventMetadata metadata, T? payload);
public delegate void EventReceivedHandler<T>(EventMetadata metadata, T? payload);

public class EventMetadata
{
    public required string EventType { get; set; }
    public required string SourceWorkflowId { get; set; }
    public required string SourceWorkflowType { get; set; }
    public required string SourceAgent { get; set; }
}

public interface IEventHub
{
    void Subscribe<T>(EventReceivedAsyncHandler<T> handler);
    void Subscribe<T>(EventReceivedHandler<T> handler);
    void Unsubscribe<T>(EventReceivedHandler<T> handler);
    void Unsubscribe<T>(EventReceivedAsyncHandler<T> handler);
    internal Task EventListener(EventSignal obj);
}

public class EventHub : IEventHub
{
    private readonly List<Func<EventMetadata, object?, Task>> _handlers = new List<Func<EventMetadata, object?, Task>>();
    private readonly Logger<EventHub> _logger = Logger<EventHub>.For();
    // Dictionary to keep track of handler references for unregistration
    private readonly Dictionary<Delegate, Func<EventMetadata, object?, Task>> _handlerMappings =
        new Dictionary<Delegate, Func<EventMetadata, object?, Task>>();

    public void Subscribe<T>(EventReceivedAsyncHandler<T> handler)
    {
        // Convert the delegate type with proper type casting
        Func<EventMetadata, object?, Task> funcHandler = (metadata, payload) =>
        {
            var typedPayload = payload != null ? CastPayload<T>(payload) : default;
            return handler(metadata, typedPayload);
        };

        if (!_handlerMappings.ContainsKey(handler))
        {
            _handlerMappings[handler] = funcHandler;
            _handlers.Add(funcHandler);
        }
    }

    public void Subscribe<T>(EventReceivedHandler<T> handler)
    {
        // Wrap the synchronous handler to return a completed task with proper type casting
        Func<EventMetadata, object?, Task> funcHandler = (metadata, payload) =>
        {
            var typedPayload = payload != null ? CastPayload<T>(payload) : default;
            handler(metadata, typedPayload);
            return Task.CompletedTask;
        };

        if (!_handlerMappings.ContainsKey(handler))
        {
            _handlerMappings[handler] = funcHandler;
            _handlers.Add(funcHandler);
        }
    }

    public void Unsubscribe<T>(EventReceivedHandler<T> handler)
    {
        if (_handlerMappings.TryGetValue(handler, out var funcHandler))
        {
            _handlers.Remove(funcHandler);
            _handlerMappings.Remove(handler);
        }
    }

    public void Unsubscribe<T>(EventReceivedAsyncHandler<T> handler)
    {
        if (_handlerMappings.TryGetValue(handler, out var funcHandler))
        {
            _handlers.Remove(funcHandler);
            _handlerMappings.Remove(handler);
        }
    }

    public async Task EventListener(EventSignal obj)
    {
        var metadata = new EventMetadata
        {
            EventType = obj.EventType,
            SourceWorkflowId = obj.SourceWorkflowId,
            SourceWorkflowType = obj.SourceWorkflowType,
            SourceAgent = obj.SourceAgent
        };
        // Call all handlers uniformly
        foreach (var handler in _handlers.ToList())
        {
            await handler(metadata, obj.Payload);
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

}