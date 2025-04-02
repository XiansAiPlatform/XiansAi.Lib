using Microsoft.Extensions.Logging;
using XiansAi.DynamicOrchestrator.Core;
using XiansAi.DynamicOrchestrator.Channels;
using XiansAi.DynamicOrchestrator;

namespace XiansAi.Channel
{
    /// <summary>
    /// Manages communication between the agent and users through a channel.
    /// This class handles the flow of messages between the user and the AI agent,
    /// including command processing, message handling, and error management.
    /// </summary>
    public class ChannelManager
    {
        // Private fields for dependency injection and service management
        private readonly IChannel _channel;        // Communication channel interface
        private readonly ILogger<ChannelManager> _logger;  // Logging service
        private readonly XiansAi.DynamicOrchestrator.Core.DynamicOrchestrator _agent;          // AI agent instance

        /// <summary>
        /// Initializes a new instance of the ChannelManager.
        /// Sets up the communication channel, AI agent, and logging service.
        /// </summary>
        /// <param name="channel">The communication channel to use for user interaction.</param>
        /// <param name="agent">The AI agent that will process user messages.</param>
        /// <param name="logger">Optional logger for tracking communication and errors.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        public ChannelManager(IChannel channel, XiansAi.DynamicOrchestrator.Core.DynamicOrchestrator agent, ILogger<ChannelManager>? logger = null)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _agent = agent ?? throw new ArgumentNullException(nameof(agent));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes a single interaction cycle between the user and the agent.
        /// This method handles one complete turn of the conversation, including:
        /// 1. Receiving user input
        /// 2. Processing commands
        /// 3. Handling regular messages
        /// 4. Managing errors
        /// </summary>
        /// <returns>
        /// A task containing a boolean indicating the communication state:
        /// - true: Continue the conversation
        /// - false: Stop the conversation (e.g., on exit command)
        /// </returns>
        public async Task<bool> RunAgentTaskAsync()
        {
            try
            {
                // Step 1: Get user input from the channel
                string userInput = await _channel.ReceiveResponseAsync();
                if (string.IsNullOrWhiteSpace(userInput))
                {
                    return true; // Skip empty inputs but continue communication
                }

                // Step 2: Process any special commands
                var command = userInput.Trim().ToLower();
                if (await HandleCommandAsync(command))
                {
                    // Return false for exit command, true for other commands
                    return command != "exit";
                }

                // Step 3: Process regular user message
                await ProcessMessageAsync(userInput);
                return true; // Continue communication after processing message
            }
            catch (Exception ex)
            {
                // Step 4: Handle any errors that occur during processing
                _logger?.LogError(ex, "Error in agent task");
                await _channel.SendMessageAsync("An error occurred while processing your request. Please try again.");
                return true; // Continue despite errors to maintain conversation
            }
        }

        /// <summary>
        /// Initiates and maintains the continuous communication loop with the agent.
        /// This method runs until explicitly stopped by a command or error.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="Exception">Thrown when an unhandled error occurs in the communication process.</exception>
        public async Task StartCommunicationAsync()
        {
            try
            {
                // Initialize the communication loop
                bool shouldContinue = true;
                while (shouldContinue)
                {
                    // Process one interaction and update the continuation flag
                    shouldContinue = await RunAgentTaskAsync();
                }
            }
            catch (Exception ex)
            {
                // Log and rethrow any unhandled exceptions
                _logger?.LogError(ex, "Error in communication process");
                throw;
            }
        }

        /// <summary>
        /// Processes special commands issued by the user.
        /// Handles commands like 'exit', 'clear', and 'history'.
        /// </summary>
        /// <param name="command">The command to process (in lowercase).</param>
        /// <returns>
        /// A task containing a boolean indicating:
        /// - true: The input was a recognized command
        /// - false: The input was not a command
        /// </returns>
        private async Task<bool> HandleCommandAsync(string command)
        {
            switch (command)
            {
                case "exit":
                    return true;  // Command recognized, will stop communication
                case "clear":
                    _agent.ClearHistory();
                    await _channel.SendMessageAsync("Chat history cleared.");
                    return true;  // Command recognized, continue communication
                case "history":
                    // Display the conversation history
                    var history = _agent.ConversationHistory;
                    if (history.Count == 0)
                    {
                        await _channel.SendMessageAsync("No chat history available.");
                    }
                    else
                    {
                        var historyText = string.Join("\n", history.Select(m => $"{m.Role}: {m.Content}"));
                        await _channel.SendMessageAsync($"\nChat History:\n{historyText}\n");
                    }
                    return true;  // Command recognized, continue communication
                default:
                    return false; // Not a recognized command
            }
        }

        /// <summary>
        /// Processes a regular user message by sending it to the agent and handling the response.
        /// This method handles the core communication with the AI agent.
        /// </summary>
        /// <param name="message">The user's message to process.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ProcessMessageAsync(string message)
        {
            try
            {
                // Step 1: Get the agent's response
                var response = await _agent.RunAgentAsync(message, ResponseType.String);
                
                // Step 2: Send the response back to the user
                await _channel.SendMessageAsync($"Agent: {response}");
            }
            catch (Exception ex)
            {
                // Handle any errors during message processing
                _logger?.LogError(ex, "Error processing message: {Message}", message);
                await _channel.SendMessageAsync("Sorry, I encountered an error processing your message. Please try again.");
            }
        }
    }
} 