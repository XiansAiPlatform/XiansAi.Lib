using System.Net.Http.Json;
using Temporalio.Activities;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Agents.Messaging.Models;
using Xians.Lib.Workflows.Messaging.Models;
using Xians.Lib.Agents.Knowledge;
using Xians.Lib.Agents.Knowledge.Models;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Workflows.Knowledge;

namespace Xians.Lib.Workflows.Messaging;

/// <summary>
/// Activities for sending messages back to the Xians platform.
/// Activities can perform non-deterministic operations like HTTP calls.
/// Delegates to shared MessageService to avoid code duplication.
/// </summary>
public class MessageActivities
{
    private readonly HttpClient _httpClient;
    private readonly Xians.Lib.Agents.Messaging.MessageService _messageService;

    public MessageActivities(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        // Create shared message service
        var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<Xians.Lib.Agents.Messaging.MessageService>();
        _messageService = new Xians.Lib.Agents.Messaging.MessageService(httpClient, logger);
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
    /// Delegates to shared MessageService.
    /// </summary>
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
            return await _messageService.GetHistoryAsync(
                request.WorkflowType,
                request.ParticipantId,
                request.Scope,
                request.TenantId,
                request.Page,
                request.PageSize);
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
    /// Delegates to shared MessageService.
    /// </summary>
    [Activity]
    public async Task SendMessageAsync(SendMessageRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "SendMessage activity started: Type={Type}, RequestId={RequestId}",
            request.Type,
            request.RequestId);
        
        try
        {
            await _messageService.SendAsync(
                request.ParticipantId,
                request.WorkflowId,
                request.WorkflowType,
                request.RequestId,
                request.Scope ?? string.Empty,
                request.Text ?? string.Empty,
                request.Data,
                request.TenantId,
                request.Authorization,
                request.ThreadId,
                request.Hint ?? string.Empty,
                request.Origin,
                request.Type);

            ActivityExecutionContext.Current.Logger.LogInformation(
                "Message sent successfully: RequestId={RequestId}",
                request.RequestId);
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error sending message: RequestId={RequestId}",
                request.RequestId);
            throw;
        }
    }
}

/// <summary>
/// Activity-safe version of UserMessageContext that sends responses via HTTP
/// instead of executing workflow activities.
/// </summary>
public class ActivityUserMessageContext : UserMessageContext
{
    private readonly Xians.Lib.Agents.Messaging.MessageService _messageService;
    private readonly Xians.Lib.Agents.Knowledge.KnowledgeService _knowledgeService;
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
        _participantId = participantId;
        _requestId = requestId;
        _scope = scope;
        _hint = hint;
        _tenantId = tenantId;
        _workflowId = workflowId;
        _workflowType = workflowType;
        _authorization = authorization;
        _threadId = threadId;
        
        // Create shared services
        var messageLogger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<Xians.Lib.Agents.Messaging.MessageService>();
        _messageService = new Xians.Lib.Agents.Messaging.MessageService(httpClient, messageLogger);
        
        // Get cache service from KnowledgeActivities static cache
        var cacheService = KnowledgeActivities.GetStaticCacheService();
        var knowledgeLogger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<Xians.Lib.Agents.Knowledge.KnowledgeService>();
        _knowledgeService = new Xians.Lib.Agents.Knowledge.KnowledgeService(httpClient, cacheService, knowledgeLogger);
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
    /// Delegates to shared MessageService.
    /// </summary>
    public override async Task<List<DbMessage>> GetChatHistoryAsync(int page = 1, int pageSize = 10)
    {
        return await _messageService.GetHistoryAsync(
            _workflowType,
            _participantId,
            _scope,
            _tenantId,
            page,
            pageSize);
    }

    /// <summary>
    /// Retrieves knowledge via HTTP instead of workflow activity.
    /// Delegates to shared KnowledgeService.
    /// </summary>
    public override async Task<Xians.Lib.Agents.Knowledge.Models.Knowledge?> GetKnowledgeAsync(string knowledgeName)
    {
        return await _knowledgeService.GetAsync(
            knowledgeName,
            GetAgentNameFromWorkflowType(),
            _tenantId);
    }

    /// <summary>
    /// Updates knowledge via HTTP instead of workflow activity.
    /// Delegates to shared KnowledgeService.
    /// </summary>
    public override async Task<bool> UpdateKnowledgeAsync(string knowledgeName, string content, string? type = null)
    {
        return await _knowledgeService.UpdateAsync(
            knowledgeName,
            content,
            type,
            GetAgentNameFromWorkflowType(),
            _tenantId);
    }

    /// <summary>
    /// Deletes knowledge via HTTP instead of workflow activity.
    /// Delegates to shared KnowledgeService.
    /// </summary>
    public override async Task<bool> DeleteKnowledgeAsync(string knowledgeName)
    {
        return await _knowledgeService.DeleteAsync(
            knowledgeName,
            GetAgentNameFromWorkflowType(),
            _tenantId);
    }

    /// <summary>
    /// Lists knowledge via HTTP instead of workflow activity.
    /// Delegates to shared KnowledgeService.
    /// </summary>
    public override async Task<List<Xians.Lib.Agents.Knowledge.Models.Knowledge>> ListKnowledgeAsync()
    {
        return await _knowledgeService.ListAsync(
            GetAgentNameFromWorkflowType(),
            _tenantId);
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
    /// Delegates to shared MessageService.
    /// </summary>
    private async Task SendHttpMessageAsync(string text, object? data)
    {
        await _messageService.SendAsync(
            _participantId,
            _workflowId,
            _workflowType,
            _requestId,
            _scope,
            text,
            data,
            _tenantId,
            _authorization,
            _threadId,
            _hint,
            origin: null,
            messageType: "chat");
    }
}
