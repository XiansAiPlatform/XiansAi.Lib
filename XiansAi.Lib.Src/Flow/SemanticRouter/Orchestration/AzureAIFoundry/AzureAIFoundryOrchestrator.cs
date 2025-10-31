using Microsoft.Extensions.Logging;
using XiansAi.Messaging;

// NOTE: This implementation requires the Azure.AI.Agents.Persistent NuGet package
// Add this to your .csproj: <PackageReference Include="Azure.AI.Agents.Persistent" Version="..." />

#if ENABLE_AZURE_AI_FOUNDRY
using Azure;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
#endif

namespace XiansAi.Flow.Router.Orchestration.AzureAIFoundry;

/// <summary>
/// Azure AI Foundry Persistent Agents implementation of the AI orchestrator.
/// 
/// NOTE: This orchestrator requires the Azure.AI.Agents.Persistent NuGet package.
/// To enable it, add the package and define ENABLE_AZURE_AI_FOUNDRY in your build configuration.
/// 
/// Example usage:
/// <code>
/// var config = new AzureAIFoundryConfig
/// {
///     ProjectEndpoint = "https://your-project.cognitiveservices.azure.com/",
///     ModelDeploymentName = "gpt-4",
///     AzureAISearchConnectionId = "your-search-connection-id",
///     SearchIndexName = "your-index"
/// };
/// 
/// using var orchestrator = new AzureAIFoundryOrchestrator();
/// var result = await orchestrator.RouteAsync(request);
/// </code>
/// </summary>
public class AzureAIFoundryOrchestrator : IAIOrchestrator
{
    private readonly ILogger _logger;

#if ENABLE_AZURE_AI_FOUNDRY
    private PersistentAgentsClient? _agentClient;
    private PersistentAgent? _agent;
    private readonly Dictionary<string, PersistentAgentThread> _threadCache = new();
#endif

    public AzureAIFoundryOrchestrator()
    {
        _logger = Globals.LogFactory.CreateLogger<AzureAIFoundryOrchestrator>();
    }

    public async Task<string?> RouteAsync(OrchestratorRequest request)
    {
#if ENABLE_AZURE_AI_FOUNDRY
        if (request.Config is not AzureAIFoundryConfig foundryConfig)
            throw new ArgumentException("Config must be AzureAIFoundryConfig for AzureAIFoundryOrchestrator", nameof(request.Config));

        try
        {
            // Initialize client and agent if not already done
            await InitializeClientAndAgentAsync(foundryConfig, request.SystemPrompt);

            // Apply incoming message interception
            var messageThread = request.Interceptor != null
                ? await request.Interceptor.InterceptIncomingMessageAsync(request.MessageThread)
                : request.MessageThread;

            // Get or create thread for this workflow
            var threadId = messageThread.WorkflowId ?? Guid.NewGuid().ToString();
            if (!_threadCache.TryGetValue(threadId, out var thread))
            {
                thread = _agentClient!.Threads.CreateThread();
                _threadCache[threadId] = thread;
                _logger.LogDebug("Created new Azure AI Foundry thread: {ThreadId}", thread.Id);
            }

            // Create message in the thread
            var userMessage = messageThread.LatestMessage?.Content ?? string.Empty;
            _agentClient!.Messages.CreateMessage(
                thread.Id,
                MessageRole.User,
                userMessage);

            // Run the agent
            var run = _agentClient.Runs.CreateRun(thread, _agent!);

            // Wait for completion
            while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                run = _agentClient.Runs.GetRun(thread.Id, run.Id);
            }

            // Check for errors
            if (run.Status != RunStatus.Completed)
            {
                throw new InvalidOperationException($"Azure AI Foundry run did not complete successfully. Status: {run.Status}, Error: {run.LastError?.Message}");
            }

            // Retrieve messages
            var messages = _agentClient.Messages.GetMessages(
                threadId: thread.Id,
                order: ListSortOrder.Descending); // Get latest first

            // Extract the assistant's response (most recent assistant message)
            string? response = null;
            foreach (var msg in messages)
            {
                if (msg.Role == MessageRole.Agent)
                {
                    foreach (var contentItem in msg.ContentItems)
                    {
                        if (contentItem is MessageTextContent textItem)
                        {
                            response = textItem.Text;
                            break;
                        }
                    }
                    if (response != null) break;
                }
            }

            if (response == null)
            {
                throw new InvalidOperationException("No response found from Azure AI Foundry agent");
            }

            _logger.LogDebug("Azure AI Foundry Response: {Response}", response);

            // Apply outgoing message interception
            var finalResponse = request.Interceptor != null
                ? await request.Interceptor.InterceptOutgoingMessageAsync(messageThread, response)
                : response;

            // Handle skip response flag
            if (messageThread.SkipResponse)
            {
                messageThread.SkipResponse = false;
                return null;
            }

            return finalResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking Azure AI Foundry agent for workflow {WorkflowType}", request.MessageThread.WorkflowType);
            throw;
        }
#else
        await Task.CompletedTask; // Suppress warning
        throw new NotSupportedException(
            "Azure AI Foundry orchestrator is not available. " +
            "Add the Azure.AI.Agents.Persistent NuGet package and define ENABLE_AZURE_AI_FOUNDRY to enable this orchestrator.");
#endif
    }

    public async Task<string?> CompletionAsync(string prompt, string? systemInstruction, OrchestratorConfig config)
    {
#if ENABLE_AZURE_AI_FOUNDRY
        if (config is not AzureAIFoundryConfig foundryConfig)
            throw new ArgumentException("Config must be AzureAIFoundryConfig for AzureAIFoundryOrchestrator", nameof(config));

        try
        {
            // Initialize client and agent
            await InitializeClientAndAgentAsync(foundryConfig, systemInstruction ?? "You are a helpful assistant.");

            // Create a temporary thread for this completion
            var thread = _agentClient!.Threads.CreateThread();

            try
            {
                // Create message
                _agentClient.Messages.CreateMessage(
                    thread.Id,
                    MessageRole.User,
                    prompt);

                // Run the agent
                var run = _agentClient.Runs.CreateRun(thread, _agent!);

                // Wait for completion
                while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                    run = _agentClient.Runs.GetRun(thread.Id, run.Id);
                }

                // Check for errors
                if (run.Status != RunStatus.Completed)
                {
                    throw new InvalidOperationException($"Azure AI Foundry run did not complete successfully. Status: {run.Status}, Error: {run.LastError?.Message}");
                }

                // Retrieve messages
                var messages = _agentClient.Messages.GetMessages(
                    threadId: thread.Id,
                    order: ListSortOrder.Descending);

                // Extract response
                foreach (var msg in messages)
                {
                    if (msg.Role == MessageRole.Agent)
                    {
                        foreach (var contentItem in msg.ContentItems)
                        {
                            if (contentItem is MessageTextContent textItem)
                            {
                                _logger.LogDebug("Azure AI Foundry Completion Response: {Response}", textItem.Text);
                                return textItem.Text;
                            }
                        }
                    }
                }

                throw new InvalidOperationException("No response found from Azure AI Foundry agent");
            }
            finally
            {
                // Clean up temporary thread
                _agentClient.Threads.DeleteThread(thread.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Azure AI Foundry completion for prompt: {Prompt}", prompt);
            throw;
        }
#else
        await Task.CompletedTask; // Suppress warning
        throw new NotSupportedException(
            "Azure AI Foundry orchestrator is not available. " +
            "Add the Azure.AI.Agents.Persistent NuGet package and define ENABLE_AZURE_AI_FOUNDRY to enable this orchestrator.");
#endif
    }

#if ENABLE_AZURE_AI_FOUNDRY
    private async Task InitializeClientAndAgentAsync(AzureAIFoundryConfig config, string instructions)
    {
        if (_agentClient != null && _agent != null)
            return; // Already initialized

        // Create the Agent Client using DefaultAzureCredential
        _agentClient = new PersistentAgentsClient(config.ProjectEndpoint, new DefaultAzureCredential());

        // Build tool resources if search is configured
        ToolResources? toolResources = null;
        List<ToolDefinition>? tools = null;

        if (!string.IsNullOrEmpty(config.AzureAISearchConnectionId) && 
            !string.IsNullOrEmpty(config.SearchIndexName))
        {
            var searchResource = new AzureAISearchToolResource(
                indexConnectionId: config.AzureAISearchConnectionId,
                indexName: config.SearchIndexName,
                topK: config.SearchTopK,
                filter: config.SearchFilter,
                queryType: config.SearchQueryType == "Semantic" ? AzureAISearchQueryType.Semantic : AzureAISearchQueryType.Simple
            );

            toolResources = new ToolResources { AzureAISearch = searchResource };
            tools = new List<ToolDefinition> { new AzureAISearchToolDefinition() };
        }

        // Create the agent
        _agent = _agentClient.Administration.CreateAgent(
            model: config.ModelDeploymentName,
            name: config.AgentName ?? "XiansAI-Agent",
            instructions: config.AgentInstructions ?? instructions,
            tools: tools,
            toolResources: toolResources
        );

        _logger.LogInformation("Initialized Azure AI Foundry agent: {AgentId}", _agent.Id);

        await Task.CompletedTask; // Suppress warning
    }
#endif

    public void Dispose()
    {
#if ENABLE_AZURE_AI_FOUNDRY
        // Clean up threads
        if (_agentClient != null)
        {
            foreach (var thread in _threadCache.Values)
            {
                try
                {
                    _agentClient.Threads.DeleteThread(thread.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error cleaning up thread {ThreadId}", thread.Id);
                }
            }
            _threadCache.Clear();
        }

        // Clean up agent
        if (_agentClient != null && _agent != null)
        {
            try
            {
                _agentClient.Administration.DeleteAgent(_agent.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up agent {AgentId}", _agent.Id);
            }
        }
#endif
    }
}

