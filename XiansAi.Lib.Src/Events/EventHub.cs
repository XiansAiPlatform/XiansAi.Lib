using System.Text.Json;
using Microsoft.Extensions.Logging;
using Temporal;
using Temporalio.Activities;
using Temporalio.Workflows;

namespace XiansAi.Events;

public delegate Task EventReceivedAsyncHandler(Event evt);
public delegate void EventReceivedHandler(Event evt);

public interface IEventHub
{
    void SendEvent(string targetWorkflowType, string evtType, object? payload = null);
    void RegisterAsyncHandler(EventReceivedAsyncHandler handler);
    void RegisterHandler(EventReceivedHandler handler);
    void UnregisterHandler(EventReceivedHandler handler);
    void UnregisterAsyncHandler(EventReceivedAsyncHandler handler);
    internal Task ReceiveEvent(Event evt);
}

public class EventHub : IEventHub
{
    private readonly List<Func<Event, Task>> _handlers = new List<Func<Event, Task>>();
    private readonly RouteContext _routeContext;
    private readonly ILogger<EventHub> _logger;
    // Dictionary to keep track of handler references for unregistration
    private readonly Dictionary<Delegate, Func<Event, Task>> _handlerMappings =
        new Dictionary<Delegate, Func<Event, Task>>();

    public EventHub(RouteContext routeContext)
    {
        _logger = Globals.LogFactory.CreateLogger<EventHub>();
        _routeContext = routeContext;
    }

    public async void SendEvent(string targetWorkflowType, string evtType, object? payload = null)
    {
        var evt = new Event
        {
            EventType = evtType,
            SourceWorkflowId = _routeContext.WorkflowId,
            SourceWorkflowType = _routeContext.WorkflowType,
            SourceAgent = _routeContext.Agent,
            SourceQueueName = _routeContext.QueueName,
            SourceAssignment = _routeContext.AssignmentId,
            Payload = payload,
            Timestamp = DateTimeOffset.UtcNow,
            TargetWorkflowType = targetWorkflowType,
        };

        if (Workflow.InWorkflow)
        {
            await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.StartAndSendEventToWorkflowByType(evt),
                new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10) });
        }
        else
        {
            await new SystemActivities().StartAndSendEventToWorkflowByType(evt);
        }
    }

    public void RegisterAsyncHandler(EventReceivedAsyncHandler handler)
    {
        // Convert the delegate type
        Func<Event, Task> funcHandler = evt => handler(evt);

        if (!_handlerMappings.ContainsKey(handler))
        {
            _handlerMappings[handler] = funcHandler;
            _handlers.Add(funcHandler);
        }
    }

    public void RegisterHandler(EventReceivedHandler handler)
    {
        // Wrap the synchronous handler to return a completed task
        Func<Event, Task> funcHandler = evt =>
        {
            handler(evt);
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

    public async Task ReceiveEvent(Event evt)
    {
        _logger.LogInformation($"Received Event: {JsonSerializer.Serialize(evt)}");

        // Call all handlers uniformly
        foreach (var handler in _handlers.ToList())
        {
            await handler(evt);
        }
    }

}