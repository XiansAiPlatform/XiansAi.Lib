using Temporalio.Workflows;
using XiansAi.Messaging;
using System.Reflection;
using XiansAi.Activity;
using XiansAi.Knowledge;
using Temporalio.Exceptions;

namespace XiansAi.Flow;

// Define delegate for message listening
public delegate Task MessageListenerDelegate(MessageThread messageThread);

public abstract class FlowBase : StaticFlowBase
{
    private readonly Queue<MessageThread> _messageQueue = new Queue<MessageThread>();
    protected List<Type> Capabilities { get; } = new List<Type>();

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

    protected virtual async Task RunConversation(MessageListenerDelegate? messageListener = null)
    {

        Console.WriteLine("Pre Consultation Flow started");

        var systemPrompt = await GetSystemPrompt();

        while (true)
        {
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
            _ = ProcessMessage(thread, systemPrompt);

        }
    }

    private async Task ProcessMessage(MessageThread messageThread, string systemPrompt)
    {
        // Route the message to the appropriate flow
        var response = await Router.RouteAsync(messageThread, systemPrompt, Capabilities.Select(t => t.FullName!).ToArray());

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