using System.Text.Json;
using Temporalio.Workflows;
using XiansAi.Logging;

namespace XiansAi.Events;

public delegate Task EventReceivedAsyncHandler(Event evt);
public delegate void EventReceivedHandler(Event evt);

public interface IEventHub
{
    void RegisterAsyncHandler(EventReceivedAsyncHandler handler);
    void RegisterHandler(EventReceivedHandler handler);
    void UnregisterHandler(EventReceivedHandler handler);
    void UnregisterAsyncHandler(EventReceivedAsyncHandler handler);
    internal Task ReceiveEvent(Event evt);
}

public class EventHub : IEventHub
{
    private readonly List<Func<Event, Task>> _handlers = new List<Func<Event, Task>>();
    private readonly Logger<EventHub> _logger = Logger<EventHub>.For();
    // Dictionary to keep track of handler references for unregistration
    private readonly Dictionary<Delegate, Func<Event, Task>> _handlerMappings =
        new Dictionary<Delegate, Func<Event, Task>>();

    public static async void SendEvent(string targetWorkflowId, string targetWorkflowType, string evtType, object? payload = null)
    {
        try {
            var agentContext = AgentContext.Instance;
            var evt = new Event
            {
                EventType = evtType,
                SourceWorkflowId = agentContext.WorkflowId,
                SourceWorkflowType = agentContext.WorkflowType,
                SourceAgent = agentContext.Agent,
                Payload = payload,
                Timestamp = DateTimeOffset.UtcNow,
                TargetWorkflowType = targetWorkflowType,
                TargetWorkflowId = targetWorkflowId
            };

            Console.WriteLine($"Sending event: {JsonSerializer.Serialize(evt)}");

            if (Workflow.InWorkflow)
            {
                await Workflow.ExecuteActivityAsync(
                    (SystemActivities a) => a.SendEvent(evt),
                    new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10) });
            }
            else
            {
                await new SystemActivities().SendEvent(evt);
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