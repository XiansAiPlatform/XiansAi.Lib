using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Server;
using Temporalio.Activities;
using Temporalio.Common;
using Temporalio.Workflows;
using XiansAi.Knowledge;
using XiansAi.Messaging;
using XiansAi.Models;
using XiansAi.Flow.Router;
using System.Text.Json;
using System.Net;

public class SendMessageResponse
{
    public required string[] MessageIds { get; set; }
}

public class SystemActivities
{
    private static readonly ILogger _logger = Globals.LogFactory.CreateLogger<SystemActivities>();

    private readonly List<Type> _capabilities = new();

    public SystemActivities(List<Type> capabilities)
    {
        _capabilities = capabilities;
    }

    [Activity]
    public async Task SendEvent(EventSignal eventDto)
    {
        await SendEventStatic(eventDto);
    }

    public static async Task SendEventStatic(EventSignal eventDto)
    {
        _logger.LogInformation("Sending event from workflow {SourceWorkflow} to {TargetWorkflow}", 
            eventDto.SourceWorkflowId, eventDto.TargetWorkflowType);

        try
        {
            if (!SecureApi.IsReady)
            {
                throw new InvalidOperationException("SecureApi is not ready. Please ensure it is properly initialized.");
            }

            // Log connection health status for observability
            var healthStatus = SecureApi.Instance.GetConnectionHealthStatus();
            _logger.LogDebug("Connection health before sending event: {HealthStatus}", 
                JsonSerializer.Serialize(healthStatus));

            var response = await SecureApi.Instance.ExecuteWithRetryAsync(async client =>
                await client.PostAsJsonAsync("api/agent/events/with-start", eventDto));

            // Ensure success status code and surface errors for retries
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("HTTP {StatusCode} error sending event: {ErrorContent}", 
                    response.StatusCode, errorContent);
                throw new HttpRequestException($"HTTP {response.StatusCode} error sending event: {errorContent}");
            }

            _logger.LogInformation("Event sent successfully from {SourceWorkflow} to {TargetWorkflow}", 
                eventDto.SourceWorkflowId, eventDto.TargetWorkflowType);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(ex, "SecureApi instance was disposed. Skipping event send operation.");
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error sending event from {SourceWorkflow} to {TargetWorkflow}", 
                eventDto.SourceWorkflowId, eventDto.TargetWorkflowType);
            throw; // Surface HTTP errors for Temporal retry
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            _logger.LogError(ex, "Failed to start and send event from {SourceWorkflow} to {TargetWorkflow}",
                eventDto.SourceWorkflowId, eventDto.TargetWorkflowType);
            throw;
        }
    }


    [Activity]
    public async Task<Knowledge?> GetKnowledgeAsync(string knowledgeName)
    {
        return await GetKnowledgeAsyncStatic(knowledgeName);
    }

    public static async Task<Knowledge?> GetKnowledgeAsyncStatic(string knowledgeName)
    {
        try {
            var knowledgeLoader = new KnowledgeLoaderImpl();
            var knowledge = await knowledgeLoader.Load(knowledgeName);
            return knowledge;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting knowledge: {KnowledgeName}", knowledgeName);
            throw;
        }
    }


    public static async Task<bool> UpdateKnowledgeAsyncStatic(string knowledgeName, string knowledgeType, string knowledgeContent)
    {
        return await UpdateKnowledgeAsyncStatic(knowledgeName, knowledgeType, knowledgeContent);
    }

    [Activity]
    public async Task<bool> UpdateKnowledgeAsync(string knowledgeName, string knowledgeType, string knowledgeContent)
    {
        try
        {
            var knowledgeUpdater = new KnowledgeUpdaterImpl();
            var response = await knowledgeUpdater.Update(knowledgeName, knowledgeType, knowledgeContent);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating knowledge: {InstructionName}", knowledgeName);
            throw;
        }

    }

    [Activity]
    public async Task<string> RouteAsync(MessageThread messageThread, string systemPrompt)
    {
        // do the routing
        return await new SemanticRouterImpl().RouteAsync(messageThread, systemPrompt, _capabilities.ToArray());
    }

    [Activity]
    public async Task<string?> SendHandoff(HandoffRequest message) {
        return await SendHandoffStatic(message);
    }

    public static async Task<string?> SendHandoffStatic(HandoffRequest message) {

        if (!SecureApi.IsReady)
        {
            throw new Exception("App server secure API is not ready, skipping message send operation");
        }

        try
        {
            // Log connection health status for observability
            var healthStatus = SecureApi.Instance.GetConnectionHealthStatus();
            _logger.LogDebug("Connection health before sending handoff: {HealthStatus}", 
                JsonSerializer.Serialize(healthStatus));

            var response = await SecureApi.Instance.ExecuteWithRetryAsync(async client =>
                await client.PostAsJsonAsync("api/agent/conversation/outbound/handoff", message));

            // Ensure success status code and surface errors for retries
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("HTTP {StatusCode} error sending handoff message: {ErrorContent}", 
                    response.StatusCode, errorContent);
                throw new HttpRequestException($"HTTP {response.StatusCode} error sending handoff message: {errorContent}");
            }

            var result = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Handoff message sent successfully for participant {ParticipantId}", 
                message.ParticipantId);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error sending handoff message for participant {ParticipantId}", 
                message.ParticipantId);
            throw; // Surface HTTP errors for Temporal retry
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending handoff message: {Message}", message);
            throw new Exception($"Failed to send handoff message: {ex.Message}");
        }
    }

    [Activity]
    public async Task<string> SendChatOrData(ChatOrDataRequest message, MessageType type) {
        return await SendChatOrDataStatic(message, type);
    }

    public static async Task<string> SendChatOrDataStatic(ChatOrDataRequest message, MessageType type) {

        if (!SecureApi.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping message send operation");
            throw new InvalidOperationException("SecureApi is not ready. Please ensure it is properly initialized.");
        }

        if (type == MessageType.Chat && string.IsNullOrEmpty(message.Text)) {
            throw new Exception("Text is required for chat message");
        }

        if (type == MessageType.Data && message.Data == null) {
            throw new Exception("Data is required for data message");
        }

        try
        {
            // Log connection health status for observability
            var healthStatus = SecureApi.Instance.GetConnectionHealthStatus();
            _logger.LogDebug("Connection health before sending {Type} message: {HealthStatus}", 
                type, JsonSerializer.Serialize(healthStatus));

            var response = await SecureApi.Instance.ExecuteWithRetryAsync(async client =>
                await client.PostAsJsonAsync($"api/agent/conversation/outbound/{type.ToString().ToLower()}", message));

            // Ensure success status code and surface errors for retries
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("HTTP {StatusCode} error sending {Type} message: {ErrorContent}", 
                    response.StatusCode, type, errorContent);
                throw new HttpRequestException($"HTTP {response.StatusCode} error sending {type} message: {errorContent}");
            }

            var result = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("{Type} message sent successfully for participant {ParticipantId}", 
                type, message.ParticipantId);
            return result;
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(ex, "SecureApi instance was disposed. Skipping message send operation.");
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error sending {Type} message for participant {ParticipantId}", 
                type, message.ParticipantId);
            throw; // Surface HTTP errors for Temporal retry
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending {Type} message: {Message}", type, message);
            throw new Exception($"Failed to send {type} message: {ex.Message}");
        }
    }


    [Activity]
    public async Task<List<DbMessage>> GetMessageHistory(string? workflowType, string participantId, int page = 1, int pageSize = 10)
    {
        return await GetMessageHistoryStatic(workflowType, participantId, page, pageSize);
    }

    public static async Task<List<DbMessage>> GetMessageHistoryStatic(string? workflowType, string participantId, int page = 1, int pageSize = 10)
    {
        _logger.LogDebug("Getting message history for thread WorkflowType: '{WorkflowType}' ParticipantId: '{ParticipantId}'", workflowType, participantId);

        if (!SecureApi.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping message history fetch");
            return new List<DbMessage>();
        }
        try
        {
            // Log connection health status for observability
            var healthStatus = SecureApi.Instance.GetConnectionHealthStatus();
            _logger.LogDebug("Connection health before fetching message history: {HealthStatus}", 
                JsonSerializer.Serialize(healthStatus));

            var response = await SecureApi.Instance.ExecuteWithRetryAsync(async client =>
                await client.GetAsync($"api/agent/conversation/history?&workflowType={workflowType}&participantId={participantId}&page={page}&pageSize={pageSize}"));

            // Ensure success status code and surface errors for retries
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("HTTP {StatusCode} error fetching message history: {ErrorContent}", 
                    response.StatusCode, errorContent);
                throw new HttpRequestException($"HTTP {response.StatusCode} error fetching message history: {errorContent}");
            }

            var messages = await response.Content.ReadFromJsonAsync<List<DbMessage>>();
            var result = messages ?? new List<DbMessage>();
            
            _logger.LogDebug("Successfully fetched {Count} messages for participant {ParticipantId}", 
                result.Count, participantId);
            return result;
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(ex, "SecureApi instance was disposed. Skipping message history fetch.");
            return new List<DbMessage>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching message history for thread: {WorkflowType} {ParticipantId}", 
                workflowType, participantId);
            throw; // Surface HTTP errors for Temporal retry
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching message history for thread: {WorkflowType} {ParticipantId}", workflowType, participantId);
            throw;
        }
    }
}

public class SystemActivityOptions : ActivityOptions
{
    public SystemActivityOptions()
    {
        StartToCloseTimeout = TimeSpan.FromSeconds(60);
        RetryPolicy = new RetryPolicy
        {
            InitialInterval = TimeSpan.FromSeconds(1),
            MaximumInterval = TimeSpan.FromSeconds(30),
            MaximumAttempts = 10,
            BackoffCoefficient = 2,
            NonRetryableErrorTypes = new[] 
            { 
                "System.ArgumentNullException",
                "System.ArgumentException",
                "System.InvalidOperationException"
            }
        };
    }
}