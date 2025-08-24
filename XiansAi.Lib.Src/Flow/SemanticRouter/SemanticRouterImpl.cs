using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using XiansAi.Messaging;
using XiansAi.Flow.Router.Plugins;
using Server;
using Microsoft.Extensions.DependencyInjection;

namespace XiansAi.Flow.Router;

/// <summary>
/// Core implementation of the semantic router functionality.
/// Handles kernel creation, plugin registration, chat history construction,
/// and message routing with LLM providers (OpenAI/Azure OpenAI).
/// </summary>
internal class SemanticRouterHubImpl : IDisposable
{
    private const string INCOMING_MESSAGE = "incoming";
    private const string OUTGOING_MESSAGE = "outgoing";
    private const string HANDOVER_INSTRUCTION = @"First evaluate if you can accomplish the user request 
        through your primary capabilities. If you can, respond with the answer. 
        If you cannot, check if you can Handover to another bot. If you can, handover to another workflow.
        If you cannot accomplish the user request through your primary capabilities, or handover to another bot,
        respond with a message indicating that you cannot accomplish the user request.";

    private readonly ILogger _logger;
    private readonly FlowServerSettings _settings;
    private readonly LlmConfigurationResolver _configResolver;
    private readonly Lazy<HttpClient> _httpClient;

    public SemanticRouterHubImpl()
    {
        _logger = Globals.LogFactory.CreateLogger<SemanticRouterHubImpl>();
        _settings = LoadSettingsAsync().GetAwaiter().GetResult();
        _configResolver = new LlmConfigurationResolver(_settings);
        _httpClient = new Lazy<HttpClient>(() => new HttpClient());
    }

    private static async Task<FlowServerSettings> LoadSettingsAsync()
    {
        return await SettingsService.GetSettingsFromServer();
    }


    public async Task<string?> ChatCompletionAsync(string prompt, RouterOptions? options = null)
    {
        options ??= new RouterOptions();
        
        try
        {
            var kernel = CreateKernel(options, Array.Empty<Type>(), new KernelPlugins());
            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
            var settings = CreatePromptExecutionSettings(options, enableFunctions: false);
            
            var result = await chatCompletionService.GetChatMessageContentAsync(prompt, settings);
            return result.Content ?? string.Empty;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error in chat completion");
            throw;
        }
    }


    internal async Task<string?> RouteAsync(
        MessageThread messageThread, 
        string systemPrompt, 
        Type[] capabilitiesPluginTypes, 
        RouterOptions options, 
        IChatInterceptor? interceptor, 
        KernelPlugins plugins)
    {
        if (string.IsNullOrWhiteSpace(systemPrompt))
            throw new ArgumentException("System prompt is required", nameof(systemPrompt));

        try
        {
            // Apply incoming message interception
            messageThread = await ApplyIncomingInterceptorAsync(messageThread, interceptor);

            // Create kernel with all plugins
            var kernel = CreateKernelWithPlugins(options, capabilitiesPluginTypes, messageThread, plugins);
            
            // Build chat history and get response
            var chatHistory = await BuildChatHistoryAsync(messageThread, systemPrompt, options.HistorySizeToFetch);
            var settings = CreatePromptExecutionSettings(options, enableFunctions: true);
            
            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
            var result = await chatCompletionService.GetChatMessageContentAsync(chatHistory, settings, kernel);
            var response = result.Content ?? string.Empty;

            // Apply outgoing message interception
            response = await ApplyOutgoingInterceptorAsync(messageThread, response, interceptor);

            // Handle skip response flag
            if (messageThread.SkipResponse)
            {
                messageThread.SkipResponse = false;
                return null;
            }

            return response;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error routing message for workflow {WorkflowType}", messageThread.WorkflowType);
            throw;
        }
    }



    private async Task<MessageThread> ApplyIncomingInterceptorAsync(MessageThread messageThread, IChatInterceptor? interceptor)
    {
        if (interceptor == null) return messageThread;

        try
        {
            return await interceptor.InterceptIncomingMessageAsync(messageThread);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error intercepting incoming message");
            return messageThread; // Continue with original message on error
        }
    }

    private async Task<string> ApplyOutgoingInterceptorAsync(MessageThread messageThread, string response, IChatInterceptor? interceptor)
    {
        if (interceptor == null) return response;

        try
        {
            return await interceptor.InterceptOutgoingMessageAsync(messageThread, response) ?? response;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error intercepting outgoing message");
            return response; // Continue with original response on error
        }
    }

    private Kernel CreateKernelWithPlugins(
        RouterOptions options, 
        Type[] capabilitiesPluginTypes, 
        MessageThread messageThread, 
        KernelPlugins plugins)
    {
        var kernel = CreateKernel(options, capabilitiesPluginTypes, plugins);
        
        // Register non-static plugins with MessageThread context
        RegisterNonStaticPlugins(kernel, capabilitiesPluginTypes, messageThread);
        
        return kernel;
    }

    private Kernel CreateKernel(RouterOptions options, Type[] capabilitiesPluginTypes, KernelPlugins plugins)
    {
        var builder = ConfigureKernelBuilder(options);
        
        // Configure logging
        builder.Services.AddLogging(configure => 
        {
            configure.AddConsole();
            configure.SetMinimumLevel(LogLevel.Information);
        });

        var kernel = builder.Build() ?? throw new InvalidOperationException("Failed to build Semantic Kernel");

        // Add system plugins
        if (plugins.DatePlugin.Enabled)
        {
            _logger.LogDebug("Adding Date plugin");
            kernel.Plugins.AddFromFunctions("System_DatePlugin", DatePlugin.GetFunctions());
        }

        // Add static capability plugins
        RegisterStaticPlugins(kernel, capabilitiesPluginTypes);

        return kernel;
    }

    private IKernelBuilder ConfigureKernelBuilder(RouterOptions options)
    {
        var providerName = _configResolver.GetProviderName(options);
        var builder = Kernel.CreateBuilder();
        
        var httpClient = _httpClient.Value;
        httpClient.Timeout = TimeSpan.FromSeconds(options.HTTPTimeoutSeconds);

        switch (providerName?.ToLower())
        {
            case "openai":
                var modelName = _configResolver.GetModelName(options);
                _logger.LogDebug("Configuring OpenAI with model {ModelName}", modelName);
                builder.AddOpenAIChatCompletion(
                    modelId: modelName,
                    apiKey: _configResolver.GetApiKey(options),
                    httpClient: httpClient);
                break;

            case "azureopenai":
                var deploymentName = _configResolver.GetDeploymentName(options);
                _logger.LogDebug("Configuring Azure OpenAI with deployment {DeploymentName}", deploymentName);
                builder.AddAzureOpenAIChatCompletion(
                    deploymentName: deploymentName,
                    endpoint: _configResolver.GetEndpoint(options),
                    apiKey: _configResolver.GetApiKey(options),
                    httpClient: httpClient);
                break;

            default:
                throw new NotSupportedException($"Unsupported provider: {providerName}. Supported providers are: openai, azureopenai");
        }

        return builder;
    }

    private void RegisterStaticPlugins(Kernel kernel, Type[] capabilitiesPluginTypes)
    {
        foreach (var type in capabilitiesPluginTypes.Where(t => t.IsAbstract && t.IsSealed))
        {
            var functions = PluginReader.GetFunctionsFromStaticType(type);
            kernel.Plugins.AddFromFunctions(type.Name, functions);
        }
    }

    private void RegisterNonStaticPlugins(Kernel kernel, Type[] capabilitiesPluginTypes, MessageThread messageThread)
    {
        foreach (var type in capabilitiesPluginTypes.Where(t => !(t.IsAbstract && t.IsSealed)))
        {
            var instance = TypeActivator.CreateWithOptionalArgs(type, messageThread);
            var functions = PluginReader.GetFunctionsFromInstanceType(type, instance);
            
            // Remove existing plugin if present
            if (kernel.Plugins.TryGetPlugin(type.Name, out var existingPlugin))
            {
                kernel.Plugins.Remove(existingPlugin);
            }
            
            kernel.Plugins.AddFromFunctions(type.Name, functions);
        }
    }

    private OpenAIPromptExecutionSettings CreatePromptExecutionSettings(RouterOptions options, bool enableFunctions)
    {
        return new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = enableFunctions ? FunctionChoiceBehavior.Auto() : FunctionChoiceBehavior.None(),
            MaxTokens = options.MaxTokens,
            Temperature = options.Temperature
        };
    }

    private async Task<ChatHistory> BuildChatHistoryAsync(MessageThread messageThread, string systemPrompt, int historySizeToFetch)
    {
        var chatHistory = new ChatHistory(systemPrompt);
        chatHistory.AddSystemMessage(HANDOVER_INSTRUCTION);

        var historyFromServer = await messageThread.FetchThreadHistory(1, historySizeToFetch);
        historyFromServer.Reverse(); // Put most recent messages last

        foreach (var message in historyFromServer)
        {
            if (string.IsNullOrWhiteSpace(message.Text))
                continue;

            var chatMessage = CreateChatMessage(message);
            if (chatMessage != null)
            {
                chatHistory.Add(chatMessage);
            }
        }

        return chatHistory;
    }

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates
    private ChatMessageContent? CreateChatMessage(DbMessage message)
    {
        return message.Direction switch
        {
            var d when d.Equals(INCOMING_MESSAGE, StringComparison.OrdinalIgnoreCase) => new()
            {
                Role = AuthorRole.User,
                Content = message.Text,
                AuthorName = SanitizeName(message.ParticipantId)
            },
            var d when d.Equals(OUTGOING_MESSAGE, StringComparison.OrdinalIgnoreCase) => new()
            {
                Role = AuthorRole.Assistant,
                Content = message.Text,
                AuthorName = SanitizeName(message.WorkflowType)
            },
            _ => null // Skip other message types like "Handovers"
        };
    }
#pragma warning restore SKEXP0001

    private static string SanitizeName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return "user";

        // Replace spaces and disallowed characters with underscores
        return System.Text.RegularExpressions.Regex.Replace(name, @"[\s<|\\/>]", "_");
    }

    public void Dispose()
    {
        if (_httpClient.IsValueCreated)
        {
            _httpClient.Value?.Dispose();
        }
    }
}