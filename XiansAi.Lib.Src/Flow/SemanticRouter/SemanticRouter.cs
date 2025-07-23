using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Temporalio.Workflows;
using XiansAi.Messaging;
using XiansAi.Flow.Router.Plugins;
using Server;
using Microsoft.Extensions.DependencyInjection;

namespace XiansAi.Flow.Router;


public static class SemanticRouter
{

    public static async Task<string> RouteAsync(MessageThread messageThread, string systemPrompt)
    {
        // Go through a Temporal activity to perform IO operations
        var response = await Workflow.ExecuteActivityAsync(
            (SystemActivities a) => a.RouteAsync(messageThread, systemPrompt),
            new SystemActivityOptions());

        return response;
    }

}

class SemanticRouterImpl
{

    static readonly string INCOMING_MESSAGE = "incoming";
    static readonly string OUTGOING_MESSAGE = "outgoing";
    // Static dictionary to cache kernels by workflow type
    private static readonly Dictionary<string, Kernel> _kernelCache = new Dictionary<string, Kernel>();
    private static readonly object _kernelCacheLock = new object();
    private readonly ILogger _logger;
    private readonly FlowServerSettings _settings;
    private readonly string? _llmProvider;
    private readonly string? _llmApiKey;
    private readonly string? _llmEndpoint;
    private readonly string? _llmDeploymentName;

    public SemanticRouterImpl()
    {
        _logger = Globals.LogFactory.CreateLogger<SemanticRouterImpl>();
        _settings = SettingsService.GetSettingsFromServer().GetAwaiter().GetResult();

        // The name of the LLM provider, e.g., "openai", "azureopenai"
        _llmProvider = Environment.GetEnvironmentVariable("LLM_PROVIDER");

        // The API key for the LLM provider
        _llmApiKey = Environment.GetEnvironmentVariable("LLM_API_KEY");

        // The endpoint for the LLM provider, if any, e.g., "https://api.openai.com"
        _llmEndpoint = Environment.GetEnvironmentVariable("LLM_ENDPOINT");

        // The deployment name for the LLM provider, if any, e.g., "gpt-3.5-turbo"
        _llmDeploymentName = Environment.GetEnvironmentVariable("LLM_DEPLOYMENT_NAME");
    }

    public async Task<string> RouteAsync(MessageThread messageThread, string systemPrompt, Type[] capabilitiesPluginTypes)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                throw new Exception("System prompt is required");
            }

            var options = new RouterOptions {
                ModelName = _settings.ModelName,
                HistorySizeToFetch = 10
            };

            var kernel = Initialize(messageThread.WorkflowType, options, capabilitiesPluginTypes, messageThread);

            var chatHistory = await ConstructHistory(messageThread, systemPrompt, options.HistorySizeToFetch);
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
        catch (Exception e)
        {
            _logger.LogError(e, "Error routing message");
            throw;
        }
    }

    private Kernel GetOrCreateCacheableKernel(string workflowType, RouterOptions options, Type[] capabilitiesPluginTypes)
    {

        if (string.IsNullOrEmpty(workflowType))
        {
            // If no workflow type, create a new non-cached kernel
            return InitializeCacheable(options, capabilitiesPluginTypes);
        }

        lock (_kernelCacheLock)
        {
            if (_kernelCache.TryGetValue(workflowType, out var cachedKernel))
            {
                return cachedKernel;
            }

            var newKernel = InitializeCacheable(options, capabilitiesPluginTypes);
            _kernelCache[workflowType] = newKernel;
            return newKernel;
        }
    }

    private Kernel Initialize(string workflowType, RouterOptions options, Type[] capabilitiesPluginTypes, MessageThread messageThread)
    {
        var kernel = GetOrCreateCacheableKernel(workflowType, options, capabilitiesPluginTypes);

        foreach (var type in capabilitiesPluginTypes)
        {

            if (!(type.IsAbstract && type.IsSealed))
            {
                _logger.LogInformation("Getting functions from instance type {PluginType}", type.Name);
                var instance = Activator.CreateInstance(type, new object[] { messageThread }) ?? throw new Exception($"Failed to create instance of {type.Name}");
                var functions = PluginReader.GetFunctionsFromInstanceType(type, instance);
                kernel.Plugins.TryGetPlugin(type.Name, out var plugin);
                if (plugin != null)
                {
                    kernel.Plugins.Remove(plugin);
                }
                kernel.Plugins.AddFromFunctions(type.Name, functions);
            }
        }

        return kernel;
    }

    private Kernel InitializeCacheable(RouterOptions options, Type[] capabilitiesPluginTypes)
    {
         string GetApiKey() =>
            !string.IsNullOrEmpty(_llmApiKey) ? _llmApiKey :
            !string.IsNullOrEmpty(_settings.ApiKey) ? _settings.ApiKey :
            throw new Exception("LLM API Key is not available");

        string GetProviderName() =>
            !string.IsNullOrEmpty(_llmProvider) ? _llmProvider :
            !string.IsNullOrEmpty(_settings.ProviderName) ? _settings.ProviderName :
            throw new Exception("LLM Provider is not available");

        string GetDeploymentName() =>
            !string.IsNullOrWhiteSpace(_llmDeploymentName) ? _llmDeploymentName :
            _settings.AdditionalConfig != null &&
            _settings.AdditionalConfig.TryGetValue("DeploymentName", out var configDeploymentName) &&
            !string.IsNullOrWhiteSpace(configDeploymentName) ? configDeploymentName :
            throw new Exception("LLM DeploymentName is not available");

        string GetEndpoint() =>
            !string.IsNullOrWhiteSpace(_llmEndpoint) ? _llmEndpoint :
            !string.IsNullOrWhiteSpace(_settings.BaseUrl) ? _settings.BaseUrl :
            throw new Exception("LLM BaseUrl is not available");

        var providerName = GetProviderName();
        var builder = Kernel.CreateBuilder();

        switch (providerName?.ToLower())
        {
            case "openai":
                builder.Services.AddOpenAIChatCompletion(
                    modelId: options.ModelName,
                    apiKey: GetApiKey()
                );
                break;

            case "azureopenai":
                builder.AddAzureOpenAIChatCompletion(
                    deploymentName: GetDeploymentName(),
                    endpoint: GetEndpoint(),
                    apiKey: GetApiKey()
                );
                break;

            default:
                throw new Exception($"Unsupported provider: {_settings.ProviderName}. Supported providers are: openai, azureopenai");
        }

        // add logging
        builder.Services.AddLogging(configure => configure.AddConsole());
        builder.Services.AddLogging(configure => configure.SetMinimumLevel(LogLevel.Information));

        var kernel = builder.Build() ?? throw new Exception("Semantic Kernel is not built");
        // add system plugins
        kernel.Plugins.AddFromFunctions("System_DatePlugin", DatePlugin.GetFunctions());

        // add capabilities plugins
        foreach (var type in capabilitiesPluginTypes)
        {

            if (type.IsAbstract && type.IsSealed)
            {
                // static plugin
                _logger.LogInformation("Getting functions from static type {PluginType}", type.Name);
                var functions = PluginReader.GetFunctionsFromStaticType(type);
                kernel.Plugins.AddFromFunctions(type.Name, functions);
            }
        }

        return kernel;
    }

    private async Task<ChatHistory> ConstructHistory(MessageThread messageThread, string systemPrompt, int historySizeToFetch)
    {

        var chatHistory = new ChatHistory(systemPrompt);
        chatHistory.AddSystemMessage(@"First evaluate if you can accomplish the user request 
        through your primary capabilities. If you can, respond with the answer. 
        If you cannot, check if you can Handover to another bot. If you can, handover to another workflow.
        If you cannot accomplish the user request through your primary capabilities, or handover to another bot,
        respond with a message indicating that you cannot accomplish the user request.");

        var historyFromServer = await messageThread.FetchThreadHistory(1, historySizeToFetch);
        // Reverse the history to put the most recent messages last
        historyFromServer.Reverse();

        foreach (var message in historyFromServer)
        {
            if (string.IsNullOrEmpty(message.Text?.Trim()))
            {
                continue;
            }
            if (message.Direction.Equals(INCOMING_MESSAGE, StringComparison.OrdinalIgnoreCase))
            {
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                chatHistory.Add(
                    new()
                    {
                        Role = AuthorRole.User,
                        Content = message.Text,
                        AuthorName = SanitizeName(message.ParticipantId)
                    }
                );
            }
            else if (message.Direction.Equals(OUTGOING_MESSAGE, StringComparison.OrdinalIgnoreCase))
            {
                chatHistory.Add(
                    new()
                    {
                        Role = AuthorRole.Assistant,
                        Content = message.Text,
                        AuthorName = SanitizeName(message.WorkflowType)
                    }
                );
            }
            else
            {
                // skip the messages such as "Handovers"
                continue;
            }
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        }

        return chatHistory;
    }

    private string SanitizeName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return "user";

        // Replace spaces with underscores and remove any disallowed characters
        return System.Text.RegularExpressions.Regex.Replace(name, @"[\s<|\\/>]", "_");
    }
}