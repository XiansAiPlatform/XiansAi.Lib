using Temporalio.Workflows;
using XiansAi.Messaging;
using XiansAi.Logging;
using XiansAi.Flow.Router;
using XiansAi.Knowledge;

namespace XiansAi.Flow;

// Define delegate for message listening
public delegate Task MessageListenerDelegate(MessageThread messageThread);


public abstract class FlowBase : AbstractFlow
{
    private readonly Queue<MessageThread> _messageQueue = new Queue<MessageThread>();
    private readonly Logger<FlowBase> _logger = Logger<FlowBase>.For();
    private MessageListenerDelegate? _messageListener;

    public RouterOptions RouterOptions { get; set; } = new RouterOptions();

    // default system prompt is empty
    public string SystemPrompt { 
        set {
            _systemPromptProvider = () => Task.FromResult(value);
        }
    }

    public string SystemPromptName { 
        set {
            _systemPromptProvider = async () =>
            {
                var knowledge = await KnowledgeHub.Fetch(value);
                return knowledge?.Content ?? throw new Exception($"Knowledge '{value}' not found");
            };
        }
    }

    protected Func<Task<string>>? _systemPromptProvider;

    public FlowBase() : base()  
    {
        // Register the message handler
        _messageHub.SubscribeChatHandler(_messageQueue.Enqueue);
    }

    /// <summary>
    /// Subscribe to message events with a custom listener
    /// </summary>
    /// <param name="messageListener">The delegate to handle incoming messages</param>
    public void SubscribeToMessages(MessageListenerDelegate messageListener)
    {
        _messageListener = messageListener;
    }

    protected async Task InitConversation()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        await InitConversation(null);
#pragma warning restore CS0618 // Type or member is obsolete
    }

    [Obsolete("Use InitConversation() instead after setting the SystemPrompt/SystemPromptName on constructor.")]
    protected async Task InitConversation(string? knowledgeContent = null)
    {
        if (knowledgeContent != null)
        {
            _systemPromptProvider = () => Task.FromResult(knowledgeContent);
        }
        
        if (_systemPromptProvider == null)
        {
            throw new InvalidOperationException("System prompt provider has not been set. Set SystemPrompt or SystemPromptName on constructor.");
        }

        await ListenToUserMessages();
    }


    private async Task ListenToUserMessages()
    {
        if (_systemPromptProvider == null)
        {
            throw new InvalidOperationException("System prompt provider has not been set. Call one of the InitConversation methods first.");
        }

        while (true)
        {
            try {
                _logger.LogDebug($"{GetType().Name} Flow is waiting for a message");
                // Wait for a message to be added to the queue
                await Workflow.WaitConditionAsync(() => _messageQueue.Count > 0);

                // Get the message from the queue
                var thread = _messageQueue.Dequeue();
                
                // Invoke the message listener if provided
                if (_messageListener != null)
                {
                    await _messageListener(thread);
                }

                // Get the system prompt using the provider
                var systemPrompt = await _systemPromptProvider();

                // Asynchronously process the message
                await ProcessMessage(thread, systemPrompt);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error processing message", ex);
            }
        }
    }

    private async Task ProcessMessage(MessageThread messageThread, string systemPrompt)
    {
        _logger.LogDebug($"Processing message from '{messageThread.ParticipantId}' on '{messageThread.ThreadId}'");
        // Route the message to the appropriate flow
        var response = await SemanticRouter.RouteAsync(messageThread, systemPrompt, RouterOptions);

        _logger.LogDebug($"Response from router: '{response}' for '{messageThread.ParticipantId}' on '{messageThread.ThreadId}'");

        // Respond to the user
        await messageThread.SendChat(response);
    }
}
