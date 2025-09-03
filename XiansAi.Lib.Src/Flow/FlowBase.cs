using Temporalio.Workflows;
using XiansAi.Flow.Router;
using XiansAi.Messaging;

namespace XiansAi.Flow;

public abstract class FlowBase : AbstractFlow
{
    protected readonly ChatHandler _chatHandler;
    protected readonly DataHandler _dataHandler;
    protected readonly WebhookHandler _webhookHandler;
    protected readonly ScheduleHandler _scheduleHandler;
    public RouterOptions RouterOptions 
    { 
        get => _chatHandler.RouterOptions; 
        set => _chatHandler.RouterOptions = value; 
    }
    public string SystemPrompt 
    { 
        set => _chatHandler.SystemPrompt = value;
    }

    public string SystemPromptName 
    { 
        set => _chatHandler.SystemPromptName = value;
    }

    [WorkflowUpdate("ProcessMessageStateless")]
    public async Task<MessageResponse?> ProcessMessageStateless(MessageThread messageThread) {

        if(messageThread.LatestMessage.Type == MessageType.Chat) {
            // Set the message thread to stateless
            messageThread.Stateful = false;
            // Process the message
            var response = await _chatHandler.ProcessMessage(messageThread);
            // Return the response
            return new MessageResponse {
                Text = response ?? string.Empty,
                MessageType = MessageType.Chat,
                Timestamp = DateTime.UtcNow,
                Scope = messageThread.LatestMessage.Scope,
                Hint = messageThread.LatestMessage.Hint,
                RequestId = messageThread.LatestMessage.RequestId,
                ThreadId = messageThread.ThreadId,
                ParticipantId = messageThread.ParticipantId,
            };
        } else if (messageThread.LatestMessage.Type == MessageType.Data) {
            // Set the message thread to stateless
            messageThread.Stateful = false;

            // Process the message
            var response = await _dataHandler.ProcessData(messageThread);
            return new MessageResponse {
                Data = response,
                MessageType = MessageType.Data,
                Timestamp = DateTime.UtcNow,
                Scope = messageThread.LatestMessage.Scope,
                Hint = messageThread.LatestMessage.Hint,
                RequestId = messageThread.LatestMessage.RequestId,
                ThreadId = messageThread.ThreadId,
                ParticipantId = messageThread.ParticipantId,
            };
        } else {
            throw new Exception("Latest message is not a chat or data message");
        }
    }

    public FlowBase() : base()  
    {
        _chatHandler = new ChatHandler(_messageHub);
        _dataHandler = new DataHandler(_messageHub, this);
        _webhookHandler = new WebhookHandler();
        _scheduleHandler = new ScheduleHandler(this);
    }

    /// <summary>
    /// Subscribe to message events with a custom listener
    /// </summary>
    /// <param name="messageListener">The delegate to handle incoming messages</param>
    public void SubscribeToMessages(MessageListenerDelegate messageListener)
    {
        _chatHandler.SubscribeToMessages(messageListener);
    }

    protected async Task InitWebhookProcessing()
    {
        await _webhookHandler.InitWebhookProcessing();
    }

    protected async Task InitDataProcessing()
    {
        await _dataHandler.InitDataProcessing();
    }

    protected async Task InitSchedule()
    {
        await _scheduleHandler.InitSchedule();
    }

    /// <summary>
    /// Initializes the conversation and starts listening for messages
    /// </summary>
    protected async Task InitConversation()
    {
        await _chatHandler.InitConversation();
    }

}
