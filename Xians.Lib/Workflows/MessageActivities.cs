using System.Net.Http.Json;
using Temporalio.Activities;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Xians.Lib.Agents;
using Xians.Lib.Agents.Models;
using Xians.Lib.Workflows.Models;

namespace Xians.Lib.Workflows;

/// <summary>
/// Activities for sending messages back to the Xians platform.
/// Activities can perform non-deterministic operations like HTTP calls.
/// </summary>
public class MessageActivities
{
    private readonly HttpClient _httpClient;

    public MessageActivities(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Processes a user message by invoking the registered handler and sending responses.
    /// This activity encapsulates the full process: agent API calls and sending responses.
    /// </summary>
    [Activity]
    public async Task ProcessAndSendMessageAsync(ProcessMessageActivityRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "ProcessAndSendMessage activity started: RequestId={RequestId}, WorkflowType={WorkflowType}",
            request.RequestId,
            request.WorkflowType);

        try
        {
            // Look up the handler from the static registry (avoids serialization issues)
            if (!DefaultWorkflow._handlersByWorkflowType.TryGetValue(request.WorkflowType, out var metadata))
            {
                var errorMessage = $"No message handler registered for workflow type '{request.WorkflowType}' in activity.";
                ActivityExecutionContext.Current.Logger.LogError(
                    "Handler lookup failed: WorkflowType={WorkflowType}",
                    request.WorkflowType);
                
                // Throw exception to let workflow handle error response
                throw new InvalidOperationException(errorMessage);
            }

            // Create a context that sends responses via HTTP instead of collecting them
            var context = new ActivityUserMessageContext(
                _httpClient,
                new UserMessage { Text = request.MessageText },
                request.ParticipantId,
                request.RequestId,
                request.Scope,
                request.Hint,
                request.Data,
                request.TenantId,
                request.WorkflowId,
                request.WorkflowType,
                request.Authorization,
                request.ThreadId
            );

            // Invoke the registered handler (which makes agent API calls and sends responses)
            await metadata.Handler(context);

            ActivityExecutionContext.Current.Logger.LogInformation(
                "Message processed and responses sent: RequestId={RequestId}",
                request.RequestId);
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error processing message: RequestId={RequestId}",
                request.RequestId);

            // Re-throw to let Temporal handle retry
            // The workflow will send error response to user after all retries are exhausted
            throw;
        }
    }

    /// <summary>
    /// Retrieves paginated chat history for a conversation from the server.
    /// For system-scoped agents, uses tenant ID from workflow context.
    /// </summary>
    /// <param name="request">The request containing participant and pagination details.</param>
    [Activity]
    public async Task<List<DbMessage>> GetMessageHistoryAsync(GetMessageHistoryRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "GetMessageHistory activity started: WorkflowType={WorkflowType}, Page={Page}, PageSize={PageSize}",
            request.WorkflowType,
            request.Page,
            request.PageSize);
        
        try
        {
            // Build query string for history endpoint with proper URL encoding
            var endpoint = $"api/agent/conversation/history?" +
                          $"workflowType={Uri.EscapeDataString(request.WorkflowType)}" +
                          $"&participantId={Uri.EscapeDataString(request.ParticipantId)}" +
                          $"&page={request.Page}" +
                          $"&pageSize={request.PageSize}" +
                          $"&scope={Uri.EscapeDataString(request.Scope)}";
            
            ActivityExecutionContext.Current.Logger.LogTrace(
                "Fetching message history from {Endpoint}",
                endpoint);
            
            // Create HTTP request message to add tenant header
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
            
            // Add X-Tenant-Id header for tenant routing (critical for system-scoped agents)
            // This ensures history is fetched from the correct tenant's context
            httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", request.TenantId);
            
            var response = await _httpClient.SendAsync(httpRequest);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                ActivityExecutionContext.Current.Logger.LogError(
                    "Failed to fetch message history: StatusCode={StatusCode}, Error={Error}",
                    response.StatusCode,
                    error);
                // Don't expose server error details to prevent information disclosure
                throw new HttpRequestException(
                    $"Failed to fetch message history. Status: {response.StatusCode}");
            }
            
            var messages = await response.Content.ReadFromJsonAsync<List<DbMessage>>();
            
            ActivityExecutionContext.Current.Logger.LogInformation(
                "Message history fetched successfully: {Count} messages",
                messages?.Count ?? 0);
            
            return messages ?? new List<DbMessage>();
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error fetching message history for WorkflowType={WorkflowType}",
                request.WorkflowType);
            throw;
        }
    }

    /// <summary>
    /// Sends a chat or data message to a participant via the Xians platform API.
    /// Uses the same endpoint format as XiansAi.Lib.Src SystemActivities.
    /// For system-scoped agents, includes X-Tenant-Id header to route to correct tenant.
    /// </summary>
    /// <param name="request">The message request containing all message details.</param>
    [Activity]
    public async Task SendMessageAsync(SendMessageRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "SendMessage activity started: Type={Type}, RequestId={RequestId}",
            request.Type,
            request.RequestId);
        
        // Validate message type against whitelist
        var allowedTypes = new[] { "chat", "data" };
        var messageType = request.Type.ToLower();
        if (!allowedTypes.Contains(messageType))
        {
            var error = $"Invalid message type: {request.Type}. Allowed types: {string.Join(", ", allowedTypes)}";
            ActivityExecutionContext.Current.Logger.LogError(error);
            throw new ArgumentException(error, nameof(request.Type));
        }
        
        // Build payload matching the ChatOrDataRequest structure from XiansAi.Lib.Src
        var payload = new
        {
            participantId = request.ParticipantId,
            workflowId = request.WorkflowId,
            workflowType = request.WorkflowType,
            requestId = request.RequestId,
            scope = request.Scope,
            data = request.Data,
            authorization = request.Authorization,
            text = request.Text,
            threadId = request.ThreadId,
            hint = request.Hint,
            origin = request.Origin
        };

        // Use the correct endpoint: api/agent/conversation/outbound/{type}
        // Type is lowercase: "chat" or "data"
        var endpoint = $"api/agent/conversation/outbound/{messageType}";
        
        ActivityExecutionContext.Current.Logger.LogTrace(
            "Posting to {Endpoint}: TextLength={TextLength}",
            endpoint,
            request.Text?.Length ?? 0);
        
        // Create HTTP request message to add tenant header
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Content = JsonContent.Create(payload);
        
        // Add X-Tenant-Id header for tenant routing (critical for system-scoped agents)
        // This matches the TenantIdHandler behavior from XiansAi.Lib.Src
        httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", request.TenantId);
        
        var response = await _httpClient.SendAsync(httpRequest);

        ActivityExecutionContext.Current.Logger.LogDebug(
            "HTTP response: StatusCode={StatusCode}, IsSuccess={IsSuccess}",
            response.StatusCode,
            response.IsSuccessStatusCode);

        // Throw exception if the request failed - Temporal will retry automatically
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            
            ActivityExecutionContext.Current.Logger.LogError(
                "Message send failed: StatusCode={StatusCode}, Error={Error}",
                response.StatusCode,
                error);
            
            // Don't expose participant ID or detailed error to prevent information disclosure
            throw new HttpRequestException(
                $"Failed to send message. Status: {response.StatusCode}");
        }
        
        ActivityExecutionContext.Current.Logger.LogInformation(
            "Message sent successfully: RequestId={RequestId}",
            request.RequestId);
    }
}

/// <summary>
/// Activity-safe version of UserMessageContext that sends responses via HTTP
/// instead of executing workflow activities.
/// </summary>
public class ActivityUserMessageContext : UserMessageContext
{
    private readonly HttpClient _httpClient;
    private readonly string _workflowId;
    private readonly string _workflowType;
    private readonly string _participantId;
    private readonly string _requestId;
    private readonly string _scope;
    private readonly string? _threadId;
    private readonly string? _authorization;
    private readonly string _hint;
    private readonly string _tenantId;

    public ActivityUserMessageContext(
        HttpClient httpClient,
        UserMessage message,
        string participantId,
        string requestId,
        string scope,
        string hint,
        object data,
        string tenantId,
        string workflowId,
        string workflowType,
        string? authorization = null,
        string? threadId = null)
        : base(message, participantId, requestId, scope, hint, data, tenantId, authorization, threadId)
    {
        _httpClient = httpClient;
        _participantId = participantId;
        _requestId = requestId;
        _scope = scope;
        _hint = hint;
        _tenantId = tenantId;
        _workflowId = workflowId;
        _workflowType = workflowType;
        _authorization = authorization;
        _threadId = threadId;
    }

    /// <summary>
    /// Sends response via HTTP instead of workflow activity.
    /// </summary>
    public override async Task ReplyAsync(string response)
    {
        await SendHttpMessageAsync(response, null);
    }

    /// <summary>
    /// Sends response with data via HTTP instead of workflow activity.
    /// </summary>
    public override async Task ReplyWithDataAsync(string content, object? data)
    {
        await SendHttpMessageAsync(content, data);
    }

    /// <summary>
    /// Retrieves chat history via HTTP instead of workflow activity.
    /// For system-scoped agents, uses tenant ID from workflow context.
    /// </summary>
    public override async Task<List<DbMessage>> GetChatHistoryAsync(int page = 1, int pageSize = 10)
    {
        // Build query string with proper URL encoding
        var endpoint = $"api/agent/conversation/history?" +
                      $"workflowType={Uri.EscapeDataString(_workflowType ?? string.Empty)}" +
                      $"&participantId={Uri.EscapeDataString(_participantId ?? string.Empty)}" +
                      $"&page={page}" +
                      $"&pageSize={pageSize}" +
                      $"&scope={Uri.EscapeDataString(_scope ?? string.Empty)}";
        
        // Create HTTP request message to add tenant header
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        
        // Add X-Tenant-Id header for tenant routing (critical for system-scoped agents)
        httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", _tenantId);
        
        var response = await _httpClient.SendAsync(httpRequest);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            // Log detailed error but don't expose to caller
            // (Logging context available in activity context if needed)
            throw new HttpRequestException(
                $"Failed to fetch message history. Status: {response.StatusCode}");
        }

        var messages = await response.Content.ReadFromJsonAsync<List<DbMessage>>();
        return messages ?? new List<DbMessage>();
    }

    /// <summary>
    /// Retrieves knowledge via HTTP instead of workflow activity.
    /// </summary>
    public override async Task<Knowledge?> GetKnowledgeAsync(string knowledgeName)
    {
        var endpoint = $"api/agent/knowledge/latest?" +
                      $"name={Uri.EscapeDataString(knowledgeName)}" +
                      $"&agent={Uri.EscapeDataString(GetAgentNameFromWorkflowType())}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", _tenantId);

        var response = await _httpClient.SendAsync(httpRequest);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Failed to fetch knowledge. Status: {response.StatusCode}");
        }

        return await response.Content.ReadFromJsonAsync<Knowledge>();
    }

    /// <summary>
    /// Updates knowledge via HTTP instead of workflow activity.
    /// </summary>
    public override async Task<bool> UpdateKnowledgeAsync(string knowledgeName, string content, string? type = null)
    {
        var knowledge = new Knowledge
        {
            Name = knowledgeName,
            Content = content,
            Type = type,
            Agent = GetAgentNameFromWorkflowType(),
            TenantId = _tenantId
        };

        var endpoint = "api/agent/knowledge";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Content = JsonContent.Create(knowledge);
        httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", _tenantId);

        var response = await _httpClient.SendAsync(httpRequest);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Failed to update knowledge. Status: {response.StatusCode}");
        }

        return true;
    }

    /// <summary>
    /// Deletes knowledge via HTTP instead of workflow activity.
    /// </summary>
    public override async Task<bool> DeleteKnowledgeAsync(string knowledgeName)
    {
        var endpoint = $"api/agent/knowledge?" +
                      $"name={Uri.EscapeDataString(knowledgeName)}" +
                      $"&agent={Uri.EscapeDataString(GetAgentNameFromWorkflowType())}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Delete, endpoint);
        httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", _tenantId);

        var response = await _httpClient.SendAsync(httpRequest);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Failed to delete knowledge. Status: {response.StatusCode}");
        }

        return true;
    }

    /// <summary>
    /// Lists knowledge via HTTP instead of workflow activity.
    /// </summary>
    public override async Task<List<Knowledge>> ListKnowledgeAsync()
    {
        var endpoint = $"api/agent/knowledge/list?" +
                      $"agent={Uri.EscapeDataString(GetAgentNameFromWorkflowType())}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", _tenantId);

        var response = await _httpClient.SendAsync(httpRequest);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Failed to list knowledge. Status: {response.StatusCode}");
        }

        var knowledgeList = await response.Content.ReadFromJsonAsync<List<Knowledge>>();
        return knowledgeList ?? new List<Knowledge>();
    }

    /// <summary>
    /// Extracts agent name from workflow type.
    /// </summary>
    private string GetAgentNameFromWorkflowType()
    {
        var separatorIndex = _workflowType.IndexOf(':');
        return separatorIndex > 0 ? _workflowType.Substring(0, separatorIndex) : _workflowType;
    }

    /// <summary>
    /// Sends message directly via HTTP API.
    /// </summary>
    private async Task SendHttpMessageAsync(string text, object? data)
    {
        var payload = new
        {
            participantId = _participantId,
            workflowId = _workflowId,
            workflowType = _workflowType,
            requestId = _requestId,
            scope = _scope,
            data = data,
            authorization = _authorization,
            text = text,
            threadId = _threadId,
            hint = _hint,
            origin = (string?)null
        };

        var endpoint = "api/agent/conversation/outbound/chat";
        
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Content = JsonContent.Create(payload);
        httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", _tenantId);
        
        var response = await _httpClient.SendAsync(httpRequest);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            // Log error details but don't expose participant ID or server errors
            throw new HttpRequestException(
                $"Failed to send message. Status: {response.StatusCode}");
        }
    }
}
