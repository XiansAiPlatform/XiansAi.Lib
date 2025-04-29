using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Temporalio.Workflows;
using XiansAi.Messaging;
using XiansAi.Router.Plugins;

namespace XiansAi.Router;

public interface ISemanticRouter
{
    Task<string> RouteAsync(MessageThread messageThread, string systemPrompt, string[] capabilitiesPluginNames, RouterOptions options);
}

public class SemanticRouter : ISemanticRouter
{

    public async Task<string> RouteAsync(MessageThread messageThread, string systemPrompt, string[] capabilitiesPluginNames, RouterOptions options)
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
    static readonly string OUTGOING_MESSAGE = "outgoing";
    // Static dictionary to cache kernels by workflow type
    private static readonly Dictionary<string, Kernel> _kernelCache = new Dictionary<string, Kernel>();
    private static readonly object _kernelCacheLock = new object();
    private readonly ILogger _logger;

    public SemanticRouterImpl()
    {
        _logger = Globals.LogFactory.CreateLogger<SemanticRouterImpl>();
    }

    public async Task<string> RouteAsync(MessageThread messageThread, string systemPrompt, string[] capabilitiesPluginNames, RouterOptions options)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                throw new Exception("System prompt is required");
            }

            var kernel = Initialize(messageThread.WorkflowType, options, capabilitiesPluginNames, messageThread);

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

    private Kernel GetOrCreateCacheableKernel(string workflowType, RouterOptions options, string[] capabilitiesPluginNames)
    {

        if (string.IsNullOrEmpty(workflowType))
        {
            // If no workflow type, create a new non-cached kernel
            return InitializeCacheable(options, capabilitiesPluginNames);
        }

        lock (_kernelCacheLock)
        {
            if (_kernelCache.TryGetValue(workflowType, out var cachedKernel))
            {
                return cachedKernel;
            }

            var newKernel = InitializeCacheable(options, capabilitiesPluginNames);
            _kernelCache[workflowType] = newKernel;
            return newKernel;
        }
    }

    private Kernel Initialize(string workflowType, RouterOptions options, string[] capabilitiesPluginNames, MessageThread messageThread)
    {
        var kernel = GetOrCreateCacheableKernel(workflowType, options, capabilitiesPluginNames);
        // add instance capabilities plugins

        var routeContext = new RouteContext {
            Agent = options.Agent,
            QueueName = options.QueueName,
            AssignmentId = options.AssignmentId,
            WorkflowId = options.WorkflowId,
            WorkflowType = options.WorkflowType
        };

        foreach (var pluginName in capabilitiesPluginNames)
        {
            var type = GetPluginType(pluginName);

            if (!(type.IsAbstract && type.IsSealed))
            {
                _logger.LogInformation("Getting functions from instance type {PluginType}", type.Name);
                var instance = Activator.CreateInstance(type, new object[] { messageThread, routeContext }) ?? throw new Exception($"Failed to create instance of {pluginName}");
                var functions = PluginReader.GetFunctionsFromInstanceType(type, instance);
                kernel.Plugins.TryGetPlugin(GetPluginName(pluginName), out var plugin);
                if (plugin != null)
                {
                    kernel.Plugins.Remove(plugin);
                }
                kernel.Plugins.AddFromFunctions(GetPluginName(pluginName), functions);
            }
        }

        return kernel;
    }

    private Kernel InitializeCacheable(RouterOptions options, string[] capabilitiesPluginNames)
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
            var type = GetPluginType(pluginName);

            if (type.IsAbstract && type.IsSealed)
            {
                // static plugin
                _logger.LogInformation("Getting functions from static type {PluginType}", type.Name);
                var functions = PluginReader.GetFunctionsFromStaticType(type);
                kernel.Plugins.AddFromFunctions(GetPluginName(pluginName), functions);
            }
        }

        return kernel;
    }

    private Type GetPluginType(string pluginName)
    {
        // Try to get the type directly first
        var pluginType = Type.GetType(pluginName);

        // If not found, search through all loaded assemblies
        if (pluginType == null)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                pluginType = assembly.GetType(pluginName);
                if (pluginType != null)
                    break;
            }
        }

        if (pluginType == null)
        {
            throw new Exception($"Plugin type {pluginName} not found.");
        }

        return pluginType;
    }

    private string GetPluginName(string input)
    {
        return input.Split('.').Last();
    }

    private async Task<ChatHistory> ConstructHistory(IMessageThread messageThread, string systemPrompt, int historySizeToFetch)
    {

        var chatHistory = new ChatHistory(systemPrompt);
        chatHistory.AddSystemMessage(@"First evaluate if you can accomplish the user request 
        through your primary capabilities. If you can, respond with the answer. 
        If you cannot, check if you can Handover to another bot. If you can, handover to another workflow.
        If you cannot accomplish the user request through your primary capabilities, or handover to another bot,
        respond with a message indicating that you cannot accomplish the user request.");

        var historyFromServer = await messageThread.GetThreadHistory(1, historySizeToFetch);
        // Reverse the history to put the most recent messages last
        historyFromServer.Reverse();

        foreach (var message in historyFromServer)
        {
            if (message.Direction.Equals(INCOMING_MESSAGE, StringComparison.OrdinalIgnoreCase))
            {
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                chatHistory.Add(
                    new()
                    {
                        Role = AuthorRole.User,
                        Content = message.Content,
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
                        Content = message.Content,
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