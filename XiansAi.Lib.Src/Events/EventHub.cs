using System.Text.Json;
using Microsoft.Extensions.Logging;
using Temporalio.Converters;
using Temporalio.Workflows;

namespace XiansAi.Events;

public delegate Task EventReceivedAsyncHandler(Event evt);
public delegate void EventReceivedHandler(Event evt);

public interface IEventHub
{
    void SendEventToWorkflowAsync(string targetWorkflowId, string evtType, object? payload = null);
    void RegisterAsyncHandler(EventReceivedAsyncHandler handler);
    void RegisterHandler(EventReceivedHandler handler);
    void UnregisterHandler(EventReceivedHandler handler);
    void UnregisterAsyncHandler(EventReceivedAsyncHandler handler);
    internal Task ReceiveEvent(Event evt);
}

public class EventHub : IEventHub
{
    private readonly List<Func<Event, Task>> _handlers = new List<Func<Event, Task>>();
    private readonly string _workflowId;
    private readonly string _workflowType;
    private readonly IReadOnlyDictionary<string, IRawValue> _workflowMemo;
    private readonly ILogger<EventHub> _logger;
    // Dictionary to keep track of handler references for unregistration
    private readonly Dictionary<Delegate, Func<Event, Task>> _handlerMappings = 
        new Dictionary<Delegate, Func<Event, Task>>();

    public EventHub(string workflowId, string workflowType, IReadOnlyDictionary<string, IRawValue> workflowMemo)
    {
        _workflowId = workflowId;
        _workflowType = workflowType;
        _workflowMemo = workflowMemo;
        _logger = Globals.LogFactory.CreateLogger<EventHub>();
    }

    public static EventHub Instance { 
        get {
            if (!Workflow.InWorkflow) {
                throw new InvalidOperationException("EventHub must be used only within a workflow execution context");
            }

            return new EventHub(
                Workflow.Info.WorkflowId,
                Workflow.Info.WorkflowType,
                Workflow.Memo
            );
        } 
    }

    public async void StartAndSendEventToWorkflowByType(string targetWorkflowType, string evtType, object? payload = null)
    {
        var evt = new StartAndSendEvent {
            EventType = evtType,
            SourceWorkflowId = _workflowId,
            SourceWorkflowType = _workflowType,
            SourceAgent = ExtractMemoValue(_workflowMemo, "agent") ?? throw new Exception("Agent not found in memo"),
            SourceQueueName = ExtractMemoValue(_workflowMemo, "queue"),
            SourceAssignment = ExtractMemoValue(_workflowMemo, "assignment"),
            Payload = payload,
            Timestamp = DateTimeOffset.UtcNow,
            TargetWorkflowType = targetWorkflowType,
        };

        if(Workflow.InWorkflow) {
            await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.StartAndSendEventToWorkflowByType(evt),
                new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10) });
        }
        else {
            await new SystemActivities().StartAndSendEventToWorkflowByType(evt);
        }
    }

    public async void SendEventToWorkflowAsync(string targetWorkflowId, string evtType, object? payload = null)
    {
        var evt = new Event {
            EventType = evtType,
            SourceWorkflowId = _workflowId,
            SourceWorkflowType = _workflowType,
            SourceAgent = ExtractMemoValue(_workflowMemo, "agent") ?? throw new Exception("Agent not found in memo"),
            SourceQueueName = ExtractMemoValue(_workflowMemo, "queue"),
            SourceAssignment = ExtractMemoValue(_workflowMemo, "assignment"),
            Payload = payload,
            Timestamp = DateTimeOffset.UtcNow,
            TargetWorkflowId = targetWorkflowId,
        };

        try
        {
            // From workflow to workflow
            await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.SendEventToWorkflowById(evt),
                new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send event {EventType} from {SourceWorkflow} to {TargetWorkflow}", 
                evt.EventType, _workflowId, targetWorkflowId);
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

    private string? ExtractMemoValue(IReadOnlyDictionary<string, IRawValue> memo, string key)
    {
        if (memo.TryGetValue(key, out var memoValue))
        {
            var value = memoValue?.Payload?.Data?.ToStringUtf8()?.Replace("\"", "");
            return memoValue?.Payload?.Data?.ToStringUtf8()?.Replace("\"", "");
        }
        return null;
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