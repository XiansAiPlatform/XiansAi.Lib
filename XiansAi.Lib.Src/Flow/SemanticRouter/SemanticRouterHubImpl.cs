using Microsoft.Extensions.Logging;
using XiansAi.Messaging;
using XiansAi.Flow.Router.Orchestration;
using XiansAi.Flow.Router.Orchestration.SemanticKernel;

namespace XiansAi.Flow.Router;

/// <summary>
/// Implementation wrapper for the semantic router functionality.
/// Delegates to the appropriate AI orchestrator based on configuration.
/// </summary>
internal class SemanticRouterHubImpl : IDisposable
{
    private readonly ILogger _logger;
    private IAIOrchestrator? _orchestrator;

    public SemanticRouterHubImpl()
    {
        _logger = Globals.LogFactory.CreateLogger<SemanticRouterHubImpl>();
    }

    public async Task<string?> CompletionAsync(string prompt, string? systemInstruction, RouterOptions? options = null)
    {
        options ??= new RouterOptions();
        
        try
        {
            // Convert RouterOptions to SemanticKernelConfig for backward compatibility
            var config = OrchestratorFactory.ConvertFromRouterOptions(options);
            
            // Create orchestrator
            _orchestrator = OrchestratorFactory.Create(config);
            
            return await _orchestrator.CompletionAsync(prompt, systemInstruction, config);
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
            // Convert RouterOptions to SemanticKernelConfig for backward compatibility
            var config = OrchestratorFactory.ConvertFromRouterOptions(options);
            
            // Create orchestrator
            _orchestrator = OrchestratorFactory.Create(config);
            
            // Build request
            var request = new OrchestratorRequest
            {
                MessageThread = messageThread,
                SystemPrompt = systemPrompt,
                Config = config,
                CapabilityTypes = capabilitiesPluginTypes,
                Interceptor = interceptor,
                KernelModifiers = kernelModifiers
            };
            
            return await _orchestrator.RouteAsync(request);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error routing message for workflow {WorkflowType}", messageThread.WorkflowType);
            throw;
        }
    }

    public void Dispose()
    {
        _orchestrator?.Dispose();
    }
}

