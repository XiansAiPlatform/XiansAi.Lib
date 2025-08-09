using XiansAi.Flow.Router;

namespace XiansAi.Flow;

public abstract class FlowBase : AbstractFlow
{
    protected readonly ChatHandler _chatHandler;
    protected readonly DataHandler _dataHandler;
    protected readonly ScheduleHandler _scheduleHandler;
    public RouterOptions RouterOptions 
    { 
        get => _chatHandler.RouterOptions; 
        set => _chatHandler.RouterOptions = value; 
    }

    public SystemActivityOptions SystemActivityOptions 
    { 
        set {
            _chatHandler.SystemActivityOptions = value;
            _dataHandler.SystemActivityOptions = value;
        }
    }

    public string SystemPrompt 
    { 
        set => _chatHandler.SystemPrompt = value;
    }

    public string SystemPromptName 
    { 
        set => _chatHandler.SystemPromptName = value;
    }

    public FlowBase() : base()  
    {
        _chatHandler = new ChatHandler(_messageHub);
        _dataHandler = new DataHandler(_messageHub, this);
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

    /// <summary>
    /// Initializes the conversation with a specific system prompt
    /// </summary>
    /// <param name="knowledgeContent">The system prompt content</param>
    [Obsolete("Use InitConversation() instead after setting the SystemPrompt/SystemPromptName on constructor.")]
    protected async Task InitConversation(string? knowledgeContent = null)
    {
        if (knowledgeContent != null)
        {
            _chatHandler.SystemPrompt = knowledgeContent;
        }

        await InitConversation();
    }
}
