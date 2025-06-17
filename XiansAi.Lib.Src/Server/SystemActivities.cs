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
            var client = SecureApi.Instance.Client;
            var response = await client.PostAsJsonAsync("api/agent/events/with-start", eventDto);
            response.EnsureSuccessStatusCode();
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(ex, "SecureApi instance was disposed. Skipping event send operation.");
            throw;
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
            var client = SecureApi.Instance.Client;
            var response = await client.PostAsJsonAsync("api/agent/conversation/outbound/handoff", message);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message: {Message}", message);
            throw new Exception($"Failed to send message: {ex.Message}");
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
            var client = SecureApi.Instance.Client;
            var response = await client.PostAsJsonAsync($"api/agent/conversation/outbound/{type.ToString().ToLower()}", message);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(ex, "SecureApi instance was disposed. Skipping message send operation.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message: {Message}", message);
            throw new Exception($"Failed to send message: {ex.Message}");
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
            var client = SecureApi.Instance.Client;
            var response = await client.GetAsync($"api/agent/conversation/history?&workflowType={workflowType}&participantId={participantId}&page={page}&pageSize={pageSize}");
            response.EnsureSuccessStatusCode();

            var messages = await response.Content.ReadFromJsonAsync<List<DbMessage>>();


            return messages ?? new List<DbMessage>();
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(ex, "SecureApi instance was disposed. Skipping message history fetch.");
            return new List<DbMessage>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching message history for thread: {WorkflowType} {ParticipantId}", workflowType, participantId);
            throw;
        }
    }

    [Activity]
    public async Task ValidateToken(string token, string authProvider)
    {
        await ValidateTokenStatic(token, authProvider);
    }

    public static async Task ValidateTokenStatic(string token, string authProvider)
    {
        try
        {
            _logger.LogInformation("Starting token validation...");

            switch (authProvider.ToLower())
            {
                case "keycloak":
                    {
                        var keycloakService = new KeycloakService();
                        await keycloakService.ValidateTokenAsync(token);
                        break;
                    }
                case "auth0":
                    {
                        var azureService = new Auth0Service();
                        await azureService.ValidateTokenAsync(token);
                        break;
                    }
                case "certificate":
                    {
                        await SendSecureApiRequestStatic<object>("api/agent/settings/flowserver", HttpMethod.Get, token);
                        break;
                    }
                default:
                    throw new NotSupportedException($"Authentication provider '{authProvider}' is not supported.");
            }

            _logger.LogInformation("Token validation completed successfully");
        }
        catch (Exception)
        {
            throw;
        }
    }

    public static async Task<TResponse?> SendSecureApiRequestStatic<TResponse>(string endpoint, HttpMethod method, string token, object? payload = null)
    {
        if (string.IsNullOrEmpty(token))
        {
            throw new ArgumentNullException(nameof(token), "Bearer token is required");
        }

        try
        {
            _logger.LogInformation("Starting secure API request to {Endpoint}", endpoint);

            if (!SecureApi.IsReady)
            {
                throw new InvalidOperationException("SecureApi is not ready. Please ensure it is properly initialized.");
            }

            // Create a new HttpClient with the same base address as SecureApi
            using var client = new HttpClient
            {
                BaseAddress = SecureApi.Instance.Client.BaseAddress,
                Timeout = TimeSpan.FromSeconds(30)
            };

            // Add bearer token to request headers
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            HttpResponseMessage response;
            switch (method.Method.ToUpper())
            {
                case "GET":
                    response = await client.GetAsync(endpoint);
                    break;
                case "POST":
                    response = await client.PostAsJsonAsync(endpoint, payload);
                    break;
                case "PUT":
                    response = await client.PutAsJsonAsync(endpoint, payload);
                    break;
                case "DELETE":
                    response = await client.DeleteAsync(endpoint);
                    break;
                default:
                    throw new NotSupportedException($"HTTP method {method.Method} is not supported.");
            }

            response.EnsureSuccessStatusCode();

            if (typeof(TResponse) == typeof(string))
            {
                return (TResponse)(object)await response.Content.ReadAsStringAsync();
            }

            return await response.Content.ReadFromJsonAsync<TResponse>();
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(ex, "Client was disposed. Request operation failed.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error making secure API request to {Endpoint}", endpoint);
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
            MaximumInterval = TimeSpan.FromSeconds(10),
            MaximumAttempts = 5,
            BackoffCoefficient = 2
        };
    }
}