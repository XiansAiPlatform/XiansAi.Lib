using System.Text.Json;
using Microsoft.Extensions.Logging;
using Temporalio.Converters;
using Temporalio.Workflows;

namespace XiansAi.Messaging;

public delegate Task MessageReceivedAsyncHandler(MessageThread messageThread);
public delegate void MessageReceivedHandler(MessageThread messageThread);

public interface IMessenger
{
    Task<string?> SendMessageAsync(string content, string participantId, string? metadata = null);
    void RegisterAsyncHandler(MessageReceivedAsyncHandler handler);
    void RegisterHandler(MessageReceivedHandler handler);
    void UnregisterHandler(MessageReceivedHandler handler);
    void UnregisterAsyncHandler(MessageReceivedAsyncHandler handler);
    internal Task ReceiveMessage(MessageSignal messageSignal);
}

public class Messenger : IMessenger
{
    private readonly List<Func<MessageThread, Task>> _handlers = new List<Func<MessageThread, Task>>();
    private readonly string _workflowId;
    private readonly string _workflowType;
    private readonly IReadOnlyDictionary<string, IRawValue> _workflowMemo;
    private readonly ILogger<Messenger> _logger;

    private readonly Dictionary<Delegate, Func<MessageThread, Task>> _handlerMappings = 
        new Dictionary<Delegate, Func<MessageThread, Task>>();

    public Messenger(string workflowId, string workflowType, IReadOnlyDictionary<string, IRawValue> workflowMemo)
    {
        _workflowId = workflowId;
        _workflowType = workflowType;
        _workflowMemo = workflowMemo;
        _logger = Globals.LogFactory.CreateLogger<Messenger>();
    }

    public static Messenger Instance { 
        get {
            if (!Workflow.InWorkflow) {
                throw new InvalidOperationException("Messenger must be used only within a workflow execution context");
            }

            return new Messenger(
                Workflow.Info.WorkflowId,
                Workflow.Info.WorkflowType,
                Workflow.Memo
            );
        } 
    }

    public async Task<string?> SendMessageAsync(string content, string participantId, string? metadata = null)
    {

        var outgoingMessage = new OutgoingMessage
        {
            Content = content,
            Metadata = metadata,
            ParticipantId = participantId,
            WorkflowId = _workflowId,
            WorkflowType = _workflowType,
            Agent = ExtractMemoValue(_workflowMemo, Constants.AgentKey) ?? throw new Exception("Agent is required"),
            QueueName = ExtractMemoValue(_workflowMemo, Constants.QueueNameKey) ?? "",
            Assignment = ExtractMemoValue(_workflowMemo, Constants.AssignmentKey) ?? ""
        };

        var success = await Workflow.ExecuteActivityAsync(
            (SystemActivities a) => a.SendMessage(outgoingMessage),
            new SystemActivityOptions());

        return success;
    }

    public void RegisterAsyncHandler(MessageReceivedAsyncHandler handler)
    {
        // Convert the delegate type
        Func<MessageThread, Task> funcHandler = messageThread => handler(messageThread);
        
        if (!_handlerMappings.ContainsKey(handler))
        {
            _handlerMappings[handler] = funcHandler;
            _handlers.Add(funcHandler);
        }
    }

    public void RegisterHandler(MessageReceivedHandler handler)
    {
        // Wrap the synchronous handler to return a completed task
        Func<MessageThread, Task> funcHandler = messageThread => 
        {
            handler(messageThread);
            return Task.CompletedTask;
        };
        
        if (!_handlerMappings.ContainsKey(handler))
        {
            _handlerMappings[handler] = funcHandler;
            _handlers.Add(funcHandler);
        }
    }
    
    public void UnregisterHandler(MessageReceivedHandler handler)
    {
        if (_handlerMappings.TryGetValue(handler, out var funcHandler))
        {
            _handlers.Remove(funcHandler);
            _handlerMappings.Remove(handler);
        }
    }
    
    public void UnregisterAsyncHandler(MessageReceivedAsyncHandler handler)
    {
        if (_handlerMappings.TryGetValue(handler, out var funcHandler))
        {
            _handlers.Remove(funcHandler);
            _handlerMappings.Remove(handler);
        }
    }

    public async Task ReceiveMessage(MessageSignal messageSignal)
    {
        _logger.LogInformation("Received Signal Message: {Message}", JsonSerializer.Serialize(messageSignal));

        var messageThread = new MessageThread {
            ParticipantId = messageSignal.ParticipantId,
            WorkflowId = _workflowId,
            WorkflowType = _workflowType,
            ThreadId = messageSignal.ThreadId,
            // optional fields required for start and handover
            Agent = ExtractMemoValue(_workflowMemo, Constants.AgentKey) ?? throw new Exception("Agent is required"),
            QueueName = ExtractMemoValue(_workflowMemo, Constants.QueueNameKey) ?? "",
            Assignment = ExtractMemoValue(_workflowMemo, Constants.AssignmentKey) ?? ""
        };

        
        // Call all handlers uniformly
        foreach (var handler in _handlers.ToList())
        {
            await handler(messageThread);
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
}