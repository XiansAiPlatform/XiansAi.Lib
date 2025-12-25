using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Xians.Lib.Workflows.Models;

namespace Xians.Lib.Agents;

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
    public async Task<List<DbMessage>> GetHistoryAsync(
        string workflowType,
        string participantId,
        string scope,
        string tenantId,
        int page,
        int pageSize)
    {
        // Build query string with proper URL encoding
        var endpoint = $"api/agent/conversation/history?" +
                      $"workflowType={Uri.EscapeDataString(workflowType ?? string.Empty)}" +
                      $"&participantId={Uri.EscapeDataString(participantId ?? string.Empty)}" +
                      $"&page={page}" +
                      $"&pageSize={pageSize}" +
                      $"&scope={Uri.EscapeDataString(scope ?? string.Empty)}";

        _logger.LogTrace("Fetching message history from {Endpoint}", endpoint);

        // Create HTTP request with tenant header
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);

        var response = await _httpClient.SendAsync(httpRequest);

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

        var messages = await response.Content.ReadFromJsonAsync<List<DbMessage>>();

        _logger.LogInformation(
            "Message history fetched successfully: {Count} messages",
            messages?.Count ?? 0);

        return messages ?? new List<DbMessage>();
    }

    /// <summary>
    /// Sends a chat or data message to a participant via the Xians platform API.
    /// </summary>
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
        string messageType)
    {
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

        var response = await _httpClient.SendAsync(httpRequest);

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

