using System.Text.Json;
using Temporalio.Workflows;
using XiansAi.Logging;

namespace XiansAi.Events;

public delegate Task EventReceivedAsyncHandler(object obj);
public delegate void EventReceivedHandler(object obj);

public interface IEventHub
{
    void RegisterAsyncHandler(EventReceivedAsyncHandler handler);
    void RegisterHandler(EventReceivedHandler handler);
    void UnregisterHandler(EventReceivedHandler handler);
    void UnregisterAsyncHandler(EventReceivedAsyncHandler handler);
    internal Task EventListener(object obj);
}

public class EventHub : IEventHub
{
    private readonly List<Func<object, Task>> _handlers = new List<Func<object, Task>>();
    private readonly Logger<EventHub> _logger = Logger<EventHub>.For();
    // Dictionary to keep track of handler references for unregistration
    private readonly Dictionary<Delegate, Func<object, Task>> _handlerMappings =
        new Dictionary<Delegate, Func<object, Task>>();

    public static async Task SendEvent(string targetWorkflowId, string targetWorkflowType, string evtType, object? payload = null)
    {
        try {
            var eventDto = new EventDto
            {
                EventType = evtType,
                SourceWorkflowId = AgentContext.WorkflowId,
                SourceWorkflowType = AgentContext.WorkflowType,
                SourceAgent = AgentContext.Agent,
                Payload = payload,
                Timestamp = DateTimeOffset.UtcNow,
                TargetWorkflowType = targetWorkflowType,
                TargetWorkflowId = targetWorkflowId
            };

            if (Workflow.InWorkflow)
            {
                await Workflow.ExecuteActivityAsync(
                    (SystemActivities a) => a.SendEvent(eventDto),
                    new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10) });
            }
            else
            {
                await new SystemActivities().SendEvent(eventDto);
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            throw;
        }
    }

    public void RegisterAsyncHandler(EventReceivedAsyncHandler handler)
    {
        // Convert the delegate type
        Func<object, Task> funcHandler = (obj) => handler(obj);

        if (!_handlerMappings.ContainsKey(handler))
        {
            _handlerMappings[handler] = funcHandler;
            _handlers.Add(funcHandler);
        }
    }

    public void RegisterHandler(EventReceivedHandler handler)
    {
        // Wrap the synchronous handler to return a completed task
        Func<object, Task> funcHandler = (obj) =>
        {
            handler(obj);
            return Task.CompletedTask;
        };

        if (!_handlerMappings.ContainsKey(handler))
        {
            _handlerMappings[handler] = funcHandler;
            _handlers.Add(funcHandler);
        }
    }

    public void UnregisterHandler(EventReceivedHandler handler)
    {
        if (_handlerMappings.TryGetValue(handler, out var funcHandler))
        {
            _handlers.Remove(funcHandler);
            _handlerMappings.Remove(handler);
        }
    }

    public void UnregisterAsyncHandler(EventReceivedAsyncHandler handler)
    {
        if (_handlerMappings.TryGetValue(handler, out var funcHandler))
        {
            _handlers.Remove(funcHandler);
            _handlerMappings.Remove(handler);
        }
    }

    public async Task EventListener(object obj)
    {
        // Call all handlers uniformly
        foreach (var handler in _handlers.ToList())
        {
            await handler(obj);
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
            throw new InvalidOperationException($"Failed to cast event payload to {typeof(T).Name}");
        }
    }

}