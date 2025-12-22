using System.Net.Http.Json;
using Temporalio.Activities;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Xians.Lib.Agents;

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
            WorkflowHandlerMetadata? metadata;
            lock (DefaultWorkflow._handlersByWorkflowType)
            {
                DefaultWorkflow._handlersByWorkflowType.TryGetValue(request.WorkflowType, out metadata);
            }

            if (metadata == null)
            {
                var errorMessage = $"No message handler registered for workflow type '{request.WorkflowType}' in activity.";
                ActivityExecutionContext.Current.Logger.LogError(
                    "Handler lookup failed: WorkflowType={WorkflowType}",
                    request.WorkflowType);
                
                await SendErrorResponseAsync(
                    request.ParticipantId,
                    request.WorkflowId,
                    request.WorkflowType,
                    request.RequestId,
                    request.Scope,
                    request.ThreadId,
                    request.Authorization,
                    request.Hint,
                    request.TenantId,
                    errorMessage);
                return;
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

            // Send error response directly
            await SendErrorResponseAsync(
                request.ParticipantId,
                request.WorkflowId,
                request.WorkflowType,
                request.RequestId,
                request.Scope,
                request.ThreadId,
                request.Authorization,
                request.Hint,
                request.TenantId,
                ex.Message);

            throw; // Re-throw to let Temporal handle retry
        }
    }

    /// <summary>
    /// Helper method to send error responses.
    /// </summary>
    private async Task SendErrorResponseAsync(
        string participantId,
        string workflowId,
        string workflowType,
        string requestId,
        string scope,
        string? threadId,
        string? authorization,
        string hint,
        string tenantId,
        string errorMessage)
    {
        var request = new SendMessageRequest
        {
            ParticipantId = participantId,
            WorkflowId = workflowId,
            WorkflowType = workflowType,
            Text = $"Error: {errorMessage}",
            Data = null,
            RequestId = requestId,
            Scope = scope,
            ThreadId = threadId,
            Authorization = authorization,
            Hint = hint,
            Origin = null,
            Type = "Chat",
            TenantId = tenantId
        };

        await SendMessageAsync(request);
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
            "GetMessageHistory activity started: WorkflowType={WorkflowType}, ParticipantId={ParticipantId}, Page={Page}, PageSize={PageSize}, Tenant={Tenant}",
            request.WorkflowType,
            request.ParticipantId,
            request.Page,
            request.PageSize,
            request.TenantId);
        
        try
        {
            // Build query string for history endpoint
            var endpoint = $"api/agent/conversation/history?workflowType={request.WorkflowType}" +
                          $"&participantId={request.ParticipantId}" +
                          $"&page={request.Page}" +
                          $"&pageSize={request.PageSize}" +
                          $"&scope={request.Scope}";
            
            ActivityExecutionContext.Current.Logger.LogDebug(
                "Fetching message history from {Endpoint}, Tenant={Tenant}",
                endpoint,
                request.TenantId);
            
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
                    "Failed to fetch message history: StatusCode={StatusCode}, Error={Error}, Tenant={Tenant}",
                    response.StatusCode,
                    error,
                    request.TenantId);
                throw new HttpRequestException(
                    $"Failed to fetch message history. Status: {response.StatusCode}, Error: {error}");
            }
            
            var messages = await response.Content.ReadFromJsonAsync<List<DbMessage>>();
            
            ActivityExecutionContext.Current.Logger.LogInformation(
                "Message history fetched successfully: {Count} messages, Tenant={Tenant}",
                messages?.Count ?? 0,
                request.TenantId);
            
            return messages ?? new List<DbMessage>();
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error fetching message history for WorkflowType={WorkflowType}, ParticipantId={ParticipantId}, Tenant={Tenant}",
                request.WorkflowType,
                request.ParticipantId,
                request.TenantId);
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
            "SendMessage activity started: ParticipantId={ParticipantId}, Type={Type}, RequestId={RequestId}, Tenant={Tenant}",
            request.ParticipantId,
            request.Type,
            request.RequestId,
            request.TenantId);
        
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
        var endpoint = $"api/agent/conversation/outbound/{request.Type.ToLower()}";
        
        ActivityExecutionContext.Current.Logger.LogDebug(
            "Posting to {Endpoint}: WorkflowId={WorkflowId}, Tenant={Tenant}, TextLength={TextLength}",
            endpoint,
            request.WorkflowId,
            request.TenantId,
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
                "Message send failed: StatusCode={StatusCode}, Error={Error}, ParticipantId={ParticipantId}",
                response.StatusCode,
                error,
                request.ParticipantId);
            
            throw new HttpRequestException(
                $"Failed to send message to participant {request.ParticipantId}. " +
                $"Status: {response.StatusCode}, Error: {error}");
        }
        
        ActivityExecutionContext.Current.Logger.LogInformation(
            "Message sent successfully: ParticipantId={ParticipantId}, RequestId={RequestId}",
            request.ParticipantId,
            request.RequestId);
    }
}

/// <summary>
/// Request object for processing messages via activity.
/// </summary>
public class ProcessMessageActivityRequest
{
    public required string MessageText { get; set; }
    public required string ParticipantId { get; set; }
    public required string RequestId { get; set; }
    public required string Scope { get; set; }
    public required string Hint { get; set; }
    public required object Data { get; set; }
    public required string TenantId { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
    public string? Authorization { get; set; }
    public string? ThreadId { get; set; }
    // Handler is looked up from static registry using WorkflowType - not passed to avoid serialization issues
}

/// <summary>
/// Activity-safe version of UserMessageContext that sends responses via HTTP
/// instead of executing workflow activities.
/// </summary>
public class ActivityUserMessageContext : Xians.Lib.Agents.UserMessageContext
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
        Xians.Lib.Agents.UserMessage message,
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
        var endpoint = $"api/agent/conversation/history?workflowType={_workflowType}" +
                      $"&participantId={_participantId}" +
                      $"&page={page}" +
                      $"&pageSize={pageSize}" +
                      $"&scope={_scope}";
        
        // Create HTTP request message to add tenant header
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        
        // Add X-Tenant-Id header for tenant routing (critical for system-scoped agents)
        httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", _tenantId);
        
        var response = await _httpClient.SendAsync(httpRequest);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Failed to fetch message history. Status: {response.StatusCode}, Error: {error}");
        }

        var messages = await response.Content.ReadFromJsonAsync<List<DbMessage>>();
        return messages ?? new List<DbMessage>();
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
            throw new HttpRequestException(
                $"Failed to send message to participant {_participantId}. " +
                $"Status: {response.StatusCode}, Error: {error}");
        }
    }
}

/// <summary>
/// Request object for sending messages via activity.
/// Using a single parameter object is recommended by Temporal.
/// Matches the ChatOrDataRequest structure from XiansAi.Lib.Src.
/// </summary>
public class SendMessageRequest
{
    public required string ParticipantId { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
    public required string RequestId { get; set; }
    public string? Scope { get; set; }
    public object? Data { get; set; }
    public string? Authorization { get; set; }
    public string? Text { get; set; }
    public string? ThreadId { get; set; }
    public string? Hint { get; set; }
    public string? Origin { get; set; }
    public required string Type { get; set; }
    /// <summary>
    /// Tenant ID from the workflow context. For system-scoped agents, this ensures
    /// replies are sent to the correct tenant that initiated the workflow.
    /// </summary>
    public required string TenantId { get; set; }
}

/// <summary>
/// Request object for retrieving message history via activity.
/// </summary>
public class GetMessageHistoryRequest
{
    public required string WorkflowType { get; set; }
    public required string ParticipantId { get; set; }
    public required string Scope { get; set; }
    public required string TenantId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

