using Microsoft.SemanticKernel;

namespace XiansAi.Flow;

/// <summary>
/// Interface for type-erased bot storage.
/// </summary>
internal interface IBot
{
    Task RunAsync(RunnerOptions? options);
}

/// <summary>
/// Manages capabilities for a specific bot type.
/// </summary>
/// <typeparam name="TBot">The bot class type</typeparam>
public class Bot<TBot> : Flow<TBot>, IBot where TBot : FlowBase
{

    internal Bot(Agent agent, int numberOfWorkers) : base(agent, numberOfWorkers)
    {
    }

    public void EnableDatePlugin(KernelPluginOptions? options = null)
    {
        _runner.Plugins.DatePlugin = options ?? new KernelPluginOptions();
    }

    /// <summary>
    /// Adds capabilities to this bot.
    /// </summary>
    /// <param name="capabilityType">The capability type to add</param>
    /// <returns>This bot instance for method chaining</returns>
    public Bot<TBot> AddCapabilities(Type capabilityType)
    {
        _runner.AddBotCapabilities(capabilityType);
        return this;
    }

    public Bot<TBot> SetChatInterceptor(IChatInterceptor interceptor)
    {
        _runner.ChatInterceptor = interceptor;
        return this;
    }

    public Bot<TBot> SetKernelModifier(IKernelModifier modifier)
    {
        _runner.KernelModifier = modifier;
        return this;
    }

    /// <summary>
    /// Adds capabilities to this bot.
    /// </summary>
    /// <typeparam name="TCapability">The capability type to add</typeparam>
    /// <returns>This bot instance for method chaining</returns>
    public Bot<TBot> AddCapabilities<TCapability>()
    {
        _runner.AddBotCapabilities<TCapability>();
        return this;
    }
}
