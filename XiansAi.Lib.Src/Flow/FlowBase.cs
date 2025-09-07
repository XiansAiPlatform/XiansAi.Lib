using System.Reflection;
using Temporalio.Workflows;
using XiansAi.Flow.Router;

namespace XiansAi.Flow;

public abstract class FlowBase : AbstractFlow
{
    internal readonly ChatHandler _chatHandler;
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

    public static Type GetTypeByWorkflowType(string workflowType) {
        // Find all types in the current app domain that inherit from FlowBase
        var flowTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(FlowBase).IsAssignableFrom(type) && !type.IsAbstract)
            .ToList();

        // Find the type with matching WorkflowAttribute.Name
        foreach (var type in flowTypes)
        {
            var workflowAttr = type.GetCustomAttribute<WorkflowAttribute>();
            if (workflowAttr != null && workflowAttr.Name == workflowType)
            {
                return type;
            }
        }

        throw new InvalidOperationException($"No FlowBase implementation found with WorkflowAttribute.Name = '{workflowType}'");
    }

    public static FlowBase GetInstance(string workflowType) {
        var type = GetTypeByWorkflowType(workflowType);
        return (FlowBase)Activator.CreateInstance(type)!;
    }
}
