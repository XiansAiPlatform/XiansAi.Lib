using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Server;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Agents;
using XiansAi.Messaging;
using XiansAi.Flow.Router.Plugins;
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

    private readonly ILogger _logger;
    //private readonly ServerSettings _settings;
    //private readonly LlmConfigurationResolver _configResolver;
    private readonly Lazy<HttpClient> _httpClient;

    public SemanticRouterHubImpl()
    {
        _logger = Globals.LogFactory.CreateLogger<SemanticRouterHubImpl>();
        _httpClient = new Lazy<HttpClient>(() => new HttpClient());
    }

    private ChatCompletionAgent CreateChatCompletionAgent(
        string name,
        string instructions,
        Kernel kernel,
        RouterOptions options,
        bool enableFunctions = true)
    {
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = enableFunctions ? FunctionChoiceBehavior.Auto() : FunctionChoiceBehavior.None(),
            MaxTokens = options.MaxTokens,
            Temperature = options.Temperature
        };

        return new ChatCompletionAgent
        {
            Name = name,
            Instructions = instructions,
            Kernel = kernel,
            Arguments = new KernelArguments(executionSettings)
        };
    }


    public async Task<string?> CompletionAsync(string prompt, string? systemInstruction, RouterOptions? options = null)
    {
        options ??= new RouterOptions();
        
        try
        {
            var kernel = BuildKernelAsync(options);
            var agent = CreateChatCompletionAgent(
                name: "CompletionAgent",
                instructions: systemInstruction ?? "You are a helpful assistant. Perform the user's request accurately and concisely.",
                kernel: kernel,
                options: options,
                enableFunctions: false
            );
            
            // Create a simple thread for single-turn chat completion
            var chatHistory = new ChatHistory(systemInstruction ?? "You are a helpful assistant. Perform the user's request accurately and concisely.");
            long historyMessageCount = chatHistory.Count;
            // Apply chat history reduction if token limits are configured
            if (options.TokenLimit > 0)
            {
                var chatService = kernel.GetRequiredService<IChatCompletionService>();
                var reducerLogger = Globals.LogFactory.CreateLogger<ChatHistoryReducer>();
                var reducer = new ChatHistoryReducer(reducerLogger, options, chatService);
                chatHistory = await reducer.ReduceAsync(chatHistory);
            }

            long reducedMessageCount = chatHistory.Count;
            var thread = new ChatHistoryAgentThread(chatHistory);
            var userMessage = new ChatMessageContent(AuthorRole.User, prompt);
            
            // Measure response time for the LLM call
            var stopwatch = Stopwatch.StartNew();
            var responses = new List<ChatMessageContent>();
            await foreach (var response in agent.InvokeAsync(userMessage, thread))
            {
                responses.Add(response);
            }
            stopwatch.Stop();
            
            var completion = string.Join(" ", responses.Select(r => r.Content));

            return completion;
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Error in LLM completion in processing message: `{prompt}` and system instruction: `{systemInstruction}`");
            throw;
        }
    }


    internal async Task<string?> RouteAsync(
        MessageThread messageThread, 
        string systemPrompt, 
        RouterOptions options, 
        List<Type> capabilitiesPluginTypes, 
        IChatInterceptor? interceptor, 
        List<IKernelModifier> kernelModifiers)
    {
        if (string.IsNullOrWhiteSpace(systemPrompt))
            throw new ArgumentException("System prompt is required", nameof(systemPrompt));

        try
        {
            // Sanitize the agent name to comply with OpenAI requirements
            var agentId = $"RouterAgent_{SanitizeName(messageThread.WorkflowId)}";

            // Create kernel with all plugins
            var kernel = await CreateKernelWithPlugins(options, capabilitiesPluginTypes.ToArray(), messageThread, kernelModifiers);

            // Create the agent with full instructions
            var agent = CreateChatCompletionAgent(
                name: agentId,
                instructions: systemPrompt,
                kernel: kernel,
                options: options,
                enableFunctions: true
            );
            
            // Build chat history and create thread with proper history
            var chatHistory = await BuildChatHistoryAsync(messageThread, systemPrompt, options.HistorySizeToFetch);
            long historyMessageCount = chatHistory.Count;
            // Apply chat history reduction if token limits are configured
            if (options.TokenLimit > 0)
            {
                var chatService = kernel.GetRequiredService<IChatCompletionService>();
                var reducerLogger = Globals.LogFactory.CreateLogger<ChatHistoryReducer>();
                var reducer = new ChatHistoryReducer(reducerLogger, options, chatService);
                
                // Apply reduction to prevent token limit issues
                chatHistory = await reducer.ReduceAsync(chatHistory);
            }
            long reducedMessageCount = chatHistory.Count;
            var thread = new ChatHistoryAgentThread(chatHistory);
            
            // Create the current user message from the latest message
            var currentMessage = new ChatMessageContent(
                AuthorRole.User, 
                messageThread.LatestMessage?.Content ?? string.Empty
            );

            // Apply incoming message interception
            messageThread = await ApplyIncomingInterceptorAsync(messageThread, interceptor);
            
            // Measure response time for the LLM call
            var stopwatch = Stopwatch.StartNew();
            var responses = new List<ChatMessageContent>();
            await foreach (var item in agent.InvokeAsync(currentMessage, thread))
            {
                responses.Add(item);
            }
            stopwatch.Stop();
            
            var response = string.Join(" ", responses.Select(r => r.Content));

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

    private async Task<Kernel> CreateKernelWithPlugins(
        RouterOptions options, 
        Type[] capabilitiesPluginTypes, 
        MessageThread messageThread, 
        List<IKernelModifier> kernelModifiers)
    {
        var kernel = BuildKernelAsync(options);
        
        // Register non-static plugins with MessageThread context
        RegisterNonStaticPlugins(kernel, capabilitiesPluginTypes, messageThread);

        // Add static capability plugins
        RegisterStaticPlugins(kernel, capabilitiesPluginTypes);

        // Add system plugins
        _logger.LogDebug("Adding Date plugin");
        kernel.Plugins.AddFromFunctions("System_DatePlugin", DatePlugin.GetFunctions(options));
        

        // Apply kernel modifiers sequentially if provided
        foreach (var kernelModifier in kernelModifiers ?? new List<IKernelModifier>())
        {
            try
            {
                _logger.LogDebug("Modifying kernel with {KernelModifierType}", kernelModifier.GetType().Name);
                kernel = await kernelModifier.ModifyKernelAsync(kernel, messageThread);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error modifying kernel with {KernelModifierType}", kernelModifier.GetType().Name);
            }
        }
        
        return kernel;
    }

    private Kernel BuildKernelAsync(RouterOptions options)
    {
        var builder = ConfigureKernelBuilder(options);

        // Configure logging
        builder.Services.AddLogging(configure =>
        {
            configure.AddConsole();
            configure.SetMinimumLevel(LogLevel.Information);

            // Add any additional logger providers registered by consumers
            foreach (var provider in Globals.AdditionalLoggerProviders)
                configure.AddProvider(provider);
        });

        // To avoid infinite loops, we need to add the termination filter as a scoped service.
        builder.Services.AddScoped<IAutoFunctionInvocationFilter>(_ => new TerminationFilter(options.MaxConsecutiveCalls));

        var kernel = builder.Build() ?? throw new InvalidOperationException("Failed to build Semantic Kernel");

        return kernel;
    }

    private IKernelBuilder ConfigureKernelBuilder(RouterOptions options)
    {
        var configResolver = new LlmConfigurationResolver();
        var providerName = configResolver.GetProviderName(options);
        var builder = Kernel.CreateBuilder();
        
        var httpClient = _httpClient.Value;
        httpClient.Timeout = TimeSpan.FromSeconds(options.HTTPTimeoutSeconds);

        switch (providerName?.ToLower())
        {
            case "openai":
                var modelName = configResolver.GetModelName(options);
                builder.AddOpenAIChatCompletion(
                    modelId: modelName,
                    apiKey: configResolver.GetApiKey(options),
                    httpClient: httpClient);
                _logger.LogDebug("Configured OpenAI with model {ModelName}", modelName);

                break;

            case "azureopenai":
                var deploymentName = configResolver.GetDeploymentName(options);
                builder.AddAzureOpenAIChatCompletion(
                    deploymentName: deploymentName,
                    endpoint: configResolver.GetEndpoint(options),
                    apiKey: configResolver.GetApiKey(options),
                    httpClient: httpClient);
                 _logger.LogDebug("Configured Azure OpenAI with deployment {DeploymentName}", deploymentName);
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
            _logger.LogDebug("Adding static plugin {PluginType}", type.Name);
            var functions = PluginReader.GetFunctionsFromStaticType(type);
            kernel.Plugins.AddFromFunctions(type.Name, functions);
        }
    }

    private void RegisterNonStaticPlugins(Kernel kernel, Type[] capabilitiesPluginTypes, MessageThread messageThread)
    {
        foreach (var type in capabilitiesPluginTypes.Where(t => !(t.IsAbstract && t.IsSealed)))
        {
            _logger.LogDebug("Adding non-static plugin {PluginType}", type.Name);
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

    private async Task<ChatHistory> BuildChatHistoryAsync(MessageThread messageThread, string systemPrompt, int historySizeToFetch)
    {
        var chatHistory = new ChatHistory(systemPrompt);

        // If the latest message is stateless, don't add any history
        if (messageThread.LatestMessage.Hint == Constants.HINT_STATELESS)
        {
            _logger.LogDebug("Latest message is stateless, not adding any history");
            return chatHistory;
        }
        
        var historyFromServer = await messageThread.FetchThreadHistory(1, historySizeToFetch);
        historyFromServer.Reverse(); // Put most recent messages last

        // Get the current message content to exclude it from history
        var currentMessageContent = messageThread.LatestMessage?.Content?.Trim();

        for (int i = 0; i < historyFromServer.Count; i++)
        {
            var message = historyFromServer[i];
            
            if (string.IsNullOrWhiteSpace(message.Text))
                continue;

            // Only check the last message in history for duplication with current message
            bool isLastMessage = i == historyFromServer.Count - 1;
            if (isLastMessage && 
                !string.IsNullOrEmpty(currentMessageContent) && 
                message.Text?.Trim().Equals(currentMessageContent, StringComparison.OrdinalIgnoreCase) == true &&
                message.Direction.Equals(INCOMING_MESSAGE, StringComparison.OrdinalIgnoreCase))
            {
                continue; // Skip only the last message if it matches the current one
            }

            var chatMessage = CreateChatMessage(message);
            if (chatMessage != null)
            {
                chatHistory.Add(chatMessage);
            }
        }

        return chatHistory;
    }

    private ChatMessageContent? CreateChatMessage(DbMessage message)
    {
        return message.Direction switch
        {
            var d when d.Equals(INCOMING_MESSAGE, StringComparison.OrdinalIgnoreCase) => new()
            {
                Role = AuthorRole.User,
                Content = message.Text
                // AuthorName removed to avoid validation issues with ChatCompletionAgent
            },
            var d when d.Equals(OUTGOING_MESSAGE, StringComparison.OrdinalIgnoreCase) => new()
            {
                Role = AuthorRole.Assistant,
                Content = message.Text
                // AuthorName removed to avoid validation issues with ChatCompletionAgent
            },
            _ => null // Skip other message types like "Handovers"
        };
    }

    private static string SanitizeName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return "user";

        // OpenAI API only allows alphanumeric characters, underscores, and hyphens
        // First replace spaces and common separators with underscores
        var sanitized = System.Text.RegularExpressions.Regex.Replace(name, @"[\s:;,\.]", "_");
        
        // Then remove any remaining characters that aren't alphanumeric, underscore, or hyphen
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"[^a-zA-Z0-9_-]", "");
        
        // Ensure we don't have multiple underscores in a row
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"_{2,}", "_");
        
        // Trim underscores from start and end
        sanitized = sanitized.Trim('_', '-');
        
        // If the result is empty, return a default
        return string.IsNullOrEmpty(sanitized) ? "default_agent" : sanitized;
    }

    public void Dispose()
    {
        if (_httpClient.IsValueCreated)
        {
            _httpClient.Value?.Dispose();
        }
    }
}
