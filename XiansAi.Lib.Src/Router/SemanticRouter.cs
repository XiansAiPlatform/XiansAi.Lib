using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Temporalio.Workflows;
using XiansAi.Flow;
using XiansAi.Messaging;
using XiansAi.Router.Plugins;

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
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(60) });

        return response;
    }

}

class SemanticRouterImpl: ISemanticRouter
{

    static readonly string INCOMING_MESSAGE = "incoming";

    public async Task<string> RouteAsync(MessageThread messageThread, string systemPrompt, string[] capabilitiesPluginNames, RouterOptions? options)
    {
        if (string.IsNullOrWhiteSpace(systemPrompt))
        {
            throw new Exception("System prompt is required");
        }
        options = options ?? new RouterOptions();
        var kernel = Initialize(options, capabilitiesPluginNames);
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


    private Kernel Initialize(RouterOptions options, string[] capabilitiesPluginNames)
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
            kernel.Plugins.AddFromFunctions(GetPluginName(pluginName), PluginReader.GetFunctions(pluginName));
        }

        return kernel;
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