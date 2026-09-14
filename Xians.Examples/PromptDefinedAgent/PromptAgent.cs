using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using PromptDefinedAgent.Mcp;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Messaging;

internal sealed class PromptAgent
{
    private readonly ChatClient _chatClient;
    private readonly WebTools _webTools;

    public PromptAgent(string apiKey, string webSearchApiKey, string model = "gpt-4o-mini")
    {
        _chatClient = new OpenAIClient(apiKey).GetChatClient(model);
        _webTools = new WebTools(webSearchApiKey);
    }

    public async Task<string> RunAsync(UserMessageContext context)
    {
        var configuredPrompt = await XiansContext.CurrentAgent.Knowledge.GetAsync("system-prompt");
        await using var mcpTools = await McpToolProvider.LoadAsync();
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(GetCurrentDateTime),
            AIFunctionFactory.Create(_webTools.SearchWeb),
            AIFunctionFactory.Create(_webTools.ReadWebPage)
        };
        tools.AddRange(mcpTools.Tools);

        var agent = _chatClient.CreateAIAgent(new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions
            {
                Instructions = configuredPrompt?.Content ?? "You are a helpful assistant.",
                Tools = tools
            },
            ChatMessageStoreFactory = _ => new ConversationStore(context)
        });

        return (await agent.RunAsync(context.Message.Text)).Text;
    }

    private static string GetCurrentDateTime() => DateTimeOffset.Now.ToString("O");
}
