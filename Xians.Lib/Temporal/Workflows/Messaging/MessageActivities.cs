using Temporalio.Activities;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Temporal.Workflows.Messaging.Models;
using Xians.Lib.Temporal.Workflows.Models;
using Xians.Lib.Agents.Core;

namespace Xians.Lib.Temporal.Workflows.Messaging;

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
    /// Supports Chat, Data, and Webhook message types.
    /// </summary>
    [Activity]
    public async Task ProcessAndSendMessageAsync(ProcessMessageActivityRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "Message processing started: RequestId={RequestId}, WorkflowType={WorkflowType}, MessageType={MessageType}",
            request.RequestId,
            request.WorkflowType,
            request.MessageType);

        // Set the participant ID for this async execution context
        XiansContext.SetParticipantId(request.ParticipantId);

        // we need to set following also to async execution context request.Authorization, request.RequestId, request.TenantId.
        XiansContext.SetAuthorization(request.Authorization);
        XiansContext.SetRequestId(request.RequestId);
        XiansContext.SetTenantId(request.TenantId);

        try
        {
            var metadata = GetHandlerMetadata(request.WorkflowType);
            var messageType = request.MessageType.ToLower();

            // Handle webhook messages separately
            if (messageType == "webhook")
            {
                await ProcessWebhookAsync(request, metadata);
                return;
            }

            // Get the appropriate handler based on message type (chat or data)
            var handler = messageType == "chat" 
                ? metadata.ChatHandler 
                : metadata.DataHandler;

            if (handler == null)
            {
                throw new InvalidOperationException(
                    $"No {request.MessageType} handler registered for workflow type '{request.WorkflowType}'. " +
                    $"Use OnUser{request.MessageType}Message() to register a handler.");
            }

            // Create a context that sends responses via HTTP instead of collecting them
            var context = new ActivityUserMessageContext(
                _httpClient,
                request.MessageText,
                request.ParticipantId,
                request.RequestId,
                request.Scope,
                request.Hint,
                request.Data,
                request.TenantId,
                request.WorkflowId,
                request.WorkflowType,
                request.Authorization,
                request.ThreadId,
                request.Metadata
            );

            // Invoke the registered handler (which makes agent API calls and sends responses)
            await handler(context);

            ActivityExecutionContext.Current.Logger.LogDebug(
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
        finally
        {
            // Clean up the async local context
            XiansContext.ClearParticipantId();
        }
    }

    /// <summary>
    /// Processes a webhook message by invoking the registered webhook handler.
    /// Returns the WebhookResponse set by the handler.
    /// If an error occurs, sends an error response back to the platform.
    /// </summary>
    private async Task ProcessWebhookAsync(ProcessMessageActivityRequest request, WorkflowHandlerMetadata metadata)
    {
        var webhookHandler = metadata.WebhookHandler;
        if (webhookHandler == null)
        {
            throw new InvalidOperationException(
                $"No webhook handler registered for workflow type '{request.WorkflowType}'. " +
                $"Use OnWebhook() to register a handler.");
        }

        // Convert payload to string - webhook payloads are typically JSON strings
        // Handle JsonElement specially to avoid double-quoting string values
        string? payloadString = null;
        if (request.Data != null)
        {
            if (request.Data is System.Text.Json.JsonElement jsonElement)
            {
                // Extract value based on JSON type to avoid extra quotes on strings
                payloadString = jsonElement.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.String => jsonElement.GetString(),
                    System.Text.Json.JsonValueKind.Null => null,
                    _ => jsonElement.GetRawText() // For objects/arrays, get the raw JSON
                };
            }
            else if (request.Data is string str)
            {
                payloadString = str;
            }
            else
            {
                // Fallback: serialize to JSON for other types
                payloadString = System.Text.Json.JsonSerializer.Serialize(request.Data);
            }
        }

        // Create a webhook context
        var context = new ActivityWebhookContext(
            _httpClient,
            request.ParticipantId,
            request.Scope,
            request.MessageText,  // Name is transferred in Text field
            payloadString,        // Payload is transferred in Data field as string
            request.Authorization,
            request.RequestId,
            request.TenantId,
            request.WorkflowId,
            request.WorkflowType,
            request.Metadata
        );

        try
        {
            // Invoke the registered webhook handler
            await webhookHandler(context);

            ActivityExecutionContext.Current.Logger.LogDebug(
                "Webhook handler completed: RequestId={RequestId}, WebhookName={WebhookName}, StatusCode={StatusCode}",
                request.RequestId,
                request.MessageText,
                context.Response.StatusCode);
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error in webhook handler: RequestId={RequestId}, WebhookName={WebhookName}, Error={Error}",
                request.RequestId,
                request.MessageText,
                ex.Message);

            // Set error response
            context.Response = WebhookResponse.InternalServerError(
                $"Webhook handler error: {ex.Message}");
        }

        // Always send the webhook response back to the platform (success or error)
        await context.SendWebhookResponseAsync();

        ActivityExecutionContext.Current.Logger.LogDebug(
            "Webhook processed and response sent: RequestId={RequestId}, WebhookName={WebhookName}, StatusCode={StatusCode}",
            request.RequestId,
            request.MessageText,
            context.Response.StatusCode);
    }

    /// <summary>
    /// Processes an A2A (Agent-to-Agent) message by invoking the handler and capturing the response.
    /// Unlike ProcessAndSendMessageAsync, this returns the response instead of sending it via HTTP.
    /// Delegates to shared A2AService to avoid code duplication.
    /// </summary>
    [Activity]
    public async Task<Xians.Lib.Agents.A2A.A2AActivityResponse> ProcessA2AMessageAsync(ProcessMessageActivityRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "ProcessA2AMessage activity started: RequestId={RequestId}, WorkflowType={WorkflowType}, MessageType={MessageType}",
            request.RequestId,
            request.WorkflowType,
            request.MessageType);

        try
        {
            // Delegate to shared A2AService for consistent handler invocation
            var a2aService = new Xians.Lib.Agents.A2A.A2AService(request.WorkflowType);
            var response = await a2aService.ProcessDirectAsync(request);

            return response;
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error processing A2A message: RequestId={RequestId}",
                request.RequestId);
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
            return await _messageService.GetHistoryAsync(request);
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
    /// Retrieves the last task ID for a conversation from the server.
    /// For system-scoped agents, uses tenant ID from workflow context.
    /// Delegates to shared MessageService.
    /// </summary>
    [Activity]
    public async Task<string?> GetLastTaskIdAsync(GetLastTaskIdRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "GetLastTaskId activity started: WorkflowType={WorkflowType}, ParticipantId={ParticipantId}",
            request.WorkflowId,
            request.ParticipantId);
        
        try
        {
            return await _messageService.GetLastTaskIdAsync(request);
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error fetching last task ID for WorkflowType={WorkflowType}",
                request.WorkflowId);
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
            await _messageService.SendAsync(request);

            ActivityExecutionContext.Current.Logger.LogDebug(
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

    /// <summary>
    /// Sends a handoff request to transfer a conversation to another agent/workflow.
    /// Delegates to shared MessageService.
    /// </summary>
    [Activity]
    public async Task<string?> SendHandoffAsync(SendHandoffRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "SendHandoff activity started: TargetWorkflowId={TargetWorkflowId}, TargetWorkflowType={TargetWorkflowType}",
            request.TargetWorkflowId,
            request.TargetWorkflowType);
        
        try
        {
            var result = await _messageService.SendHandoffAsync(request);

            ActivityExecutionContext.Current.Logger.LogDebug(
                "Handoff sent successfully: TargetWorkflowId={TargetWorkflowId}, TargetWorkflowType={TargetWorkflowType}",
                request.TargetWorkflowId,
                request.TargetWorkflowType);

            return result;
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error sending handoff: TargetWorkflowId={TargetWorkflowId}, TargetWorkflowType={TargetWorkflowType}",
                request.TargetWorkflowId,
                request.TargetWorkflowType);
            throw;
        }
    }

    /// <summary>
    /// Looks up the handler metadata from the registry.
    /// Throws InvalidOperationException if not found.
    /// </summary>
    private WorkflowHandlerMetadata GetHandlerMetadata(string workflowType)
    {
        if (!BuiltinWorkflow._handlersByWorkflowType.TryGetValue(workflowType, out var metadata))
        {
            var errorMessage = $"No message handler registered for workflow type '{workflowType}' in activity.";
            ActivityExecutionContext.Current.Logger.LogError(
                "Handler lookup failed: WorkflowType={WorkflowType}",
                workflowType);
            
            throw new InvalidOperationException(errorMessage);
        }

        return metadata;
    }
}

/// <summary>
/// Activity-safe version of UserMessageContext that sends responses via HTTP
/// instead of executing workflow activities.
/// </summary>
public class ActivityUserMessageContext : UserMessageContext
{
    private readonly MessageService _messageService;
    // private readonly KnowledgeService _knowledgeService;
    private readonly string _workflowId;
    private readonly string _workflowType;
    private readonly string _participantId;
    private readonly string _requestId;
    private readonly string? _scope;
    private readonly string? _threadId;
    private readonly string? _authorization;
    private readonly string? _hint;
    private readonly string _tenantId;

    public ActivityUserMessageContext(
        HttpClient httpClient,
        string text,
        string participantId,
        string requestId,
        string? scope,
        string? hint,
        object? data,
        string tenantId,
        string workflowId,
        string workflowType,
        string? authorization = null,
        string? threadId = null,
        Dictionary<string, string>? metadata = null)
        : base(text, participantId, requestId, scope, hint, data, tenantId, authorization, threadId, metadata)
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
        
    }

    /// <summary>
    /// Sends response via HTTP instead of workflow activity.
    /// </summary>
    public override async Task ReplyAsync(string response)
    {
        await SendHttpMessageAsync(response, null);
    }


    /// <summary>
    /// Retrieves chat history via HTTP instead of workflow activity.
    /// For system-scoped agents, uses tenant ID from workflow context.
    /// Delegates to shared MessageService.
    /// </summary>
    public override async Task<List<DbMessage>> GetChatHistoryAsync(int page = 1, int pageSize = 10)
    {
        var request = new GetMessageHistoryRequest
        {
            WorkflowId = _workflowId,
            WorkflowType = _workflowType,
            ParticipantId = _participantId,
            Scope = _scope,
            TenantId = _tenantId,
            Page = page,
            PageSize = pageSize
        };
        return await _messageService.GetHistoryAsync(request);
    }

    /// <summary>
    /// Retrieves the last task ID via HTTP instead of workflow activity.
    /// For system-scoped agents, uses tenant ID from workflow context.
    /// Delegates to shared MessageService.
    /// </summary>
    public override async Task<string?> GetLastTaskIdAsync()
    {
        var request = new GetLastTaskIdRequest
        {
            WorkflowId = _workflowId,
            ParticipantId = _participantId,
            Scope = _scope,
            TenantId = _tenantId
        };
        return await _messageService.GetLastTaskIdAsync(request);
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
        var request = new SendMessageRequest
        {
            ParticipantId = _participantId,
            WorkflowId = _workflowId,
            WorkflowType = _workflowType,
            RequestId = _requestId,
            Scope = _scope,
            Text = text,
            Data = data,
            TenantId = _tenantId,
            Authorization = _authorization,
            ThreadId = _threadId,
            Hint = _hint,
            Origin = null,
            Type = "chat"
        };
        await _messageService.SendAsync(request);
    }
}

/// <summary>
/// Activity-safe version of WebhookContext that sends responses via HTTP
/// instead of executing workflow activities.
/// </summary>
public class ActivityWebhookContext : WebhookContext
{
    private readonly Xians.Lib.Agents.Messaging.MessageService _messageService;
    private readonly string _workflowId;
    private readonly string _workflowType;
    private readonly string _participantId;
    private readonly string _requestId;
    private readonly string? _scope;
    private readonly string? _authorization;
    private readonly string _tenantId;

    public ActivityWebhookContext(
        HttpClient httpClient,
        string participantId,
        string? scope,
        string name,
        string? payload,
        string? authorization,
        string requestId,
        string tenantId,
        string workflowId,
        string workflowType,
        Dictionary<string, string>? metadata = null)
        : base(participantId, scope, name, payload, authorization, requestId, tenantId, metadata)
    {
        _participantId = participantId;
        _scope = scope;
        _authorization = authorization;
        _requestId = requestId;
        _tenantId = tenantId;
        _workflowId = workflowId;
        _workflowType = workflowType;
        
        // Create shared message service
        var messageLogger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<Xians.Lib.Agents.Messaging.MessageService>();
        _messageService = new Xians.Lib.Agents.Messaging.MessageService(httpClient, messageLogger);
    }

    /// <summary>
    /// Sends the webhook response back to the platform via HTTP.
    /// The response is sent as a Webhook message type with the WebhookResponse in the Data field.
    /// Called after the handler has finished executing.
    /// </summary>
    internal async Task SendWebhookResponseAsync()
    {
        // Build the response data object with HTTP-style response properties
        // Ensure Headers is never null to avoid server-side deserialization issues
        var responseData = new
        {
            StatusCode = (int)Response.StatusCode,
            Content = Response.Content ?? string.Empty,
            ContentType = Response.ContentType ?? "application/json",
            Headers = Response.Headers ?? new Dictionary<string, string[]>()
        };

        var request = new SendMessageRequest
        {
            ParticipantId = _participantId,
            WorkflowId = _workflowId,
            WorkflowType = _workflowType,
            RequestId = _requestId,
            Scope = _scope,
            Text = string.Empty,
            Data = responseData,  // WebhookResponse goes in the Data field
            TenantId = _tenantId,
            Authorization = _authorization,
            ThreadId = null,
            Hint = null,
            Origin = null,
            Type = "Webhook"  // Use Webhook message type
        };
        await _messageService.SendAsync(request);
    }
}
