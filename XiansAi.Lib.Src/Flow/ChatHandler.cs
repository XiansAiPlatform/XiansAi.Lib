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
public class ChatHandler : SafeHandler
{
    private readonly Queue<MessageThread> _messageQueue = new Queue<MessageThread>();
    private readonly Logger<ChatHandler> _logger = Logger<ChatHandler>.For();
    private readonly MessageHub _messageHub;
    private MessageListenerDelegate? _messageListener;
    private Func<Task<string>>? _systemPromptProvider;
    private bool _initialized = false;

    public RouterOptions RouterOptions { get; set; } = new RouterOptions();

    public ChatHandler(MessageHub messageHub)
    {
        _messageHub = messageHub;
        _messageHub.SubscribeChatHandler(EnqueueChatMessage);
    }

    public void EnqueueChatMessage(MessageThread thread) {
        if (!_initialized) {
            _logger.LogWarning("Chat handler not initialized, adding message to queue for later processing");
        }
        _messageQueue.Enqueue(thread);
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
    public async Task InitConversation()
    {
        _logger.LogDebug("Initializing chat handler");
        _initialized = true;
        
        if (_systemPromptProvider == null)
        {
            throw new InvalidOperationException("System prompt provider has not been set. Set SystemPrompt or SystemPromptName first.");
        }
        while (true)
        {
            MessageThread? messageThread = null;
            try 
            {
                // Get the message from the queue
                messageThread = await DequeueMessage();
                if (messageThread == null) continue;
                

                
                // Process message in non-blocking way
                _ =  Workflow.RunTaskAsync(async () =>
                {
                    try
                    {
                        // Check if we should send a welcome message
                        var welcomeMessageSent = await HandleWelcomeMessage(messageThread);
                        
                        // Skip processing if welcome message was sent
                        if (welcomeMessageSent) return;

                        // Invoke the message listener if provided
                        if (_messageListener != null)
                        {
                            try 
                            {
                                await _messageListener(messageThread);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError("Error processing message in custom listener", ex);
                            }
                        }

                        // Asynchronously process the message
                        var response = await ProcessMessage(messageThread);

                        if (response == null) {
                            if (messageThread.SkipResponse) {
                                _logger.LogDebug("Skipping response from router due to skip response flag");
                                return;
                            } else {
                                throw new Exception("Null response received from router");
                            }
                        }
                        // Respond to the user
                        await messageThread.SendChat(response);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error processing message in background task", ex);
                        
                        // Send error message back to the caller
                        try
                        {
                            await messageThread.SendData(new { error = ex.Message }, "Error occurred while processing message");
                        }
                        catch (Exception sendEx)
                        {
                            _logger.LogError("Failed to send error message", sendEx);
                        }
                    }
                });

            }
            catch (ContinueAsNewException)
            {
                _logger.LogDebug("ChatHandler is continuing as new");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in DataHandler", ex);

                // Send error message back to the caller
                if (messageThread != null)
                {
                    try
                    {
                        await messageThread.SendData(new { error = ex.Message }, "Error occurred while processing data");
                    }
                    catch (Exception sendEx)
                    {
                        _logger.LogError("Failed to send error message", sendEx);
                    }
                }
            }
        }
    }

    private async Task<MessageThread?> DequeueMessage()
    {
        _logger.LogDebug("Waiting for message...");
        await Workflow.WaitConditionAsync(() => _messageQueue.Count > 0 || ShouldContinueAsNew);

        if (ShouldContinueAsNew)
        {
            // Check if we should continue as new
            ContinueAsNew();
        }

        _logger.LogDebug("Message received");
        return _messageQueue.TryDequeue(out var thread) ? thread : null;
    }

    public async Task<string?> ProcessMessage(MessageThread messageThread)
    {
        _logger.LogDebug($"Processing message from '{messageThread.ParticipantId}' on '{messageThread.ThreadId}'");

        // Get the system prompt using the provider
        var systemPrompt = await _systemPromptProvider!();

        // Route the message to the appropriate flow
        var response = await SemanticRouterHub.RouteAsync(messageThread, systemPrompt, RouterOptions);

        _logger.LogDebug($"Response from router: '{response}' for '{messageThread.ParticipantId}' on '{messageThread.ThreadId}'");

        return response;
    }

    /// <summary>
    /// Handles sending a welcome message if conditions are met
    /// </summary>
    /// <returns>True if a welcome message was sent, false otherwise</returns>
    private async Task<bool> HandleWelcomeMessage(MessageThread messageThread)
    {

        // Check if welcome message is configured and the latest message content is null or empty
        if (!string.IsNullOrEmpty(RouterOptions.WelcomeMessage) && 
            string.IsNullOrWhiteSpace(messageThread.LatestMessage.Content))
        {
            _logger.LogDebug($"Sending welcome message to '{messageThread.ParticipantId}' on '{messageThread.ThreadId}'");
            
            // Send the welcome message from the agent
            await messageThread.SendChat(RouterOptions.WelcomeMessage);
            
            _logger.LogDebug($"Welcome message sent to '{messageThread.ParticipantId}' on '{messageThread.ThreadId}'");
            return true; // Indicate that a welcome message was sent
        }
        return false; // Indicate that no welcome message was sent
    }
}
