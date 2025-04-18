using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Server;
using Temporalio.Activities;
using XiansAi;
using XiansAi.Flow;
using XiansAi.Router;
using XiansAi.Router.Plugins;

public class SendMessageResponse {
    public required string MessageId { get; set; }
    public required string ThreadId { get; set; }
}

public class SystemActivities {
    private readonly ILogger _logger;

    public const string INCOMING_MESSAGE = "incoming";

    public SystemActivities()
    {
        _logger = Globals.LogFactory.CreateLogger<SystemActivities>();
    }

    [Activity]
    public async Task<string> RouteAsync(IMessageThread messageThread, string systemPrompt, string pluginName, RouterOptions? options)
    {
        options = options ?? new RouterOptions();
        var kernel = Initialize(options, pluginName);
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


    private Kernel Initialize(RouterOptions options, string pluginName)
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
        kernel.Plugins.AddFromFunctions("Date_Plugin", DatePlugin.GetFunctions());
        kernel.Plugins.AddFromFunctions("Worker_Plugin", PluginReader.GetFunctions(pluginName));

        return kernel;
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

    [Activity]
    public async Task<SendMessageResponse?> SendMessage(OutgoingMessage message) {
        _logger.LogInformation("Sending message: {Message}", message);
        if (!SecureApi.Instance.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping message send operation");
            return null;
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var response = await client.PostAsJsonAsync("api/agent/conversation/outbound", message);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadFromJsonAsync<SendMessageResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message: {Message}", message);
            throw;
        }
    }
}