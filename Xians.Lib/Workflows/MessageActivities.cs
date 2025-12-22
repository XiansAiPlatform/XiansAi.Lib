using System.Net.Http.Json;
using Temporalio.Activities;
using Microsoft.Extensions.Logging;
using System.Net.Http;

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

