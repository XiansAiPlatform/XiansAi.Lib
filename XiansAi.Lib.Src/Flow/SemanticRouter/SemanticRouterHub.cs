using Temporalio.Workflows;
using XiansAi.Messaging;
using Server;

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
        RouterOptions options, 
        SystemActivityOptions systemActivityOptions)
    {
        // Go through a Temporal activity to perform IO operations
        var response = await Workflow.ExecuteLocalActivityAsync(
            (SystemActivities a) => a.RouteAsync(messageThread, systemPrompt, options),
            new SystemLocalActivityOptions());

        return response;
    }

    /// <summary>
    /// Performs a simple chat completion without message history or function calling.
    /// </summary>
    /// <param name="prompt">The prompt to send to the LLM</param>
    /// <param name="routerOptions">Optional router configuration</param>
    /// <param name="systemActivityOptions">Optional Temporal activity options</param>
    /// <returns>The chat completion response</returns>
    public static async Task<string?> ChatCompletionAsync(
        string prompt, 
        RouterOptions? routerOptions = null, 
        SystemActivityOptions? systemActivityOptions = null)
    {
        if (Workflow.InWorkflow)
        {
            systemActivityOptions = systemActivityOptions ?? new SystemActivityOptions();
            var response = await Workflow.ExecuteLocalActivityAsync(
                (SystemActivities a) => a.ChatCompletionAsync(prompt, routerOptions),
                new SystemLocalActivityOptions());

            return response;
        }
        else
        {
            using var impl = new SemanticRouterHubImpl();
            return await impl.ChatCompletionAsync(prompt, routerOptions);
        }
    }
}
