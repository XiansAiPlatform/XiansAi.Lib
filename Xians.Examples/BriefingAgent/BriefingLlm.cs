using Anthropic;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xians.Lib.Agents.Messaging;

public sealed class BriefingLlm
{
    /// <summary>
    /// Agent personality only. Channel markdown rules live in
    /// <see cref="MarkdownFormattingPrompt"/> so other agents can reuse them.
    /// </summary>
    internal const string AgentInstructions =
        """
        You are a friendly, helpful briefing assistant. Answer questions, remember what the user shared, and keep replies warm and concise.

        Guidelines:
        - Ask a short clarifying question when the request is ambiguous.
        - Do not invent facts you are unsure about.
        """;

    internal static string SystemPromptFor(ChatChannel channel) =>
        MarkdownFormattingPrompt.Combine(AgentInstructions, channel);

    private readonly AnthropicClient _anthropic;
    private readonly string _modelName;

    public BriefingLlm(string anthropicApiKey, string modelName = "claude-sonnet-4-6")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(anthropicApiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);

        _anthropic = new AnthropicClient { ApiKey = anthropicApiKey };
        _modelName = modelName;
    }

    public async Task<string> RunAsync(
        UserMessageContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var text = context.Message.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return "I didn't catch that — could you send your message again?";
        }

        var channel = MarkdownFormattingPrompt.FromScope(context.Message.Scope);

        // Create the agent per request so history loads the right conversation.
        var agent = _anthropic
            .AsIChatClient(_modelName)
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "BriefingAgent",
                ChatOptions = new ChatOptions
                {
                    Instructions = SystemPromptFor(channel)
                },
                AIContextProviders =
                [
                    new ChatHistoryProvider(context)
                ]
            });

        var response = await agent.RunAsync(text, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(response.Text)
            ? "I wasn't able to produce a reply. Please try again."
            : response.Text;
    }
}

internal sealed class ChatHistoryProvider(UserMessageContext userContext) : AIContextProvider(null, null)
{
    internal const int HistoryPageSize = 10;

    public override IReadOnlyList<string> StateKeys => [];

    protected override async ValueTask<AIContext> InvokingCoreAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        AIContext inputContext = context.AIContext;
#pragma warning disable MAAI001
        var filteredInput = new InvokingContext(context.Agent, context.Session, new AIContext
        {
            Instructions = inputContext.Instructions,
            Messages = inputContext.Messages is not null ? ProvideInputMessageFilter(inputContext.Messages) : null,
            Tools = inputContext.Tools
        });
#pragma warning restore MAAI001

        AIContext additional = await ProvideAIContextAsync(filteredInput, cancellationToken).ConfigureAwait(false);

        string? instructions = inputContext.Instructions;
        string? additionalInstructions = additional.Instructions;
        string? mergedInstructions = (instructions, additionalInstructions) switch
        {
            (null, _) => additionalInstructions,
            (_, null) => instructions,
            _ => instructions + "\n" + additionalInstructions
        };

        IEnumerable<ChatMessage>? historyStamped = additional.Messages?.Select(m =>
            m.WithAgentRequestMessageSource(AgentRequestMessageSourceType.AIContextProvider, typeof(ChatHistoryProvider).FullName));

        IEnumerable<ChatMessage>? inputMessages = inputContext.Messages;
        IEnumerable<ChatMessage>? mergedMessages = (historyStamped, inputMessages) switch
        {
            (null, _) => inputMessages,
            (_, null) => historyStamped,
            _ => historyStamped!.Concat(inputMessages!)
        };

        IEnumerable<AITool>? tools = inputContext.Tools;
        IEnumerable<AITool>? additionalTools = additional.Tools;
        IEnumerable<AITool>? mergedTools = (tools, additionalTools) switch
        {
            (null, _) => additionalTools,
            (_, null) => tools,
            _ => tools!.Concat(additionalTools!)
        };

        return new AIContext
        {
            Instructions = mergedInstructions,
            Messages = mergedMessages,
            Tools = mergedTools
        };
    }

    protected override async ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext _,
        CancellationToken cancellationToken = default)
    {
        var xiansMessages = await userContext.GetChatHistoryAsync(page: 1, pageSize: HistoryPageSize)
            .ConfigureAwait(false);

        var messages = xiansMessages
            .Where(msg => !string.IsNullOrEmpty(msg.Text))
            .OrderBy(msg => msg.CreatedAt)
            .Select(msg => new ChatMessage(ToChatRole(msg.Direction), msg.Text ?? string.Empty))
            .ToList();

        return new AIContext { Messages = messages };
    }

    protected override ValueTask StoreAIContextAsync(InvokedContext _, CancellationToken cancellationToken = default) =>
        default;

    private static ChatRole ToChatRole(string? direction) =>
        "outgoing".Equals(direction, StringComparison.OrdinalIgnoreCase)
        || "outbound".Equals(direction, StringComparison.OrdinalIgnoreCase)
            ? ChatRole.Assistant
            : ChatRole.User;
}
