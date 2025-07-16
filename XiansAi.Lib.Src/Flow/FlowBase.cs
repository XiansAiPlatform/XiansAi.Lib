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
    private Func<Task<string>>? _systemPromptProvider;

    [WorkflowUpdate("HandleInboundChatOrDataSync")]
    public async Task<ChatOrDataRequest> HandleInboundMessageSync(MessageSignal messageSignal)
    {
        var thread = new MessageThread {
            WorkflowId = AgentContext.WorkflowId,
            WorkflowType = AgentContext.WorkflowType,
            Agent = AgentContext.AgentName,
            LatestMessage = new Message {
                Content = messageSignal.Payload.Text,
                Type = MessageType.Chat,
                Data = messageSignal.Payload.Data,
                RequestId = messageSignal.Payload.RequestId,
                Hint = messageSignal.Payload.Hint,
                Scope = messageSignal.Payload.Scope
            },
            ParticipantId = messageSignal.Payload.ParticipantId,
            ThreadId = messageSignal.Payload.ThreadId,
            Authorization = messageSignal.Payload.Authorization
        };

        // Get the system prompt using the provider
        if (_systemPromptProvider == null)
        {
            throw new InvalidOperationException("System prompt provider has not been set. Call one of the InitConversation methods first.");
        }
        
        var systemPrompt = await _systemPromptProvider();

        // process the message
        var response = await ProcessMessageSync(thread, systemPrompt);

        var outgoingMessage = new ChatOrDataRequest
        {
            Text = response,
            RequestId = thread.LatestMessage.RequestId,
            Scope = thread.LatestMessage.Scope,
            ParticipantId = thread.ParticipantId,
            WorkflowId = thread.WorkflowId,
            WorkflowType = thread.WorkflowType,
            Agent = thread.Agent,
            ThreadId = thread.ThreadId,
        };

        // Respond to the user
        return outgoingMessage;
    }

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

    protected async Task InitConversation(string knowledgeContent)
    {
        _logger.LogInformation($"{GetType().Name} Flow started listening for messages");
        
        _systemPromptProvider = () => Task.FromResult(knowledgeContent);
        await ListenToUserMessages();
    }
    
    protected async Task InitConversation(Models.Knowledge knowledge)
    {
        _logger.LogInformation($"{GetType().Name} Flow started listening for messages");
        
        _systemPromptProvider = () => Task.FromResult(knowledge.Content);
        await ListenToUserMessages();
    }

    protected async Task InitConversationByKnowledgeKey(string knowledgeName)
    {
        _logger.LogInformation($"{GetType().Name} Flow started listening for messages");
        
        _systemPromptProvider = async () =>
        {
            var knowledge = await KnowledgeHub.Fetch(knowledgeName);
            if (knowledge == null)
            {
                throw new Exception($"Knowledge '{knowledgeName}' not found");
            }
            return knowledge.Content;
        };
        await ListenToUserMessages();
    }

    [Obsolete("Use InitConversation instead")]
    protected async Task InitUserConversation(string systemPrompt)
    {
        _logger.LogInformation($"{GetType().Name} Flow started listening for messages");
        
        _systemPromptProvider = () => Task.FromResult(systemPrompt);
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
    private async Task<string> ProcessMessageSync(MessageThread messageThread, string systemPrompt)
    {
        _logger.LogDebug($"Processing message from '{messageThread.ParticipantId}' on '{messageThread.ThreadId}'");
        // Route the message to the appropriate flow
        var response = await SemanticRouter.RouteAsync(messageThread, systemPrompt);

        _logger.LogDebug($"Response from router: '{response}' for '{messageThread.ParticipantId}' on '{messageThread.ThreadId}'");

        // Respond to the user
        return response;
    }

    private async Task ProcessMessage(MessageThread messageThread, string systemPrompt)
    {
        _logger.LogDebug($"Processing message from '{messageThread.ParticipantId}' on '{messageThread.ThreadId}'");
        // Route the message to the appropriate flow
        var response = await SemanticRouter.RouteAsync(messageThread, systemPrompt);

        _logger.LogDebug($"Response from router: '{response}' for '{messageThread.ParticipantId}' on '{messageThread.ThreadId}'");

        // Respond to the user
        await messageThread.SendChat(response);
    }
}
