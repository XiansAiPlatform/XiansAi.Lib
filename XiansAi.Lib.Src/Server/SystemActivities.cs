using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Server;
using Temporalio.Activities;
using Temporalio.Workflows;
using XiansAi.Knowledge;
using XiansAi.Messaging;
using XiansAi.Models;
using XiansAi.Flow.Router;
using XiansAi.Flow;
using System.Text.Json;
using Temporal;
using XiansAi.Memory;

public class SendMessageResponse
{
    public required string[] MessageIds { get; set; }
}
    
public class ProcessDataSettings
{
    public bool ShouldProcessDataInWorkflow { get; set; }
    public string? DataProcessorTypeName { get; set; }
}

public class ScheduleSettings
{
    public required string? ScheduleProcessorTypeName { get; set; }
    public required bool ShouldProcessScheduleInWorkflow { get; set; }
    public required bool RunAtStart { get; set; }
}

public class SystemActivities
{
    private static readonly ILogger _logger = Globals.LogFactory.CreateLogger<SystemActivities>();

    private readonly List<Type> _capabilities = new();
    private readonly IChatInterceptor? _chatInterceptor;
    private readonly List<IKernelModifier> _kernelModifiers;
    private readonly Type? _dataProcessorType;
    private readonly bool _processDataInWorkflow;
    private readonly bool? _runAtStart;
    private readonly Type? _scheduleProcessorType;
    private readonly bool _processScheduleInWorkflow;
    private readonly KernelPlugins _plugins;
    
    internal SystemActivities(dynamic flow)
    {
        _capabilities = flow.Capabilities;
        _chatInterceptor = flow.ChatInterceptor;
        _kernelModifiers = flow.KernelModifiers;
        _dataProcessorType = flow.DataProcessorType;
        _processDataInWorkflow = flow.ProcessDataInWorkflow;
        _runAtStart = flow.RunAtStart;
        _scheduleProcessorType = flow.ScheduleProcessorType;
        _processScheduleInWorkflow = flow.ProcessScheduleInWorkflow;
        _plugins = flow.Plugins;
    }

    [Activity]
    public async Task<Document?> GetAsync(string id)
    {
        return await MemoryHub.Documents.GetAsync(id);
    }

    [Activity]
    public async Task<Document>  SaveDocument(Document document, DocumentOptions? options = null) 
    {
        return await MemoryHub.Documents.SaveAsync(document, options);
    }

    [Activity]
    public async Task<Document?> GetByKeyAsync(string type, string key)
    {
        return await MemoryHub.Documents.GetByKeyAsync(type, key);
    }

    [Activity]
    public async Task<List<Document>> QueryAsync(DocumentQuery query)
    {
        return await MemoryHub.Documents.QueryAsync(query);
    }

    [Activity]
    public async Task<bool> UpdateAsync(Document document)
    {
        return await MemoryHub.Documents.UpdateAsync(document);
    }

    [Activity]
    public async Task<bool> DeleteAsync(string id)
    {
        return await MemoryHub.Documents.DeleteAsync(id);
    }

    [Activity]
    public async Task<int> DeleteManyAsync(IEnumerable<string> ids)
    {
        return await MemoryHub.Documents.DeleteManyAsync(ids);
    }

    [Activity]
    public async Task<bool> ExistsAsync(string id)
    {
        return await MemoryHub.Documents.ExistsAsync(id);
    }

    [Activity]
    public async Task<object?> SendUpdateWithStart(string workflow, string update, params object?[] args) {
        return await UpdateServiceImpl.SendUpdateWithStart(workflow, update, args);
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
    public async Task<string?> ChatCompletionAsync(string prompt, string? systemInstruction, RouterOptions? routerOptions) {
        return await new SemanticRouterHubImpl().CompletionAsync(prompt, systemInstruction, routerOptions);
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
    public async Task<bool> UpdateKnowledgeAsync(string knowledgeName, string knowledgeType, string knowledgeContent)
    {
        return await UpdateKnowledgeAsyncStatic(knowledgeName, knowledgeType, knowledgeContent);

    }

    [Activity]
    public ScheduleSettings GetScheduleSettings()
    {
        return new ScheduleSettings {
            ScheduleProcessorTypeName = _scheduleProcessorType?.AssemblyQualifiedName,
            ShouldProcessScheduleInWorkflow = _processScheduleInWorkflow,
            RunAtStart = _runAtStart ?? false
        };
    }

    [Activity]
    public ProcessDataSettings GetProcessDataSettings()
    {
        var settings = new ProcessDataSettings {
            ShouldProcessDataInWorkflow = _processDataInWorkflow,
            DataProcessorTypeName = _dataProcessorType?.AssemblyQualifiedName
        };
        _logger.LogDebug("ProcessDataSettings: {Settings}", JsonSerializer.Serialize(settings));
        return settings;
    }

    [Activity]
    public string InvokeScheduledMethod(string processorTypeName, string methodName)
    {
        return ScheduleHandler.InvokeScheduledMethod(processorTypeName, methodName, []);
    }

    [Activity]
    public async Task ProcessData(MessageThread messageThread)
    {
        if (_processDataInWorkflow)
        {
            throw new InvalidOperationException("Data processing is set to process in workflow, but this activity is not in workflow");
        }
        
        // do the routing
        await DataHandler.ProcessDataStatic(_dataProcessorType, messageThread, null);
    }

    [Activity]
    public async Task<string?> RouteAsync(MessageThread messageThread, string systemPrompt, RouterOptions options)
    {
        // do the routing
        return await new SemanticRouterHubImpl().RouteAsync(messageThread, systemPrompt, _capabilities.ToArray(), options, _chatInterceptor, _kernelModifiers, _plugins);
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
    public async Task<MessageResponse> SendBotToBotMessage(ChatOrDataRequest chatOrDataMessage, MessageType type, int timeoutSeconds) 
    {
        return await SendBotToBotMessageStatic( chatOrDataMessage, type, timeoutSeconds);
    }

    public static async Task<MessageResponse> SendBotToBotMessageStatic(ChatOrDataRequest chatOrDataMessage, MessageType type, int timeoutSeconds) 
    {
        if (!SecureApi.IsReady)
        {
            throw new Exception("App server secure API is not ready, skipping message send operation");
        }

        try
        {
            _logger.LogDebug("Sending message: {Message}", JsonSerializer.Serialize(chatOrDataMessage));
            var client = SecureApi.Instance.Client;
            var response = await client.PostAsJsonAsync($"api/agent/conversation/converse?type={type}&timeoutSeconds={timeoutSeconds}", chatOrDataMessage);
            response.EnsureSuccessStatusCode();
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse>();
            
            return apiResponse?.Response ?? throw new Exception("No Conversation response from the Agent");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message: {Message}", chatOrDataMessage);
            throw new Exception($"Failed to send message: {ex.Message}");
        }
    }

    [Activity]
    public async Task<string> SendChatOrData(ChatOrDataRequest message, MessageType type) {
        return await SendChatOrDataStatic(message, type);
    }

    public static async Task<string> SendChatOrDataStatic(ChatOrDataRequest message, MessageType type) {

        _logger.LogDebug("Sending message: {Message}", JsonSerializer.Serialize(message));

        if (!SecureApi.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping message send operation");
            throw new InvalidOperationException("SecureApi is not ready. Please ensure it is properly initialized.");
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
    public async Task<List<DbMessage>> GetMessageHistory(string? workflowType, string participantId, string? scope, int page = 1, int pageSize = 10)
    {
        return await GetMessageHistoryStatic(workflowType, participantId, scope, page, pageSize);
    }

    public static async Task<List<DbMessage>> GetMessageHistoryStatic(string? workflowType, string participantId, string? scope, int page = 1, int pageSize = 10)
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
            var response = await client.GetAsync($"api/agent/conversation/history?&workflowType={workflowType}&participantId={participantId}&page={page}&pageSize={pageSize}&scope={scope}");
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
}

public class SystemActivityOptions : ActivityOptions
{
    public SystemActivityOptions(int timeoutSeconds = 10*60)
    {
        ScheduleToCloseTimeout = TimeSpan.FromSeconds(timeoutSeconds);
    }
}

public class SystemLocalActivityOptions : LocalActivityOptions
{
    public SystemLocalActivityOptions(int timeoutSeconds = 10*60)
    {
        ScheduleToCloseTimeout = TimeSpan.FromSeconds(timeoutSeconds);
    }
}