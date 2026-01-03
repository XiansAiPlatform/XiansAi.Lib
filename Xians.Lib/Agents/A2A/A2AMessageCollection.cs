using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Workflows.Messaging.Models;
using Xians.Lib.Agents.Core;

namespace Xians.Lib.Agents.A2A;

/// <summary>
/// Specialized messaging collection for Agent-to-Agent communication.
/// Captures responses instead of sending them to users.
/// </summary>
public class A2AMessageCollection : MessageCollection
{
    private readonly A2AMessageContext _context;
    private readonly A2AResponseCapture _responseCapture;
    private readonly ILogger<A2AMessageCollection> _logger;
    private bool _responseSent = false;

    internal A2AMessageCollection(A2AMessageContext context, A2AResponseCapture responseCapture)
        : base(string.Empty, string.Empty, "a2a", string.Empty, new object(), context.TenantId)
    {
        _context = context;
        _responseCapture = responseCapture;
        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<A2AMessageCollection>();
    }

    /// <summary>
    /// Sends a reply back to the calling agent.
    /// Captures the response instead of sending to a user.
    /// </summary>
    /// <param name="response">The response text to send.</param>
    public override Task ReplyAsync(string response)
    {
        CaptureResponse(response, null);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a reply with both text and data back to the calling agent.
    /// Captures the response instead of sending to a user.
    /// </summary>
    /// <param name="content">The text content to send.</param>
    /// <param name="data">The data object to send.</param>
    public override Task ReplyWithDataAsync(string content, object? data)
    {
        CaptureResponse(content, data);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Chat history is not available for A2A messages.
    /// Returns an empty list since A2A messages are stateless one-off requests.
    /// </summary>
    public override Task<List<DbMessage>> GetHistoryAsync(int page = 1, int pageSize = 10)
    {
        _logger.LogDebug(
            "Chat history requested in A2A context - returning empty list. " +
            "A2A messages are stateless and don't have conversation history.");
        
        return Task.FromResult(new List<DbMessage>());
    }

    /// <summary>
    /// Hints are not available for A2A messages.
    /// Returns null since A2A messages are stateless one-off requests.
    /// </summary>
    public override Task<string?> GetLastHintAsync()
    {
        _logger.LogDebug(
            "Last hint requested in A2A context - returning null. " +
            "A2A messages are stateless and don't have hints.");
        
        return Task.FromResult<string?>(null);
    }

    /// <summary>
    /// Internal method to capture the A2A response.
    /// </summary>
    private void CaptureResponse(string text, object? data)
    {
        if (_responseSent)
        {
            _logger.LogWarning(
                "A2A response already sent from {SourceAgent}. Ignoring duplicate response.",
                _context.SourceAgentName);
            return;
        }

        _logger.LogDebug(
            "Capturing A2A response: From={TargetAgent}, To={SourceAgent}",
            XiansContext.AgentName,
            _context.SourceAgentName);

        _responseCapture.HasResponse = true;
        _responseCapture.Text = text;
        _responseCapture.Data = data;
        _responseSent = true;

        _logger.LogInformation(
            "A2A response captured from {Agent}",
            XiansContext.AgentName);
    }
}


