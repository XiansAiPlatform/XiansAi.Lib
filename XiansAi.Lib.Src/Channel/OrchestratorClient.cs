using Microsoft.Extensions.Logging;
using XiansAi.DynamicOrchestrator;
using XiansAi.DynamicOrchestrator.Channels;
using IChannel = XiansAi.DynamicOrchestrator.Channels.IChannel;

namespace XiansAi.Channel
{
    /// <summary>
    /// Manages communication between channels and orchestrators, handling message flow,
    /// function registration, and conversation history.
    /// </summary>
    public class ChannelOrchestratorManager : ChannelBase
    {
        /// <summary>
        /// Initializes a new instance of the ChannelOrchestratorManager.
        /// </summary>
        /// <param name="orchestratorName">Name of the orchestrator.</param>
        /// <param name="orchestrationRules">Rules for the orchestrator to follow.</param>
        /// <param name="channel">The communication channel to use.</param>
        /// <param name="logger">Logger instance for tracking operations.</param>
        /// <param name="config">Optional configuration dictionary.</param>
        public ChannelOrchestratorManager(
            string orchestratorName,
            string orchestrationRules,
            IChannel channel,
            ILogger<ChannelManager> logger,
            Dictionary<string, object>? config = null)
            : base(orchestratorName, orchestrationRules, channel, logger, config)
        {
        }

        /// <summary>
        /// Registers functions for the orchestrator to use.
        /// </summary>
        /// <param name="functionRegistryName">Name of the function registry.</param>
        /// <param name="registryType">Type containing the functions to register.</param>
        public new void RegisterFunctions(string functionRegistryName, Type registryType)
        {
            base.RegisterFunctions(functionRegistryName, registryType);
        }

        /// <summary>
        /// Starts the communication process with a welcome message and available commands.
        /// </summary>
        /// <param name="welcomeMessage">Message to display when starting.</param>
        /// <param name="availableCommands">List of available commands to display.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task StartAsync(string welcomeMessage, string[] availableCommands)
        {
            // Display welcome message
            await SendMessageAsync(welcomeMessage);
            
            // Display available commands
            await SendMessageAsync("Available commands:");
            foreach (var command in availableCommands)
            {
                await SendMessageAsync(command);
            }

            // Start the communication process
            await ExecuteTaskAsync();
        }

        /// <summary>
        /// Clears the conversation history.
        /// </summary>
        public new void ClearHistory()
        {
            base.ClearHistory();
        }

        /// <summary>
        /// Gets the current conversation history.
        /// </summary>
        /// <returns>Read-only list of chat messages.</returns>
        public new IReadOnlyList<ChatMessage> GetConversationHistory()
        {
            return base.GetConversationHistory();
        }
    }
} 