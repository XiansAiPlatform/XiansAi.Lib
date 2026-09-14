using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OpenAI;
using OpenAI.Chat;
using System.Text.Json;
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
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(GetCurrentDateTime),
            AIFunctionFactory.Create(_webTools.SearchWeb),
            AIFunctionFactory.Create(_webTools.ReadWebPage)
        };
        var mcpClients = await LoadMcpToolsAsync(tools);

        try
        {
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
        finally
        {
            foreach (var client in mcpClients)
                await client.DisposeAsync();
        }
    }

    private static async Task<List<McpClient>> LoadMcpToolsAsync(List<AITool> tools)
    {
        var clients = new List<McpClient>();
        var rules = await XiansContext.CurrentAgent.Knowledge.GetAsync("Rules");
        if (string.IsNullOrWhiteSpace(rules?.Content)) return clients;

        RulesConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<RulesConfig>(rules.Content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException exception)
        {
            Console.Error.WriteLine($"Ignoring invalid Rules JSON: {exception.Message}");
            return clients;
        }

        foreach (var server in config?.McpServers.Where(server => server.Enabled) ?? [])
        {
            if (string.IsNullOrWhiteSpace(server.Name) ||
                !Uri.TryCreate(server.Url, UriKind.Absolute, out var endpoint) ||
                endpoint.Scheme is not ("http" or "https"))
            {
                Console.Error.WriteLine($"Skipping MCP server '{server.Name}': invalid HTTP URL.");
                continue;
            }

            try
            {
                var client = await McpClient.CreateAsync(new HttpClientTransport(new()
                {
                    Name = server.Name,
                    Endpoint = endpoint
                }));
                clients.Add(client);
                tools.AddRange(await client.ListToolsAsync());
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Skipping MCP server '{server.Name}': {exception.Message}");
            }
        }

        return clients;
    }

    private static string GetCurrentDateTime() => DateTimeOffset.Now.ToString("O");

    private sealed class RulesConfig
    {
        public List<McpServerConfig> McpServers { get; init; } = [];
    }

    private sealed class McpServerConfig
    {
        public string Name { get; init; } = "";
        public string Url { get; init; } = "";
        public bool Enabled { get; init; } = true;
    }
}
