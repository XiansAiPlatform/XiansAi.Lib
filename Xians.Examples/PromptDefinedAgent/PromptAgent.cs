using System.Collections.Concurrent;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using PromptDefinedAgent.Configuration;
using PromptDefinedAgent.Mcp;
using PromptDefinedAgent.Messaging;
using PromptDefinedAgent.Scheduling;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Messaging;

public sealed class PromptAgent : IAsyncDisposable
{
    private readonly ChatClient _chatClient;
    private readonly ConcurrentDictionary<McpCacheKey, Lazy<Task<McpToolCollection>>> _mcpCache = new();

    public PromptAgent(string apiKey)
    {
        _chatClient = new OpenAIClient(apiKey).GetChatClient("gpt-4o-mini");
    }

    public Task<string> RunAsync(UserMessageContext context) =>
        RunAsync(
            context.Message.Text,
            context,
            CreateToolContext(context.Message.ParticipantId, context.Message.Scope),
            (data, text) => context.SendToolExecAsync(data, text));

    public Task<string> RunScheduledAsync(ScheduledPromptRequest request) =>
        RunAsync(request.ToAgentPrompt(), null, CreateToolContext(request.ParticipantId, request.Scope), (data, text) =>
            XiansContext.Messaging.SendToolCallMessageAsSupervisorAsync(
                text, data, scope: request.Scope, participantId: request.ParticipantId));

    private async Task<string> RunAsync(
        string prompt,
        UserMessageContext? context,
        XiansToolContext toolContext,
        Func<object, string, Task> sendToolEvent)
    {
        var configuredPrompt = await XiansContext.CurrentAgent.Knowledge.GetAsync("system-prompt");
        var mcpTools = await GetMcpToolsAsync(toolContext);
        var instructions = configuredPrompt?.Content ?? "You are a helpful assistant.";
        if (context is not null) instructions += "\n\n" + SchedulingInstructions.ForChat();

        var options = new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions
            {
                Instructions = instructions,
                Tools = new List<AITool>(mcpTools.GetTools(toolContext))
            }
        };
        if (context is not null) options.ChatMessageStoreFactory = _ => new ConversationStore(context);
        var agent = _chatClient.CreateAIAgent(options);

        return await ToolCallTracker.RunAsync(agent.RunStreamingAsync(prompt), sendToolEvent);
    }

    private async Task<McpToolCollection> GetMcpToolsAsync(XiansToolContext context)
    {
        var key = new McpCacheKey(context.TenantId, context.AgentName, context.ActivationName);
        var candidate = new Lazy<Task<McpToolCollection>>(
            LoadMcpToolsAsync,
            LazyThreadSafetyMode.ExecutionAndPublication);
        var cached = _mcpCache.GetOrAdd(key, candidate);

        try
        {
            return await cached.Value;
        }
        catch
        {
            _mcpCache.TryRemove(key, out _);
            throw;
        }
    }

    private static async Task<McpToolCollection> LoadMcpToolsAsync()
    {
        var rules = await RulesConfig.LoadAsync();
        return await McpToolProvider.LoadAsync(rules);
    }

    private static XiansToolContext CreateToolContext(string participantId, string? scope) => new(
        XiansContext.TenantId,
        XiansContext.CurrentAgent.Name,
        XiansContext.SafeIdPostfix ?? throw new InvalidOperationException("The current activation is unavailable."),
        participantId,
        scope);

    public async ValueTask DisposeAsync()
    {
        foreach (var lazyTools in _mcpCache.Values)
        {
            if (!lazyTools.IsValueCreated) continue;
            try
            {
                await (await lazyTools.Value).DisposeAsync();
            }
            catch
            {
                // A failed initialization has no client collection to dispose.
            }
        }

        _mcpCache.Clear();
    }

    private sealed record McpCacheKey(string TenantId, string AgentName, string ActivationName);
}
