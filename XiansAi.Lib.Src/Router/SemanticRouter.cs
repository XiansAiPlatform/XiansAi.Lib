using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Temporalio.Workflows;
using XiansAi.Messaging;
using XiansAi.Router.Plugins;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;
using Server;

namespace XiansAi.Router;

public interface ISemanticRouter
{
    Task<string> RouteAsync(MessageThread messageThread, string systemPrompt, string[] capabilitiesPluginNames, RouterOptions? options = null);
}

public class SemanticRouter : ISemanticRouter
{

    public async Task<string> RouteAsync(MessageThread messageThread, string systemPrompt, string[] capabilitiesPluginNames, RouterOptions? options = null)
    {
        // Go through a Temporal activity to perform IO operations
        var response = await Workflow.ExecuteActivityAsync(
            (SystemActivities a) => a.RouteAsync(messageThread, systemPrompt, capabilitiesPluginNames, options),
            new SystemActivityOptions());

        return response;
    }

}

class SemanticRouterImpl : ISemanticRouter
{

    static readonly string INCOMING_MESSAGE = "incoming";

    public async Task<string> RouteAsync(MessageThread messageThread, string systemPrompt, string[] capabilitiesPluginNames, RouterOptions? options)
    {
        if (string.IsNullOrWhiteSpace(systemPrompt))
        {
            throw new Exception("System prompt is required");
        }
        options = options ?? new RouterOptions();
        var kernel = await Initialize(options, capabilitiesPluginNames, messageThread);
        var chatHistory = await ExtractHistory(messageThread, systemPrompt, options.HistorySizeToFetch);
        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            MaxTokens = options.MaxTokens,
            Temperature = options.Temperature
        };
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        var result = await chatCompletionService.GetChatMessageContentAsync(chatHistory, settings, kernel);
        return result.Content ?? string.Empty;
    }


    private async Task<Kernel> Initialize(RouterOptions options, string[] capabilitiesPluginNames, MessageThread messageThread)
    {
        // Load environment variables from .env file
        var apiKey = PlatformConfig.OPENAI_API_KEY ?? string.Empty;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new Exception("OPENAI_API_KEY is not defined in the environment.");
        }

        var builder = Kernel.CreateBuilder();

        builder.Services.AddOpenAIChatCompletion(
            modelId: options.ModelName,
            apiKey: apiKey
        );

        var kernel = builder.Build() ?? throw new Exception("Semantic Kernel is not built");
        // add system plugins
        kernel.Plugins.AddFromFunctions("System_DatePlugin", DatePlugin.GetFunctions());

        // add capabilities plugins
        foreach (var pluginName in capabilitiesPluginNames)
        {
            kernel.Plugins.AddFromFunctions(GetPluginName(pluginName), PluginReader.GetFunctions(pluginName, messageThread));
        }

        await ConnectToMcpServer(kernel);

        return kernel;
    }

    private async Task ConnectToMcpServer(Kernel kernel)
    {
        if (PlatformConfig.MCP_SERVER_URL != null)
        {
            var sseOptions = new SseClientTransportOptions
            {
                Endpoint = new Uri(PlatformConfig.MCP_SERVER_URL),
                Name = "SSE-MCP"
            };

            // Initialize your SecureApi (returns the HttpClient)
            var httpClient = SecureApi.InitializeClient(
                PlatformConfig.APP_SERVER_API_KEY ?? throw new ArgumentNullException(nameof(PlatformConfig.APP_SERVER_API_KEY)),
                PlatformConfig.APP_SERVER_URL ?? throw new ArgumentNullException(nameof(PlatformConfig.APP_SERVER_URL))
            );

            
            var sseTransport = new SseClientTransport(sseOptions, httpClient);

            var mcpClient = await McpClientFactory.CreateAsync(sseTransport);
            var tools = await mcpClient.ListToolsAsync();
            DisplayTools(tools);

            #pragma warning disable SKEXP0001
            var kernelFunctions = tools.Select(tool => tool.AsKernelFunction());
            #pragma warning restore SKEXP0001

            kernel.Plugins.AddFromFunctions("McpTools", kernelFunctions);
        }
        else
        {
            Console.WriteLine("MCP_SERVER_URL is not set. Skipping MCP connection.");
        }
    }

    private static void DisplayTools(IList<McpClientTool> tools)
    {
        Console.WriteLine("Available MCP tools:");
        foreach (var tool in tools)
        {
            Console.WriteLine($"- Name: {tool.Name}, Description: {tool.Description}");
        }
        Console.WriteLine();
    }

    private string GetPluginName(string input)
    {
        return input.Split('.').Last();
    }

    private async Task<ChatHistory> ExtractHistory(IMessageThread messageThread, string systemPrompt, int historySizeToFetch)
    {

        var chatHistory = new ChatHistory(systemPrompt);

        var historyFromServer = await messageThread.GetThreadHistory(1, historySizeToFetch);
        // Reverse the history to put the most recent messages last
        historyFromServer.Reverse();

        foreach (var message in historyFromServer)
        {
            if (message.Direction == INCOMING_MESSAGE)
            {
                chatHistory.AddUserMessage(message.Content);
            }
            else
            {
                chatHistory.AddAssistantMessage(message.Content);
            }
        }

        return chatHistory;
    }
}