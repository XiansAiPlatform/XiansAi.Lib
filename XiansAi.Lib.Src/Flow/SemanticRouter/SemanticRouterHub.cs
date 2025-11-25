using Temporal;
using Temporalio.Activities;
using Temporalio.Workflows;
using XiansAi.Messaging;
using XiansAi.Flow.Router.Orchestration;

namespace XiansAi.Flow.Router;

/// <summary>
/// Public API for semantic routing operations within Temporal workflows.
/// Provides routing and chat completion capabilities with support for multiple AI orchestrators.
/// </summary>
public static class SemanticRouterHub
{
    /// <summary>
    /// Routes a message through the semantic router with the specified system prompt and options.
    /// </summary>
    /// <param name="messageThread">The message thread context</param>
    /// <param name="systemPrompt">The system prompt to guide the AI</param>
    /// <param name="options">Router configuration options</param>
    /// <param name="systemActivityOptions">Temporal activity options</param>
    /// <returns>The routed response or null if response is skipped</returns>
    public static async Task<string?> RouteAsync(
        MessageThread messageThread, 
        string systemPrompt, 
        RouterOptions options,
        OrchestratorConfig orchestratorConfig)

    {
        if (Workflow.InWorkflow) {
            // Go through a Temporal activity to perform IO operations
            return await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.RouteAsync(messageThread, systemPrompt, options, orchestratorConfig),
                new SystemActivityOptions());
        }
        else  
        {
            var type = WorkflowIdentifier.GetClassTypeFor(messageThread.WorkflowType);
            var runner = RunnerRegistry.GetRunner(type) ?? throw new InvalidOperationException($"Runner not found for workflow type: {messageThread.WorkflowType}");
            var capabilities = runner.Capabilities;
            var kernelModifiers = runner.KernelModifiers;
            var chatInterceptor = runner.ChatInterceptor;
            return await new SemanticRouterHubImpl().RouteAsync(messageThread, systemPrompt, options, capabilities, chatInterceptor, kernelModifiers, orchestratorConfig);
        }
    }

    [Obsolete("Use CompletionAsync instead")]
    public static async Task<string?> ChatCompletionAsync(
        string prompt, string? systemInstruction = "", RouterOptions? routerOptions = null)
    {
        return await CompletionAsync(prompt, systemInstruction, routerOptions);
    }

    /// <summary>
    /// Performs a simple chat completion without message history or function calling.
    /// </summary>
    /// <param name="prompt">The prompt to send to the LLM</param>
    /// <param name="systemInstruction">Optional system instruction to guide the AI</param>
    /// <param name="routerOptions">Optional router configuration</param>
    /// <returns>The chat completion response</returns>
    public static async Task<string?> CompletionAsync(
        string prompt, string? systemInstruction = null,
        RouterOptions? routerOptions = null)
    {
        if (Workflow.InWorkflow)
        {
            var response = await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.CompletionAsync(prompt, systemInstruction, routerOptions),
                new SystemActivityOptions());

            return response;
        }
        else
        {
            using var impl = new SemanticRouterHubImpl();
            return await impl.CompletionAsync(prompt, systemInstruction, routerOptions);
        }
    }

    /// <summary>
    /// Routes a message through the AI orchestrator with the specified configuration.
    /// This is the modern API that supports multiple orchestrator types.
    /// </summary>
    /// <param name="request">The orchestration request containing message context and configuration</param>
    /// <returns>The orchestrated response or null if response is skipped</returns>
    public static async Task<string?> RouteWithOrchestratorAsync(OrchestratorRequest request)
    {
        if (Workflow.InWorkflow)
        {
            return await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.RouteWithOrchestratorAsync(request),
                new SystemActivityOptions());
        }
        else
        {
            using var orchestrator = OrchestratorFactory.Create(request.Config);
            return await orchestrator.RouteAsync(request);
        }
    }

    /// <summary>
    /// Performs a completion using the AI orchestrator directly.
    /// This is the modern API that supports multiple orchestrator types.
    /// </summary>
    /// <param name="prompt">The prompt to send to the AI</param>
    /// <param name="systemInstruction">Optional system instruction to guide the AI</param>
    /// <param name="config">Orchestrator configuration</param>
    /// <returns>The completion response</returns>
    public static async Task<string?> CompletionWithOrchestratorAsync(
        string prompt, 
        string? systemInstruction, 
        OrchestratorConfig config)
    {
        if (Workflow.InWorkflow)
        {
            return await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.CompletionWithOrchestratorAsync(prompt, systemInstruction, config),
                new SystemActivityOptions());
        }
        else
        {
            using var orchestrator = OrchestratorFactory.Create(config);
            return await orchestrator.CompletionAsync(prompt, systemInstruction, config);
        }
    }
}
