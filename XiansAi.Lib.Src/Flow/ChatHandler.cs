using Temporalio.Workflows;
using XiansAi.Messaging;
using XiansAi.Logging;
using XiansAi.Flow.Router;
using XiansAi.Knowledge;

namespace XiansAi.Flow;

// Define delegate for message listening
public delegate Task MessageListenerDelegate(MessageThread messageThread);

/// <summary>
/// Handles chat message processing and conversation management for flows
/// </summary>
public class ChatHandler
{
    private readonly Queue<MessageThread> _messageQueue = new Queue<MessageThread>();
    private readonly Logger<ChatHandler> _logger = Logger<ChatHandler>.For();
    private readonly IMessageHub _messageHub;
    private MessageListenerDelegate? _messageListener;
    private Func<Task<string>>? _systemPromptProvider;
    public SystemActivityOptions SystemActivityOptions { get; set; } = new SystemActivityOptions();

    public RouterOptions RouterOptions { get; set; } = new RouterOptions();

    public ChatHandler(IMessageHub messageHub)
    {
        _messageHub = messageHub;
        // Register the message handler
        _messageHub.SubscribeChatHandler(_messageQueue.Enqueue);
    }

    /// <summary>
    /// Sets the system prompt directly
    /// </summary>
    public string SystemPrompt 
    { 
        set {
            _systemPromptProvider = () => Task.FromResult(value);
        }
    }

    /// <summary>
    /// Sets the system prompt from knowledge base
    /// </summary>
    public string SystemPromptName 
    { 
        set {
            _systemPromptProvider = async () =>
            {
                var knowledge = await KnowledgeHub.Fetch(value);
                return knowledge?.Content ?? throw new Exception($"Knowledge '{value}' not found");
            };
        }
    }

    /// <summary>
    /// Subscribe to message events with a custom listener
    /// </summary>
    /// <param name="messageListener">The delegate to handle incoming messages</param>
    public void SubscribeToMessages(MessageListenerDelegate messageListener)
    {
        _messageListener = messageListener;
    }

    /// <summary>
    /// Starts listening for user messages and processing them
    /// </summary>
    public async Task StartListening()
    {
        if (_systemPromptProvider == null)
        {
            throw new InvalidOperationException("System prompt provider has not been set. Set SystemPrompt or SystemPromptName first.");
        }

        await ListenToUserMessages();
    }

    private async Task ListenToUserMessages()
    {
        while (true)
        {
            try 
            {
                _logger.LogDebug("ChatHandler is waiting for a message");
                // Wait for a message to be added to the queue
                await Workflow.WaitConditionAsync(() => _messageQueue.Count > 0);

                // Get the message from the queue
                var thread = _messageQueue.Dequeue();
                
                // Invoke the message listener if provided
                if (_messageListener != null)
                {
                    try 
                    {
                        await _messageListener(thread);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error processing message in custom listener", ex);
                    }
                }

                // Get the system prompt using the provider
                var systemPrompt = await _systemPromptProvider!();

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
        var response = await SemanticRouterHub.RouteAsync(messageThread, systemPrompt, RouterOptions, SystemActivityOptions);

        _logger.LogDebug($"Response from router: '{response}' for '{messageThread.ParticipantId}' on '{messageThread.ThreadId}'");

        // Respond to the user
        await messageThread.SendChat(response);
    }
}
