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

    public FlowBase() : base()  
    {
        // Register the message handler
        _messageHub.RegisterMessageHandler(_messageQueue.Enqueue);
    }


    protected async Task InitConversation(Models.Knowledge knowledge, MessageListenerDelegate? messageListener = null)
    {
        _logger.LogInformation($"{GetType().Name} Flow started listening for messages");

        await ListenToUserMessages(messageListener, knowledge.Content);
    }

    protected async Task InitConversation(string knowledgeName, MessageListenerDelegate? messageListener = null)
    {
        _logger.LogInformation($"{GetType().Name} Flow started listening for messages");

        await ListenToUserMessagesByKnowledgeKey(messageListener, knowledgeName);
    }

    [Obsolete("Use InitConversation instead")]
    protected async Task InitUserConversation(string systemPrompt, MessageListenerDelegate? messageListener = null)
    {
        _logger.LogInformation($"{GetType().Name} Flow started listening for messages");

        await ListenToUserMessages(messageListener, systemPrompt);
    }


    private async Task ListenToUserMessages(MessageListenerDelegate? messageListener, Func<Task<string>> systemPromptProvider)
    {
        while (true)
        {
            try {
                _logger.LogDebug($"{GetType().Name} Flow is waiting for a message");
                // Wait for a message to be added to the queue
                await Workflow.WaitConditionAsync(() => _messageQueue.Count > 0);

                // Get the message from the queue
                var thread = _messageQueue.Dequeue();
                
                // Invoke the message listener if provided
                if (messageListener != null)
                {
                    await messageListener(thread);
                }

                // Get the system prompt using the provider
                var systemPrompt = await systemPromptProvider();

                // Asynchronously process the message
                await ProcessMessage(thread, systemPrompt);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error processing message", ex);
            }
        }
    }

    private async Task ListenToUserMessagesByKnowledgeKey(MessageListenerDelegate? messageListener, string systemPromptKnowledgeKey)
    {
        await ListenToUserMessages(messageListener, async () =>
        {
            var knowledge = await KnowledgeHub.Fetch(systemPromptKnowledgeKey);
            if (knowledge == null)
            {
                throw new Exception($"Knowledge '{systemPromptKnowledgeKey}' not found");
            }
            return knowledge.Content;
        });
    }

    private async Task ListenToUserMessages(MessageListenerDelegate? messageListener, string systemPrompt)
    {
        await ListenToUserMessages(messageListener, () => Task.FromResult(systemPrompt));
    }

    private async Task ProcessMessage(MessageThread messageThread, string systemPrompt)
    {
        _logger.LogDebug($"Processing message from '{messageThread.ParticipantId}' on '{messageThread.ThreadId}'");
        // Route the message to the appropriate flow
        var response = await SemanticRouter.RouteAsync(messageThread, systemPrompt);

        _logger.LogDebug($"Response from router: '{response}' for '{messageThread.ParticipantId}' on '{messageThread.ThreadId}'");

        // Respond to the user
        await messageThread.Respond(response);
    }
}
