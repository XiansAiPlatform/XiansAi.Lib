using Temporalio.Workflows;
using XiansAi.Messaging;
using XiansAi.Memory;
using Temporal;
using System.Diagnostics;
using XiansAi.Telemetry;

namespace XiansAi.Flow;

/// <summary>
/// Base class for all workflow implementations providing common functionality.
/// </summary>
public abstract class AbstractFlow
{
    protected readonly MessageHub _messageHub;
    //private readonly IEventHub _eventHub;

    /// <summary>
    /// Initializes a new instance of the FlowBase class.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when LogFactory is not initialized</exception>
    protected AbstractFlow()
    {
        _messageHub = new MessageHub();
    }

    // Signal method to receive events
    [WorkflowSignal("HandleInboundEvent")]
    public async Task HandleInboundEvent(EventSignal eventDto)
    {
        // Restore trace context from workflow memo to continue the trace from the server request
        OpenTelemetryExtensions.RestoreTraceContextFromMemo();
        
        // Create a wrapper span for the entire signal handling to ensure trace context persists
        using var signalActivity = OpenTelemetryExtensions.StartTemporalOperation(
            "Temporal.Signal.HandleInboundEvent",
            new Dictionary<string, object>
            {
                ["temporal.operation_type"] = "signal_handle",
                ["temporal.signal_name"] = "HandleInboundEvent",
                ["temporal.workflow_id"] = AgentContext.WorkflowId ?? "",
                ["temporal.workflow_type"] = AgentContext.WorkflowType ?? ""
            });
        
        await _messageHub.ReceiveFlowMessage(eventDto);
    }

    [WorkflowSignal("HandleInboundChatOrData")]
    public async Task HandleInboundChatOrDataSignal(MessageSignal messageSignal)
    {
        Console.WriteLine($"[OpenTelemetry] DIAGNOSTIC: HandleInboundChatOrDataSignal() called");
        Console.WriteLine($"  - Signal payload TraceParent: {messageSignal.Payload?.TraceParent ?? "NULL"}");
        Console.WriteLine($"  - Signal payload TraceState: {messageSignal.Payload?.TraceState ?? "NULL"}");
        
        // Restore trace context from workflow memo or signal payload to continue the trace from the server request
        // For existing workflows, trace context comes from signal payload; for new workflows, from memo
        OpenTelemetryExtensions.RestoreTraceContextFromMemo(
            traceParentFromPayload: messageSignal.Payload?.TraceParent,
            traceStateFromPayload: messageSignal.Payload?.TraceState);
        
        // Create a wrapper span for the entire signal handling to ensure trace context persists
        // This span will be the parent for all child operations (SemanticKernel, etc.)
        // NOTE: We intentionally do NOT use 'using' here because:
        // 1. The async operations continue after this method returns
        // 2. We want Activity.Current to remain set for the duration of all child operations
        // 3. Activity disposal will happen automatically when the workflow context ends
        var signalActivity = OpenTelemetryExtensions.StartTemporalOperation(
            "Temporal.Signal.HandleInboundChatOrData",
            new Dictionary<string, object>
            {
                ["temporal.operation_type"] = "signal_handle",
                ["temporal.signal_name"] = "HandleInboundChatOrData",
                ["temporal.workflow_id"] = AgentContext.WorkflowId ?? "",
                ["temporal.workflow_type"] = AgentContext.WorkflowType ?? "",
                ["temporal.message_type"] = messageSignal.Payload?.Type ?? "unknown"
            });
        
        await _messageHub.ReceiveConversationChatOrData(messageSignal);
    }

}
