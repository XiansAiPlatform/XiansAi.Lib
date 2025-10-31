using XiansAi.Messaging;

namespace XiansAi.Flow.Router.Orchestration;

/// <summary>
/// Defines the contract for AI orchestrator implementations.
/// Orchestrators manage AI agent interactions, routing, and completions across different platforms
/// (e.g., Semantic Kernel, AWS Bedrock, Azure AI Foundry).
/// </summary>
public interface IAIOrchestrator : IDisposable
{
    /// <summary>
    /// Routes a message through the AI orchestrator with the specified configuration.
    /// This method handles the full agent lifecycle including:
    /// - Plugin/capability registration
    /// - Chat history management
    /// - Message interception
    /// - Response generation
    /// </summary>
    /// <param name="request">The orchestration request containing message context and configuration</param>
    /// <returns>The orchestrated response or null if response is skipped</returns>
    Task<string?> RouteAsync(OrchestratorRequest request);

    /// <summary>
    /// Performs a simple completion without message history or function calling.
    /// Useful for one-off queries or stateless interactions.
    /// </summary>
    /// <param name="prompt">The prompt to send to the AI</param>
    /// <param name="systemInstruction">Optional system instruction to guide the AI</param>
    /// <param name="config">Orchestrator configuration</param>
    /// <returns>The completion response</returns>
    Task<string?> CompletionAsync(string prompt, string? systemInstruction, OrchestratorConfig config);
}


