using Temporalio.Workflows;
using XiansAi.Messaging;
using XiansAi.Memory;

namespace XiansAi.Flow;

/// <summary>
/// Base class for all workflow implementations providing common functionality.
/// </summary>
public abstract class AbstractFlow
{

    protected readonly IMemoryHub _memoryHub;
    protected readonly IMessageHub _messageHub;
    //private readonly IEventHub _eventHub;

    // Signal method to receive events
    [WorkflowSignal("HandleInboundEvent")]
    public async Task HandleInboundEvent(EventSignal eventDto)
    {
        await _messageHub.ReceiveFlowMessage(eventDto);
    }

    [WorkflowSignal("HandleInboundChatOrData")]
    public async Task HandleInboundChatOrData(MessageSignal messageSignal)
    {
        await _messageHub.ReceiveConversationChatOrData(messageSignal);
    }



    /// <summary>
    /// Initializes a new instance of the FlowBase class.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when LogFactory is not initialized</exception>
    protected AbstractFlow()
    {
        _messageHub = new MessageHub();
        _memoryHub = new MemoryHub();
    }

}
