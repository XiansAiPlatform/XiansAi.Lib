using Microsoft.Extensions.Logging;
using XiansAi.DynamicOrchestrator;
using XiansAi.DynamicOrchestrator.Channels;


namespace XiansAi.Channel
{
    /// <summary>
    /// Base class for channel implementations that provides common functionality
    /// for managing orchestrator communication and message handling.
    /// </summary>
    public class ChannelBase
    {
        private readonly XiansAi.DynamicOrchestrator.Core.DynamicOrchestrator _orchestrator;
        private readonly IChannel _channel;
        private readonly ILogger<ChannelManager> _logger;
        private readonly ChannelManager _communicator;

        /// <summary>
        /// Initializes a new instance of the ChannelBase.
        /// </summary>
        /// <param name="orchestratorName">Name of the orchestrator.</param>
        /// <param name="orchestrationRules">Rules for the orchestrator to follow.</param>
        /// <param name="channel">The communication channel to use.</param>
        /// <param name="logger">Logger instance for tracking operations.</param>
        /// <param name="config">Optional configuration dictionary.</param>
        public ChannelBase(
            string orchestratorName,
            string orchestrationRules,
            IChannel channel,
            ILogger<ChannelManager> logger,
            Dictionary<string, object>? config = null)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Create the orchestrator
            _orchestrator = new XiansAi.DynamicOrchestrator.Core.DynamicOrchestrator(orchestratorName, orchestrationRules, config);

            // Create the communicator
            _communicator = new ChannelManager(_channel, _orchestrator, _logger);
        }

        /// <summary>
        /// Registers functions for the orchestrator to use.
        /// </summary>
        /// <param name="functionRegistryName">Name of the function registry.</param>
        /// <param name="registryType">Type containing the functions to register.</param>
        public void RegisterFunctions(string functionRegistryName, Type registryType)
        {
            _orchestrator.RegisterFunctions(functionRegistryName, registryType);
        }

        /// <summary>
        /// Executes the communication task between the channel and orchestrator.
        /// This method starts the main processing loop for handling messages and orchestrator interactions.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ExecuteTaskAsync()
        {
            await _communicator.StartCommunicationAsync();
        }

        /// <summary>
        /// Clears the conversation history.
        /// </summary>
        public void ClearHistory()
        {
            _orchestrator.ClearHistory();
        }

        /// <summary>
        /// Gets the current conversation history.
        /// </summary>
        /// <returns>Read-only list of chat messages.</returns>
        public IReadOnlyList<ChatMessage> GetConversationHistory()
        {
            return _orchestrator.ConversationHistory;
        }

        /// <summary>
        /// Sends a message through the channel.
        /// </summary>
        /// <param name="message">The message to send.</param>
        public async Task SendMessageAsync(string message)
        {
            await _channel.SendMessageAsync(message);
        }
    }
} 