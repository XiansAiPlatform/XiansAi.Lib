using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Agents;
using XiansAi.Messaging;
using XiansAi.Flow.Router.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace XiansAi.Flow.Router.Orchestration.SemanticKernel;

/// <summary>
/// Semantic Kernel implementation of the AI orchestrator.
/// Handles kernel creation, plugin registration, chat history construction,
/// and message routing with LLM providers (OpenAI/Azure OpenAI).
/// </summary>
public class SemanticKernelOrchestrator : IAIOrchestrator
{
    private const string INCOMING_MESSAGE = "incoming";
    private const string OUTGOING_MESSAGE = "outgoing";

    private readonly ILogger _logger;
    private readonly Lazy<HttpClient> _httpClient;

    public SemanticKernelOrchestrator()
    {
        _logger = Globals.LogFactory.CreateLogger<SemanticKernelOrchestrator>();
        _httpClient = new Lazy<HttpClient>(() => new HttpClient());
    }

    public async Task<string?> RouteAsync(OrchestratorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SystemPrompt))
            throw new ArgumentException("System prompt is required", nameof(request.SystemPrompt));

        if (request.Config is not SemanticKernelConfig skConfig)
            throw new ArgumentException("Config must be SemanticKernelConfig for SemanticKernelOrchestrator", nameof(request.Config));

        try
        {
            // Sanitize the agent name to comply with OpenAI requirements
            var agentId = $"RouterAgent_{SanitizeName(request.MessageThread.WorkflowId)}";

            // Create kernel with all plugins
            var kernel = await CreateKernelWithPlugins(
                skConfig, 
                request.CapabilityTypes.ToArray(), 
                request.MessageThread, 
                request.KernelModifiers);

            // Create the agent with full instructions
            var agent = CreateChatCompletionAgent(
                name: agentId,
                instructions: request.SystemPrompt,
                kernel: kernel,
                config: skConfig,
                enableFunctions: true
            );
            
            // Build chat history and create thread with proper history
            var chatHistory = await BuildChatHistoryAsync(
                request.MessageThread, 
                request.SystemPrompt, 
                skConfig.HistorySizeToFetch);
            
            // Apply chat history reduction if token limits are configured
            if (skConfig.TokenLimit > 0)
            {
                var chatService = kernel.GetRequiredService<IChatCompletionService>();
                var reducerLogger = Globals.LogFactory.CreateLogger<ChatHistoryReducer>();
                var reducer = new ChatHistoryReducer(reducerLogger, skConfig, chatService);
                
                // Apply reduction to prevent token limit issues
                chatHistory = await reducer.ReduceAsync(chatHistory);
            }
            
            var thread = new ChatHistoryAgentThread(chatHistory);
            
            // Create the current user message from the latest message
            var currentMessage = new ChatMessageContent(
                AuthorRole.User, 
                request.MessageThread.LatestMessage?.Content ?? string.Empty
            );

            // Apply incoming message interception
            var messageThread = await ApplyIncomingInterceptorAsync(request.MessageThread, request.Interceptor);
            
            // Invoke the agent with the message and thread
            var responses = new List<ChatMessageContent>();
            await foreach (var item in agent.InvokeAsync(currentMessage, thread))
            {
                responses.Add(item);
            }
            var response = string.Join(" ", responses.Select(r => r.Content));

            // Apply outgoing message interception
            response = await ApplyOutgoingInterceptorAsync(messageThread, response, request.Interceptor);

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
            _logger.LogError(e, "Error routing message for workflow {WorkflowType}", request.MessageThread.WorkflowType);
            throw;
        }
    }

    public async Task<string?> CompletionAsync(string prompt, string? systemInstruction, OrchestratorConfig config)
    {
        if (config is not SemanticKernelConfig skConfig)
            throw new ArgumentException("Config must be SemanticKernelConfig for SemanticKernelOrchestrator", nameof(config));
        
        try
        {
            var kernel = BuildKernel(skConfig);
            var agent = CreateChatCompletionAgent(
                name: "CompletionAgent",
                instructions: systemInstruction ?? "You are a helpful assistant. Perform the user's request accurately and concisely.",
                kernel: kernel,
                config: skConfig,
                enableFunctions: false
            );
            
            // Create a simple thread for single-turn chat completion
            var chatHistory = new ChatHistory(systemInstruction ?? "You are a helpful assistant. Perform the user's request accurately and concisely.");
            
            // Apply chat history reduction if token limits are configured
            if (skConfig.TokenLimit > 0)
            {
                var chatService = kernel.GetRequiredService<IChatCompletionService>();
                var reducerLogger = Globals.LogFactory.CreateLogger<ChatHistoryReducer>();
                var reducer = new ChatHistoryReducer(reducerLogger, skConfig, chatService);
                chatHistory = await reducer.ReduceAsync(chatHistory);
            }
            
            var thread = new ChatHistoryAgentThread(chatHistory);
            var userMessage = new ChatMessageContent(AuthorRole.User, prompt);
            
            var responses = new List<ChatMessageContent>();
            await foreach (var response in agent.InvokeAsync(userMessage, thread))
            {
                responses.Add(response);
            }
            return string.Join(" ", responses.Select(r => r.Content));
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Error in LLM completion in processing message: `{prompt}` and system instruction: `{systemInstruction}`");
            throw;
        }
    }

    private ChatCompletionAgent CreateChatCompletionAgent(
        string name,
        string instructions,
        Kernel kernel,
        SemanticKernelConfig config,
        bool enableFunctions = true)
    {
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = enableFunctions ? FunctionChoiceBehavior.Auto() : FunctionChoiceBehavior.None(),
            MaxTokens = config.MaxTokens,
            Temperature = config.Temperature
        };

        return new ChatCompletionAgent
        {
            Name = name,
            Instructions = instructions,
            Kernel = kernel,
            Arguments = new KernelArguments(executionSettings)
        };
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
        SemanticKernelConfig config,
        Type[] capabilitiesPluginTypes, 
        MessageThread messageThread, 
        List<IKernelModifier> kernelModifiers)
    {
        var kernel = BuildKernel(config);
        
        // Register non-static plugins with MessageThread context
        RegisterNonStaticPlugins(kernel, capabilitiesPluginTypes, messageThread);

        // Add static capability plugins
        RegisterStaticPlugins(kernel, capabilitiesPluginTypes);

        // Add system plugins
        _logger.LogDebug("Adding Date plugin");
        kernel.Plugins.AddFromFunctions("System_DatePlugin", DatePlugin.GetFunctions());

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

    private Kernel BuildKernel(SemanticKernelConfig config)
    {
        var builder = ConfigureKernelBuilder(config);

        // Configure logging
        builder.Services.AddLogging(configure => 
        {
            configure.AddConsole();
            configure.SetMinimumLevel(LogLevel.Information);
        });

        // To avoid infinite loops, we need to add the termination filter as a scoped service.
        builder.Services.AddScoped<IAutoFunctionInvocationFilter>(_ => new TerminationFilter(config.MaxConsecutiveCalls));

        var kernel = builder.Build() ?? throw new InvalidOperationException("Failed to build Semantic Kernel");

        return kernel;
    }

    private IKernelBuilder ConfigureKernelBuilder(SemanticKernelConfig config)
    {
        var configResolver = new LlmConfigurationResolver();
        var providerName = configResolver.GetProviderName(config);
        var builder = Kernel.CreateBuilder();
        
        var httpClient = _httpClient.Value;
        httpClient.Timeout = TimeSpan.FromSeconds(config.HTTPTimeoutSeconds);

        switch (providerName?.ToLower())
        {
            case "openai":
                var modelName = configResolver.GetModelName(config);
                builder.AddOpenAIChatCompletion(
                    modelId: modelName,
                    apiKey: configResolver.GetApiKey(config),
                    httpClient: httpClient);
                _logger.LogDebug("Configured OpenAI with model {ModelName}", modelName);
                break;

            case "azureopenai":
                var deploymentName = configResolver.GetDeploymentName(config);
                builder.AddAzureOpenAIChatCompletion(
                    deploymentName: deploymentName,
                    endpoint: configResolver.GetEndpoint(config),
                    apiKey: configResolver.GetApiKey(config),
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
            },
            var d when d.Equals(OUTGOING_MESSAGE, StringComparison.OrdinalIgnoreCase) => new()
            {
                Role = AuthorRole.Assistant,
                Content = message.Text
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

