using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Xians.Lib.Workflows.Models;
using Xians.Lib.Agents.Messaging.Models;
using Xians.Lib.Workflows.Messaging;
using Xians.Lib.Workflows.Messaging.Models;
using Xians.Lib.Common.Infrastructure;

namespace Xians.Lib.Agents.Messaging;

/// <summary>
/// Core service for message operations.
/// Shared by both MessageActivities and ActivityUserMessageContext to avoid code duplication.
/// </summary>
internal class MessageService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public MessageService(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves paginated chat history for a conversation from the server.
    /// </summary>
    /// <param name="workflowType">The workflow type identifier.</param>
    /// <param name="participantId">The participant ID.</param>
    /// <param name="scope">The message scope.</param>
    /// <param name="tenantId">The tenant ID for isolation.</param>
    /// <param name="page">The page number (0-indexed).</param>
    /// <param name="pageSize">The number of messages per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of messages for the requested page.</returns>
    public async Task<List<DbMessage>> GetHistoryAsync(
        string workflowType,
        string participantId,
        string scope,
        string tenantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateRequired(workflowType, nameof(workflowType));
        ValidationHelper.ValidateRequired(participantId, nameof(participantId));
        ValidationHelper.ValidateRequired(tenantId, nameof(tenantId));
        ValidationHelper.ValidatePositive(page + 1, nameof(page)); // +1 because page is 0-indexed
        ValidationHelper.ValidatePositive(pageSize, nameof(pageSize));
        
        // For page 1, fetch +1 message since we'll drop the latest (current) message
        var fetchSize = page == 1 ? pageSize + 1 : pageSize;
        
        // Build query string with proper URL encoding
        var endpoint = $"api/agent/conversation/history?" +
                      $"workflowType={Uri.EscapeDataString(workflowType ?? string.Empty)}" +
                      $"&participantId={Uri.EscapeDataString(participantId ?? string.Empty)}" +
                      $"&page={page}" +
                      $"&pageSize={fetchSize}" +
                      $"&scope={Uri.EscapeDataString(scope ?? string.Empty)}";

        _logger.LogTrace("Fetching message history from {Endpoint}", endpoint);

        // Create HTTP request with tenant header
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Failed to fetch message history: StatusCode={StatusCode}, Error={Error}",
                response.StatusCode,
                error);
            throw new HttpRequestException(
                $"Failed to fetch message history. Status: {response.StatusCode}");
        }

        var messages = await response.Content.ReadFromJsonAsync<List<DbMessage>>(cancellationToken);

        if (messages == null)
        {
            _logger.LogWarning(
                "Message history deserialization returned null for WorkflowType={WorkflowType}, ParticipantId={ParticipantId}",
                workflowType,
                participantId);
            return new List<DbMessage>();
        }

        // For page 1, drop the latest message (the current message being processed)
        if (page == 1 && messages.Count > 0)
        {
            // Remove the most recent message by timestamp
            var latestMessage = messages.OrderByDescending(m => m.CreatedAt).First();
            messages = messages.Where(m => m.RequestId != latestMessage.RequestId).ToList();
            
            _logger.LogInformation(
                "Message history fetched (page 1): {Count} messages (dropped latest message)",
                messages.Count);
        }
        else
        {
            _logger.LogInformation(
                "Message history fetched (page {Page}): {Count} messages",
                page,
                messages.Count);
        }

        // print all messages and requestId, and text
        foreach (var message in messages)
        {
            _logger.LogInformation("Message: {Message}, RequestId: {RequestId}, Text: {Text}", message.Text, message.RequestId, message.Text);
        }

        return messages;
    }

    /// <summary>
    /// Sends a chat or data message to a participant via the Xians platform API.
    /// </summary>
    /// <param name="participantId">The participant ID.</param>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="workflowType">The workflow type.</param>
    /// <param name="requestId">The request ID.</param>
    /// <param name="scope">The message scope.</param>
    /// <param name="text">The message text.</param>
    /// <param name="data">Optional data payload.</param>
    /// <param name="tenantId">The tenant ID for isolation.</param>
    /// <param name="authorization">Optional authorization header.</param>
    /// <param name="threadId">Optional thread ID.</param>
    /// <param name="hint">Hint for message routing.</param>
    /// <param name="origin">Optional origin identifier.</param>
    /// <param name="messageType">The message type ("chat" or "data").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendAsync(
        string participantId,
        string workflowId,
        string workflowType,
        string requestId,
        string scope,
        string text,
        object? data,
        string tenantId,
        string? authorization,
        string? threadId,
        string hint,
        string? origin,
        string messageType,
        CancellationToken cancellationToken = default)
    {
        // Validate required parameters
        ValidationHelper.ValidateRequired(participantId, nameof(participantId));
        ValidationHelper.ValidateRequired(workflowId, nameof(workflowId));
        ValidationHelper.ValidateRequired(workflowType, nameof(workflowType));
        ValidationHelper.ValidateRequired(requestId, nameof(requestId));
        ValidationHelper.ValidateRequired(tenantId, nameof(tenantId));
        ValidationHelper.ValidateRequired(messageType, nameof(messageType));
        
        // Validate message type
        var allowedTypes = new[] { "chat", "data" };
        var type = messageType.ToLower();
        if (!allowedTypes.Contains(type))
        {
            var error = $"Invalid message type: {messageType}. Allowed types: {string.Join(", ", allowedTypes)}";
            _logger.LogError(error);
            throw new ArgumentException(error, nameof(messageType));
        }

        // Build payload
        var payload = new
        {
            participantId,
            workflowId,
            workflowType,
            requestId,
            scope,
            data,
            authorization,
            text,
            threadId,
            hint,
            origin
        };

        // Use endpoint: api/agent/conversation/outbound/{type}
        var endpoint = $"api/agent/conversation/outbound/{type}";

        _logger.LogTrace(
            "Posting to {Endpoint}: TextLength={TextLength}",
            endpoint,
            text?.Length ?? 0);

        // Create HTTP request with tenant header
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Content = JsonContent.Create(payload);
        httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Message send failed: StatusCode={StatusCode}, Error={Error}",
                response.StatusCode,
                error);
            throw new HttpRequestException(
                $"Failed to send message. Status: {response.StatusCode}");
        }

        _logger.LogInformation("Message sent successfully: RequestId={RequestId}", requestId);
    }
}

