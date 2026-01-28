using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Xians.Lib.Common;
using Xians.Lib.Common.Exceptions;
using Xians.Lib.Temporal.Workflows.Messaging.Models;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Common.Models;

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
    /// <param name="request">The get message history request containing workflow type, participant ID, scope, tenant ID, page, and page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of messages for the requested page.</returns>
    public async Task<List<DbMessage>> GetHistoryAsync(
        GetMessageHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateNotNull(request, nameof(request));
        ValidationHelper.ValidateRequired(request.WorkflowType, nameof(request.WorkflowType));
        ValidationHelper.ValidateRequired(request.ParticipantId, nameof(request.ParticipantId));
        ValidationHelper.ValidateRequired(request.TenantId, nameof(request.TenantId));
        ValidationHelper.ValidatePositive(request.Page + 1, nameof(request.Page)); // +1 because page is 0-indexed
        ValidationHelper.ValidatePositive(request.PageSize, nameof(request.PageSize));
        
        // For page 1, fetch +1 message since we'll drop the latest (current) message
        var fetchSize = request.Page == 1 ? request.PageSize + 1 : request.PageSize;
        
        // Build query string with proper URL encoding
        var endpoint = $"{WorkflowConstants.ApiEndpoints.ConversationHistory}?" +
                      $"workflowId={Uri.EscapeDataString(request.WorkflowId ?? string.Empty)}" +
                      $"&workflowType={Uri.EscapeDataString(request.WorkflowType ?? string.Empty)}" +
                      $"&participantId={Uri.EscapeDataString(request.ParticipantId ?? string.Empty)}" +
                      $"&page={request.Page}" +
                      $"&pageSize={fetchSize}" +
                      $"&scope={Uri.EscapeDataString(request.Scope ?? string.Empty)}";

        _logger.LogInformation("Fetching message history from {Endpoint}", endpoint);

        // Create HTTP request with tenant header
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, request.TenantId);

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
                request.WorkflowType,
                request.ParticipantId);
            return new List<DbMessage>();
        }

        // For page 1, drop the latest message (the current message being processed)
        if (request.Page == 1 && messages.Count > 0)
        {
            // Remove the most recent message by timestamp
            var latestMessage = messages.OrderByDescending(m => m.CreatedAt).First();
            messages = messages.Where(m => m.RequestId != latestMessage.RequestId).ToList();
            
            _logger.LogInformation(
                "Message history fetched - (page 1): {Count} messages (dropped latest message)",
                messages.Count);
        }
        else
        {
            _logger.LogInformation(
                "Message history fetched - (page {Page}): {Count} messages",
                request.Page,
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
    /// <param name="request">The send message request containing all message parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendAsync(
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate required parameters
        ValidationHelper.ValidateNotNull(request, nameof(request));
        ValidationHelper.ValidateRequired(request.ParticipantId, nameof(request.ParticipantId));
        ValidationHelper.ValidateRequired(request.WorkflowId, nameof(request.WorkflowId));
        ValidationHelper.ValidateRequired(request.WorkflowType, nameof(request.WorkflowType));
        ValidationHelper.ValidateRequired(request.RequestId, nameof(request.RequestId));
        ValidationHelper.ValidateRequired(request.TenantId, nameof(request.TenantId));
        ValidationHelper.ValidateRequired(request.Type, nameof(request.Type));
        
        // Validate message type using enum
        if (!MessageTypeExtensions.IsValidMessageType(request.Type))
        {
            var error = $"Invalid message type: {request.Type}. Allowed types: {string.Join(", ", MessageTypeExtensions.GetAllowedTypes())}";
            _logger.LogError(error);
            throw new ArgumentException(error, nameof(request.Type));
        }
        
        var type = request.Type.ToLower();

        // Retry logic with exponential backoff for rate limiting
        const int maxRetries = 3;
        var attempt = 0;
        
        while (true)
        {
            attempt++;
            
            try
            {
                await SendMessageInternalAsync(
                    request.ParticipantId, request.WorkflowId, request.WorkflowType, request.RequestId, request.Scope ?? string.Empty, 
                    request.Text ?? string.Empty, request.Data, request.TenantId, request.Authorization, request.ThreadId, request.Hint ?? string.Empty, request.Origin, 
                    type, cancellationToken);
                
                _logger.LogInformation("Message sent successfully: RequestId={RequestId}, WorkflowId={WorkflowId}", request.RequestId,request.WorkflowId);
                return;
            }
            catch (RateLimitException ex) when (attempt < maxRetries)
            {
                var delaySeconds = Math.Min(ex.RetryAfterSeconds, 120); // Cap at 2 minutes
                
                _logger.LogWarning(
                    "Rate limit hit (attempt {Attempt}/{MaxRetries}). Waiting {DelaySeconds}s before retry. RequestId={RequestId}",
                    attempt,
                    maxRetries,
                    delaySeconds,
                    request.RequestId);
                
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
            catch (RateLimitException) when (attempt >= maxRetries)
            {
                _logger.LogError(
                    "Rate limit exceeded after {Attempts} attempts. RequestId={RequestId}",
                    maxRetries,
                    request.RequestId);
                throw; // Re-throw after exhausting retries
            }
        }
    }

    /// <summary>
    /// Internal method that performs the actual HTTP request to send a message.
    /// </summary>
    private async Task SendMessageInternalAsync(
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
        string type,
        CancellationToken cancellationToken)
    {
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
        var endpoint = $"{WorkflowConstants.ApiEndpoints.ConversationOutbound}/{type}";

        _logger.LogTrace(
            "Posting to {Endpoint}: TextLength={TextLength}",
            endpoint,
            text?.Length ?? 0);

        // Create HTTP request with tenant header
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Content = JsonContent.Create(payload);
        httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, tenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Message send failed: StatusCode={StatusCode}, Error={Error}",
                response.StatusCode,
                error);

            // Handle rate limiting specially
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var retryAfterSeconds = ExtractRetryAfterSeconds(response, error);
                throw new RateLimitException(
                    $"Rate limit exceeded. Retry after {retryAfterSeconds} seconds.",
                    retryAfterSeconds,
                    (int)response.StatusCode);
            }

            throw new HttpRequestException(
                $"Failed to send message. Status: {response.StatusCode}");
        }
    }

    /// <summary>
    /// Retrieves the last hint for a conversation from the server.
    /// </summary>
    /// <param name="request">The get last hint request containing workflow type, participant ID, scope, and tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The last hint string, or null if not found.</returns>
    public async Task<string?> GetLastHintAsync(
        GetLastHintRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateNotNull(request, nameof(request));
        ValidationHelper.ValidateRequired(request.WorkflowType, nameof(request.WorkflowType));
        ValidationHelper.ValidateRequired(request.ParticipantId, nameof(request.ParticipantId));
        ValidationHelper.ValidateRequired(request.TenantId, nameof(request.TenantId));
        
        // Build query string with proper URL encoding
        var endpoint = $"{WorkflowConstants.ApiEndpoints.ConversationLastHint}?" +
                      $"workflowType={Uri.EscapeDataString(request.WorkflowType ?? string.Empty)}" +
                      $"&participantId={Uri.EscapeDataString(request.ParticipantId ?? string.Empty)}" +
                      $"&scope={Uri.EscapeDataString(request.Scope ?? string.Empty)}";

        _logger.LogTrace("Fetching last hint from {Endpoint}", endpoint);

        // Create HTTP request with tenant header
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, request.TenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Failed to fetch last hint: StatusCode={StatusCode}, Error={Error}",
                response.StatusCode,
                error);
            throw new HttpRequestException(
                $"Failed to fetch last hint. Status: {response.StatusCode}");
        }

        // Handle empty response (no hint available)
        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength == 0 || contentLength == null)
        {
            var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(rawContent))
            {
                _logger.LogInformation(
                    "No hint available: WorkflowType={WorkflowType}, ParticipantId={ParticipantId}",
                    request.WorkflowType,
                    request.ParticipantId);
                return null;
            }
        }

        string? hint = null;
        try
        {
            hint = await response.Content.ReadFromJsonAsync<string?>(cancellationToken);
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, 
                "Failed to parse hint JSON, treating as no hint available: WorkflowType={WorkflowType}, ParticipantId={ParticipantId}",
                request.WorkflowType,
                request.ParticipantId);
            return null;
        }

        _logger.LogInformation(
            "Last hint fetched: WorkflowType={WorkflowType}, ParticipantId={ParticipantId}, Found={Found}",
            request.WorkflowType,
            request.ParticipantId,
            hint != null);

        return hint;
    }

    /// <summary>
    /// Sends a handoff request to transfer a conversation to another agent/workflow.
    /// </summary>
    /// <param name="request">The send handoff request containing target workflow information and message details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response from the handoff operation.</returns>
    public async Task<string?> SendHandoffAsync(
        SendHandoffRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate required parameters
        ValidationHelper.ValidateNotNull(request, nameof(request));
        ValidationHelper.ValidateRequired(request.ParticipantId, nameof(request.ParticipantId));
        ValidationHelper.ValidateRequired(request.SourceWorkflowId, nameof(request.SourceWorkflowId));
        ValidationHelper.ValidateRequired(request.SourceWorkflowType, nameof(request.SourceWorkflowType));
        ValidationHelper.ValidateRequired(request.SourceAgent, nameof(request.SourceAgent));
        ValidationHelper.ValidateRequired(request.ThreadId, nameof(request.ThreadId));
        ValidationHelper.ValidateRequired(request.Text, nameof(request.Text));
        ValidationHelper.ValidateRequired(request.TenantId, nameof(request.TenantId));

        // Validate that at least one target is specified
        if (string.IsNullOrEmpty(request.TargetWorkflowId) && string.IsNullOrEmpty(request.TargetWorkflowType))
        {
            throw new ArgumentException("Either TargetWorkflowId or TargetWorkflowType must be specified", nameof(request));
        }

        // Build payload
        var payload = new
        {
            targetWorkflowId = request.TargetWorkflowId,
            targetWorkflowType = request.TargetWorkflowType,
            sourceAgent = request.SourceAgent,
            sourceWorkflowType = request.SourceWorkflowType,
            sourceWorkflowId = request.SourceWorkflowId,
            threadId = request.ThreadId,
            participantId = request.ParticipantId,
            authorization = request.Authorization,
            text = request.Text,
            data = request.Data,
            type = "Handoff"
        };

        // Use endpoint: api/agent/conversation/outbound/handoff
        var endpoint = $"{WorkflowConstants.ApiEndpoints.ConversationOutbound}/handoff";

        _logger.LogTrace(
            "Posting handoff to {Endpoint}: TargetWorkflowId={TargetWorkflowId}, TargetWorkflowType={TargetWorkflowType}",
            endpoint,
            request.TargetWorkflowId,
            request.TargetWorkflowType);

        // Create HTTP request with tenant header
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Content = JsonContent.Create(payload);
        httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, request.TenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Handoff send failed: StatusCode={StatusCode}, Error={Error}",
                response.StatusCode,
                error);

            throw new HttpRequestException(
                $"Failed to send handoff. Status: {response.StatusCode}");
        }

        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        
        _logger.LogInformation(
            "Handoff sent successfully: TargetWorkflowId={TargetWorkflowId}, TargetWorkflowType={TargetWorkflowType}",
            request.TargetWorkflowId,
            request.TargetWorkflowType);

        return result;
    }

    /// <summary>
    /// Extracts the retry-after duration from the HTTP response.
    /// Checks both the Retry-After header and the response body.
    /// </summary>
    private int ExtractRetryAfterSeconds(HttpResponseMessage response, string errorBody)
    {
        // First, check the Retry-After header
        if (response.Headers.TryGetValues("Retry-After", out var retryAfterValues))
        {
            var retryAfterValue = retryAfterValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(retryAfterValue) && int.TryParse(retryAfterValue, out var headerSeconds))
            {
                _logger.LogDebug("Retry-After header found: {Seconds} seconds", headerSeconds);
                return headerSeconds;
            }
        }

        // Fall back to parsing the error body JSON
        try
        {
            using var doc = JsonDocument.Parse(errorBody);
            if (doc.RootElement.TryGetProperty("retryAfter", out var retryAfterElement))
            {
                if (retryAfterElement.ValueKind == JsonValueKind.String &&
                    int.TryParse(retryAfterElement.GetString(), out var stringSeconds))
                {
                    _logger.LogDebug("retryAfter from error body: {Seconds} seconds", stringSeconds);
                    return stringSeconds;
                }
                else if (retryAfterElement.ValueKind == JsonValueKind.Number)
                {
                    var numberSeconds = retryAfterElement.GetInt32();
                    _logger.LogDebug("retryAfter from error body: {Seconds} seconds", numberSeconds);
                    return numberSeconds;
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse error body for retryAfter value");
        }

        // Default to 60 seconds if we can't parse it
        _logger.LogWarning("Could not extract retry-after duration, defaulting to 60 seconds");
        return 60;
    }
}

