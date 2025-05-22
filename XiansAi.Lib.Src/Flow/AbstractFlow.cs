using Temporalio.Workflows;
using XiansAi.Knowledge;
using XiansAi.Messaging;
using XiansAi.Events;
using XiansAi.Memory;

namespace XiansAi.Flow;

/// <summary>
/// Base class for all workflow implementations providing common functionality.
/// </summary>
public abstract class AbstractFlow
{

    protected readonly IMemoryHub _memoryHub;
    protected readonly IMessageHub _messageHub;
    protected readonly IEventHub _eventHub;

    // Signal method to receive events
    [WorkflowSignal("HandleInboundEvent")]
    public async Task HandleInboundEvent(EventSignal eventDto)
    {
        await _eventHub.EventListener(eventDto);
    }

    [WorkflowSignal("HandleInboundMessage")]
    public async Task HandleInboundMessage(MessageSignal messageSignal)
    {
        await _messageHub.ReceiveMessage(messageSignal);
    }

    /// <summary>
    /// Initializes a new instance of the FlowBase class.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when LogFactory is not initialized</exception>
    protected AbstractFlow()
    {
        _messageHub = new MessageHub();
        _memoryHub = new MemoryHub();
        _eventHub = new EventHub();
    }

}
