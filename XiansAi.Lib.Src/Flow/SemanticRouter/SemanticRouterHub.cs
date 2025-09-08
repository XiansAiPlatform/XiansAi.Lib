using Temporal;
using Temporalio.Activities;
using Temporalio.Workflows;
using XiansAi.Messaging;

namespace XiansAi.Flow.Router;

/// <summary>
/// Public API for semantic routing operations within Temporal workflows.
/// Provides routing and chat completion capabilities.
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
        RouterOptions options)
    {
        if (Workflow.InWorkflow) {
            // Go through a Temporal activity to perform IO operations
            return await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.RouteAsync(messageThread, systemPrompt, options),
                new SystemActivityOptions());
        }
        else  
        {
            var type = WorkflowIdentifier.GetClassTypeFor(messageThread.WorkflowType);
            var runner = RunnerRegistry.GetRunner(type) ?? throw new InvalidOperationException($"Runner not found for workflow type: {messageThread.WorkflowType}");
            var capabilities = runner.Capabilities;
            var kernelModifiers = runner.KernelModifiers;
            var chatInterceptor = runner.ChatInterceptor;
            return await new SemanticRouterHubImpl().RouteAsync(messageThread, systemPrompt, options, capabilities, chatInterceptor, kernelModifiers);
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
    /// <param name="routerOptions">Optional router configuration</param>
    /// <param name="systemActivityOptions">Optional Temporal activity options</param>
    /// <returns>The chat completion response</returns>
    public static async Task<string?> CompletionAsync(
        string prompt, string? systemInstruction = null,
        RouterOptions? routerOptions = null)
    {
        if (Workflow.InWorkflow)
        {
            var response = await Workflow.ExecuteLocalActivityAsync(
                (SystemActivities a) => a.CompletionAsync(prompt, systemInstruction, routerOptions),
                new SystemLocalActivityOptions());

            return response;
        }
        else
        {
            using var impl = new SemanticRouterHubImpl();
            return await impl.CompletionAsync(prompt, systemInstruction, routerOptions);
        }
    }
}
