using Microsoft.SemanticKernel;

namespace XiansAi.Flow;

/// <summary>
/// Interface for type-erased bot storage.
/// </summary>
internal interface IBot
{
    Task UploadDefinitionAsync(RunnerOptions? options);
    Task RunAsync(RunnerOptions? options);
}

/// <summary>
/// Manages capabilities for a specific bot type.
/// </summary>
/// <typeparam name="TBot">The bot class type</typeparam>
public class Bot<TBot> : Flow<TBot>, IBot where TBot : FlowBase
{

    internal Bot(AgentTeam agentTeam, int numberOfWorkers) : base(agentTeam, numberOfWorkers)
    {
    }

    /// <summary>
    /// Adds capabilities to this bot.
    /// </summary>
    /// <param name="capabilityType">The capability type to add</param>
    /// <returns>This bot instance for method chaining</returns>
    public Bot<TBot> AddCapabilities(Type capabilityType)
    {
        _runner.AddAgentCapabilities(capabilityType);
        return this;
    }

    public Bot<TBot> SetChatInterceptor(IChatInterceptor interceptor)
    {
        _runner.ChatInterceptor = interceptor;
        return this;
    }

    public Bot<TBot> AddKernelModifier(IKernelModifier modifier)
    {
        _runner.KernelModifiers.Add(modifier);
        return this;
    }

    /// <summary>
    /// Adds capabilities to this bot.
    /// </summary>
    /// <typeparam name="TCapability">The capability type to add</typeparam>
    /// <returns>This bot instance for method chaining</returns>
    public Bot<TBot> AddCapabilities<TCapability>()
    {
        _runner.AddAgentCapabilities<TCapability>();
        return this;
    }
}
