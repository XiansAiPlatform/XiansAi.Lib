using XiansAi.Messaging;

namespace XiansAi.Flow.Router.Orchestration;

/// <summary>
/// Represents a request to the AI orchestrator containing all necessary context
/// for routing a message through the agent system.
/// </summary>
public class OrchestratorRequest
{
    /// <summary>
    /// The message thread containing conversation history and context
    /// </summary>
    public required MessageThread MessageThread { get; init; }

    /// <summary>
    /// The system prompt that guides the AI's behavior and personality
    /// </summary>
    public required string SystemPrompt { get; init; }

    /// <summary>
    /// Configuration for the orchestrator
    /// </summary>
    public required OrchestratorConfig Config { get; init; }

    /// <summary>
    /// Plugin/capability types to register with the agent.
    /// These provide additional functions the AI can invoke.
    /// </summary>
    public List<Type> CapabilityTypes { get; init; } = new();

    /// <summary>
    /// Optional interceptor for modifying incoming/outgoing messages
    /// </summary>
    public IChatInterceptor? Interceptor { get; init; }

    /// <summary>
    /// Optional kernel modifiers for advanced customization (Semantic Kernel specific)
    /// </summary>
    public List<IKernelModifier> KernelModifiers { get; init; } = new();
}


