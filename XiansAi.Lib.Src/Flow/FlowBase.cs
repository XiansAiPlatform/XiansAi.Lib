using Temporalio.Workflows;
using XiansAi.Messaging;
using System.Reflection;
using XiansAi.Activity;
using XiansAi.Knowledge;
using Temporalio.Exceptions;
using XiansAi.Logging;
using XiansAi.Router;

namespace XiansAi.Flow;

// Define delegate for message listening
public delegate Task MessageListenerDelegate(MessageThread messageThread);

public abstract class FlowBase : StaticFlowBase
{
    private readonly Queue<MessageThread> _messageQueue = new Queue<MessageThread>();
    protected List<Type> Capabilities { get; } = new List<Type>();
    protected List<string> _capabilities { get; } = new List<string>();
    private string? _systemPromptKey;
    private readonly Logger<FlowBase> _logger = Logger<FlowBase>.For();

    public FlowBase() : base()  
    {
        // Register the message handler
        _messenger.RegisterHandler(_messageQueue.Enqueue);
        
        // Get the constructor of the derived class
        var derivedType = GetType();
        var constructor = derivedType.GetConstructors().FirstOrDefault();
        
        if (constructor != null)
        {
            // Check if the constructor has KnowledgeAttribute
            var knowledgeAttr = constructor.GetCustomAttribute<KnowledgeAttribute>();
            if (knowledgeAttr != null && knowledgeAttr.Knowledge.Length > 0)
            {

                _systemPromptKey = knowledgeAttr.Knowledge[0];
            }
        }
    }

    protected async Task InitUserConversation(MessageListenerDelegate? messageListener = null)
    {

        _logger.LogInformation($"{GetType().Name} Flow started listening for messages");

        var systemPrompt = await GetSystemPrompt();

        _capabilities.AddRange(Capabilities.Select(t => t.FullName!));

        _ = ListenToUserMessages(messageListener, systemPrompt);
    }

    private async Task ListenToUserMessages(MessageListenerDelegate? messageListener, string systemPrompt)
    {
        while (true)
        {
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

            // Asynchronously process the message
            await ProcessMessage(thread, systemPrompt);

        }
    }

    private async Task ProcessMessage(MessageThread messageThread, string systemPrompt)
    {
        var memoUtil = new MemoUtil(Workflow.Memo);
        _logger.LogDebug($"Processing message from '{messageThread.ParticipantId}' on '{messageThread.ThreadId}'");
        // Route the message to the appropriate flow

        var agentContext = new AgentContext {
            TenantId = memoUtil.GetTenantId(),
            Agent = memoUtil.GetAgent(),
            QueueName = memoUtil.GetQueueName(),
            Assignment = memoUtil.GetAssignment(),
            UserId = memoUtil.GetUserId(),
            WorkflowId = Workflow.Info.WorkflowId,
            WorkflowType = Workflow.Info.WorkflowType
        };
        var response = await _router.RouteAsync(messageThread, systemPrompt, _capabilities.ToArray(), agentContext, new RouterOptions());

        _logger.LogDebug($"Response from router: '{response}' for '{messageThread.ParticipantId}' on '{messageThread.ThreadId}'");

        // Respond to the user
        await messageThread.Respond(response);
    }

    public async Task<string> GetSystemPrompt()
    {
        if (_systemPromptKey == null)
        {
            throw new Exception("System prompt key is not set");
        }
        var knowledge = await new KnowledgeManager().GetKnowledgeAsync(_systemPromptKey);
        return knowledge?.Content ?? throw new ApplicationFailureException($"Knowledge with key {_systemPromptKey} not found");
    }
}

public static class TypeListExtensions
{
    public static List<Type> Add(this List<Type> list, Type type)
    {
        list.Add(type);
        return list;
    }
}