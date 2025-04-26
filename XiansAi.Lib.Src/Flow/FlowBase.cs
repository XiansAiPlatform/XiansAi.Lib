using Temporalio.Workflows;
using XiansAi.Messaging;
using System.Reflection;
using XiansAi.Activity;
using XiansAi.Knowledge;

namespace XiansAi.Flow;

public abstract class FlowBase : StaticFlowBase
{
    private readonly Queue<MessageThread> _messageQueue = new Queue<MessageThread>();
    protected List<Type> Capabilities { get; } = new List<Type>();
    protected string SystemPrompt { get; set; } = "You are a helpful assistant.";

    private string? _systemPromptKey;

    public FlowBase() : base()  
    {
        // Register the message handler
        Messenger.RegisterHandler(_messageQueue.Enqueue);
        
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

    protected virtual async Task RunConversation()
    {

        Console.WriteLine("Pre Consultation Flow started");

        // Get the system prompt from the knowledge base
        if (_systemPromptKey != null)
        {
            var knowledge = await new KnowledgeManager().GetKnowledgeAsync(_systemPromptKey);
            SystemPrompt = knowledge?.Content ?? throw new Exception($"Knowledge with key {_systemPromptKey} not found");
        }

        while (true)
        {
            // Wait for a message to be added to the queue
            await Workflow.WaitConditionAsync(() => _messageQueue.Count > 0);

            // Get the message from the queue
            var thread = _messageQueue.Dequeue();

            // Asynchronously process the message
            _ = ProcessMessage(thread);

        }
    }

    private async Task ProcessMessage(MessageThread messageThread)
    {
        // Route the message to the appropriate flow
        var response = await Router.RouteAsync(messageThread, SystemPrompt, Capabilities.Select(t => t.FullName!).ToArray());

        // Respond to the user
        await messageThread.Respond(response);
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