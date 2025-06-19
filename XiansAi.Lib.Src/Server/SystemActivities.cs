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
using XiansAi.Server.Base;
using System.Text.Json;

public class SendMessageResponse
{
    public required string[] MessageIds { get; set; }
}

public class SystemActivities
{
    private static readonly ILogger _staticLogger = Globals.LogFactory.CreateLogger<SystemActivities>();
    private readonly List<Type> _capabilities = new();
    private readonly IApiService _apiService;
    private readonly ILogger<SystemActivities> _logger;

    /// <summary>
    /// Constructor for dependency injection with IApiService
    /// </summary>
    public SystemActivities(IApiService apiService, ILogger<SystemActivities> logger, List<Type>? capabilities = null)
    {
        _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _capabilities = capabilities ?? new List<Type>();
    }

    /// <summary>
    /// Legacy constructor for backward compatibility - creates instance without DI
    /// </summary>
    public SystemActivities(List<Type> capabilities)
    {
        _capabilities = capabilities;
        
        // Create a BaseApiService instance for legacy support
        var httpClient = GetLegacyHttpClient();
        _apiService = new LegacyApiServiceWrapper(httpClient, _staticLogger);
        _logger = (ILogger<SystemActivities>)_staticLogger;
    }

    /// <summary>
    /// Gets HttpClient for legacy constructor - fallback to SecureApi
    /// </summary>
    private static HttpClient GetLegacyHttpClient()
    {
        if (!SecureApi.IsReady)
        {
            throw new InvalidOperationException("SecureApi is not ready. Please ensure it is properly initialized or use dependency injection.");
        }
        return SecureApi.Instance.Client;
    }

    /// <summary>
    /// Legacy wrapper that implements IApiService using BaseApiService for backward compatibility
    /// </summary>
    private class LegacyApiServiceWrapper : BaseApiService
    {
        public LegacyApiServiceWrapper(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
        }
    }

    [Activity]
    public async Task SendEvent(EventSignal eventDto)
    {
        await SendEventStatic(eventDto);
    }

    public static async Task SendEventStatic(EventSignal eventDto)
    {
        _staticLogger.LogInformation("Sending event from workflow {SourceWorkflow} to {TargetWorkflow}", 
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
            _staticLogger.LogWarning(ex, "SecureApi instance was disposed. Skipping event send operation.");
            throw;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            _staticLogger.LogError(ex, "Failed to start and send event from {SourceWorkflow} to {TargetWorkflow}",
                eventDto.SourceWorkflowId, eventDto.TargetWorkflowType);
            throw;
        }
    }

    /// <summary>
    /// Instance method that uses BaseApiService for clean API calls
    /// </summary>
    public async Task SendEventAsync(EventSignal eventDto)
    {
        _logger.LogInformation("Sending event from workflow {SourceWorkflow} to {TargetWorkflow}", 
            eventDto.SourceWorkflowId, eventDto.TargetWorkflowType);

        try
        {
            await _apiService.PostAsync("api/agent/events/with-start", eventDto);
        }
        catch (Exception ex)
        {
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
            _staticLogger.LogError(ex, "Error getting knowledge: {KnowledgeName}", knowledgeName);
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

    /// <summary>
    /// Instance method that uses BaseApiService for clean API calls
    /// </summary>
    public async Task<string?> SendHandoffAsync(HandoffRequest message) 
    {
        try
        {
            return await _apiService.PostAsync("api/agent/conversation/outbound/handoff", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending handoff message: {Message}", message);
            throw new Exception($"Failed to send handoff message: {ex.Message}");
        }
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
            _staticLogger.LogError(ex, "Error sending message: {Message}", message);
            throw new Exception($"Failed to send message: {ex.Message}");
        }
    }

    [Activity]
    public async Task<string> SendChatOrData(ChatOrDataRequest message, MessageType type) {
        return await SendChatOrDataStatic(message, type);
    }

    /// <summary>
    /// Instance method that uses BaseApiService for clean API calls
    /// </summary>
    public async Task<string> SendChatOrDataAsync(ChatOrDataRequest message, MessageType type) 
    {
        if (type == MessageType.Chat && string.IsNullOrEmpty(message.Text)) {
            throw new Exception("Text is required for chat message");
        }

        if (type == MessageType.Data && message.Data == null) {
            throw new Exception("Data is required for data message");
        }

        try
        {
            return await _apiService.PostAsync($"api/agent/conversation/outbound/{type.ToString().ToLower()}", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message: {Message}", message);
            throw new Exception($"Failed to send message: {ex.Message}");
        }
    }

    public static async Task<string> SendChatOrDataStatic(ChatOrDataRequest message, MessageType type) {

        if (!SecureApi.IsReady)
        {
            _staticLogger.LogWarning("App server secure API is not ready, skipping message send operation");
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
            _staticLogger.LogWarning(ex, "SecureApi instance was disposed. Skipping message send operation.");
            throw;
        }
        catch (Exception ex)
        {
            _staticLogger.LogError(ex, "Error sending message: {Message}", message);
            throw new Exception($"Failed to send message: {ex.Message}");
        }
    }

    [Activity]
    public async Task<List<DbMessage>> GetMessageHistory(string? workflowType, string participantId, int page = 1, int pageSize = 10)
    {
        return await GetMessageHistoryStatic(workflowType, participantId, page, pageSize);
    }

    /// <summary>
    /// Instance method that uses BaseApiService for clean API calls
    /// </summary>
    public async Task<List<DbMessage>> GetMessageHistoryAsync(string? workflowType, string participantId, int page = 1, int pageSize = 10)
    {
        _logger.LogDebug("Getting message history for thread WorkflowType: '{WorkflowType}' ParticipantId: '{ParticipantId}'", workflowType, participantId);

        try
        {
            var messages = await _apiService.GetAsync<List<DbMessage>>($"api/agent/conversation/history?&workflowType={workflowType}&participantId={participantId}&page={page}&pageSize={pageSize}");
            return messages ?? new List<DbMessage>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching message history for thread: {WorkflowType} {ParticipantId}", workflowType, participantId);
            throw;
        }
    }

    public static async Task<List<DbMessage>> GetMessageHistoryStatic(string? workflowType, string participantId, int page = 1, int pageSize = 10)
    {
        _staticLogger.LogDebug("Getting message history for thread WorkflowType: '{WorkflowType}' ParticipantId: '{ParticipantId}'", workflowType, participantId);

        if (!SecureApi.IsReady)
        {
            _staticLogger.LogWarning("App server secure API is not ready, skipping message history fetch");
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
            _staticLogger.LogWarning(ex, "SecureApi instance was disposed. Skipping message history fetch.");
            return new List<DbMessage>();
        }
        catch (Exception ex)
        {
            _staticLogger.LogError(ex, "Error fetching message history for thread: {WorkflowType} {ParticipantId}", workflowType, participantId);
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