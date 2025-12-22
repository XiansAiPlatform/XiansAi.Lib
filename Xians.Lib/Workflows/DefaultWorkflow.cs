using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Temporalio.Converters;
using Xians.Lib.Agents;

namespace Xians.Lib.Workflows;

/// <summary>
/// Default workflow that handles user chat messages via temporal signals.
/// Simplified design that receives messages via signals and delegates to user-defined handlers.
/// </summary>
[Workflow(Dynamic = true)]
public class DefaultWorkflow
{
    private readonly Queue<InboundMessage> _messageQueue = new();
    
    // Metadata for each registered workflow handler including tenant isolation info
    // Made internal static to allow activities to access it
    internal static readonly Dictionary<string, WorkflowHandlerMetadata> _handlersByWorkflowType = new();

    /// <summary>
    /// Main workflow execution method
    /// </summary>
    [WorkflowRun]
    public async Task RunAsync(IRawValue[] args)
    {
        // Start the message processing loop
        await ProcessMessagesLoopAsync();
    }

    /// <summary>
    /// Signal handler that receives chat or data messages from temporal.
    /// This matches the signal name used by the server.
    /// Returns Task.CompletedTask to satisfy Temporal's async requirement while keeping operation synchronous.
    /// </summary>
    [WorkflowSignal("HandleInboundChatOrData")]
    public Task HandleInboundChatOrData(InboundMessage message)
    {
        Workflow.Logger.LogDebug(
            "Signal received: Type={Type}, ParticipantId={ParticipantId}, RequestId={RequestId}, QueueDepth={QueueDepth}",
            message.Payload.Type,
            message.Payload.ParticipantId,
            message.Payload.RequestId,
            _messageQueue.Count);
        
        _messageQueue.Enqueue(message);
        
        Workflow.Logger.LogDebug(
            "Message enqueued: QueueDepth={QueueDepth}",
            _messageQueue.Count);
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Registers a user message handler for a specific workflow type with tenant isolation metadata.
    /// </summary>
    /// <param name="workflowType">The unique workflow type identifier.</param>
    /// <param name="handler">The handler function to register.</param>
    /// <param name="agentName">The agent name for validation.</param>
    /// <param name="tenantId">The tenant ID (null for system-scoped agents).</param>
    /// <param name="systemScoped">Whether this is a system-scoped agent.</param>
    public static void RegisterMessageHandler(
        string workflowType, 
        Func<UserMessageContext, Task> handler,
        string agentName,
        string? tenantId,
        bool systemScoped)
    {
        lock (_handlersByWorkflowType)
        {
            _handlersByWorkflowType[workflowType] = new WorkflowHandlerMetadata
            {
                Handler = handler,
                AgentName = agentName.Trim(), // Trim to handle whitespace variations
                TenantId = tenantId,
                SystemScoped = systemScoped
            };
        }
    }

    /// <summary>
    /// Main message processing loop - waits for signals and processes them.
    /// This is the top-level event loop where exceptions are caught to prevent workflow crashes.
    /// </summary>
    private async Task ProcessMessagesLoopAsync()
    {
        Workflow.Logger.LogInformation("Message processing loop started");
        
        while (true)
        {
            // Wait for a message to arrive in the queue
            Workflow.Logger.LogDebug("Waiting for messages... QueueDepth={QueueDepth}", _messageQueue.Count);
            await Workflow.WaitConditionAsync(() => _messageQueue.Count > 0);

            // Dequeue and process the message
            if (_messageQueue.TryDequeue(out var message))
            {
                Workflow.Logger.LogDebug(
                    "Dequeued message: Type={Type}, ParticipantId={ParticipantId}, RequestId={RequestId}, RemainingInQueue={RemainingInQueue}",
                    message.Payload.Type,
                    message.Payload.ParticipantId,
                    message.Payload.RequestId,
                    _messageQueue.Count);
                
                // Process message in background to avoid blocking the loop
                _ = Workflow.RunTaskAsync(async () =>
                {
                    try
                    {
                        Workflow.Logger.LogDebug(
                            "Starting message processing: RequestId={RequestId}",
                            message.Payload.RequestId);
                        
                        await ProcessMessageAsync(message);
                        
                        Workflow.Logger.LogDebug(
                            "Message processing completed: RequestId={RequestId}",
                            message.Payload.RequestId);
                    }
                    catch (Exception ex)
                    {
                        // Top-level exception handler - safe to catch here to prevent workflow crash
                        Workflow.Logger.LogError(ex, 
                            "Error processing message from {ParticipantId}: {ErrorMessage}", 
                            message.Payload.ParticipantId, 
                            ex.Message);
                        
                        // Attempt to send error response to user
                        // If this fails, we log but don't rethrow as we're already in error state
                        try
                        {
                            Workflow.Logger.LogDebug(
                                "Attempting to send error response: RequestId={RequestId}",
                                message.Payload.RequestId);
                            
                            await SendErrorResponseAsync(message, ex.Message);
                            
                            Workflow.Logger.LogDebug(
                                "Error response sent: RequestId={RequestId}",
                                message.Payload.RequestId);
                        }
                        catch (Exception errorEx)
                        {
                            Workflow.Logger.LogError(errorEx, 
                                "Failed to send error response to {ParticipantId}", 
                                message.Payload.ParticipantId);
                        }
                    }
                });
            }
        }
    }

    /// <summary>
    /// Processes a single incoming message
    /// </summary>
    private async Task ProcessMessageAsync(InboundMessage message)
    {
        Workflow.Logger.LogDebug(
            "ProcessMessageAsync: Type={Type}, Text={TextPreview}",
            message.Payload.Type,
            message.Payload.Text.Length > 50 
                ? message.Payload.Text.Substring(0, 50) + "..." 
                : message.Payload.Text);
        
        // Only process Chat type messages (skip Data and Handoff for now)
        if (message.Payload.Type.ToLower() != "chat")
        {
            Workflow.Logger.LogWarning(
                "Skipping non-chat message: Type={Type}, RequestId={RequestId}",
                message.Payload.Type,
                message.Payload.RequestId);
            return;
        }

        // Get workflow information first - needed for tenant context
        var workflowType = Workflow.Info.WorkflowType;
        var workflowId = Workflow.Info.WorkflowId;
        var taskQueue = Workflow.Info.TaskQueue;
        
        // Extract tenant ID from WorkflowId (format: "TenantId:WorkflowType...")
        // This works for BOTH system-scoped and non-system-scoped agents
        var workflowIdParts = workflowId.Split(':');
        if (workflowIdParts.Length < 2)
        {
            Workflow.Logger.LogError(
                "Invalid WorkflowId format. Expected 'TenantId:WorkflowType...', got '{WorkflowId}'",
                workflowId);
            return;
        }
        var workflowTenantId = workflowIdParts[0];
        
        Workflow.Logger.LogDebug(
            "Processing message: ParticipantId={ParticipantId}, Scope={Scope}, Hint={Hint}, Tenant={Tenant}",
            message.Payload.ParticipantId,
            message.Payload.Scope,
            message.Payload.Hint,
            workflowTenantId);
        
        // Lookup handler metadata for this specific workflow type
        WorkflowHandlerMetadata? metadata;
        lock (_handlersByWorkflowType)
        {
            _handlersByWorkflowType.TryGetValue(workflowType, out metadata);
        }
        
        if (metadata == null)
        {
            Workflow.Logger.LogWarning(
                "No message handler registered for WorkflowType={WorkflowType}, RequestId={RequestId}",
                workflowType,
                message.Payload.RequestId);
            
            await SendSimpleMessageAsync(
                message.Payload.ParticipantId,
                $"No message handler registered for workflow type '{workflowType}'.",
                message.Payload.RequestId,
                message.Payload.Scope,
                message.Payload.ThreadId,
                message.Payload.Authorization,
                message.Payload.Hint,
                workflowTenantId,
                workflowId,
                workflowType);
            return;
        }
        
        // Tenant isolation validation differs for system-scoped vs non-system-scoped
        if (metadata.SystemScoped)
        {
            // System-scoped agents can handle multiple tenants
            // Extract tenant from WorkflowId and log, but DON'T validate against registered tenant
            Workflow.Logger.LogDebug(
                "System-scoped workflow processing message: WorkflowTenant={WorkflowTenant}, WorkflowType={WorkflowType}",
                workflowTenantId,
                workflowType);
        }
        else
        {
            // Non-system-scoped agents must validate tenant isolation
            // The workflow's tenant (from WorkflowId) MUST match the agent's registered tenant
            if (metadata.TenantId != workflowTenantId)
            {
                Workflow.Logger.LogError(
                    "Tenant isolation violation: WorkflowType={WorkflowType}, RegisteredTenant={RegisteredTenant}, WorkflowTenant={WorkflowTenant}, RequestId={RequestId}",
                    workflowType,
                    metadata.TenantId,
                    workflowTenantId,
                    message.Payload.RequestId);
                await SendSimpleMessageAsync(
                    message.Payload.ParticipantId,
                    "Error: Tenant isolation violation.",
                    message.Payload.RequestId,
                    message.Payload.Scope,
                    message.Payload.ThreadId,
                    message.Payload.Authorization,
                    message.Payload.Hint,
                    workflowTenantId,
                    workflowId,
                    workflowType);
                return;
            }
            
            Workflow.Logger.LogDebug(
                "Tenant validation passed: TenantId={TenantId}, WorkflowType={WorkflowType}",
                workflowTenantId,
                workflowType);
        }
        
        // Validate agent name matches (applies to both system-scoped and non-system-scoped)
        // Trim both values to handle whitespace differences
        if (metadata.AgentName.Trim() != message.Payload.Agent?.Trim())
        {
            Workflow.Logger.LogWarning(
                "Agent name mismatch: Expected={ExpectedAgent}, Received={ReceivedAgent}, RequestId={RequestId}",
                metadata.AgentName,
                message.Payload.Agent,
                message.Payload.RequestId);
            await SendSimpleMessageAsync(
                message.Payload.ParticipantId,
                $"Error: Message intended for agent '{message.Payload.Agent?.Trim()}' but received by '{metadata.AgentName.Trim()}'.",
                message.Payload.RequestId,
                message.Payload.Scope,
                message.Payload.ThreadId,
                message.Payload.Authorization,
                message.Payload.Hint,
                workflowTenantId,
                workflowId,
                workflowType);
            return;
        }
        
        // All validations passed - process message and send responses via single activity
        Workflow.Logger.LogInformation(
            "Processing message via activity: WorkflowType={WorkflowType}, Agent={Agent}, Tenant={Tenant}, SystemScoped={SystemScoped}",
            workflowType,
            metadata.AgentName,
            workflowTenantId,
            metadata.SystemScoped);
        
        // Execute handler in activity - encapsulates agent API calls and sending responses
        var activityRequest = new ProcessMessageActivityRequest
        {
            MessageText = message.Payload.Text,
            ParticipantId = message.Payload.ParticipantId,
            RequestId = message.Payload.RequestId,
            Scope = message.Payload.Scope,
            Hint = message.Payload.Hint,
            Data = message.Payload.Data,
            TenantId = workflowTenantId,
            WorkflowId = workflowId,
            WorkflowType = workflowType,
            Authorization = message.Payload.Authorization,
            ThreadId = message.Payload.ThreadId
            // Handler is looked up in the activity from static registry
        };

        await Workflow.ExecuteActivityAsync(
            (MessageActivities act) => act.ProcessAndSendMessageAsync(activityRequest),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromMinutes(5), // Allow time for agent API calls and responses
                RetryPolicy = new()
                {
                    MaximumAttempts = 3,
                    InitialInterval = TimeSpan.FromSeconds(2),
                    MaximumInterval = TimeSpan.FromSeconds(30),
                    BackoffCoefficient = 2
                }
            });

        Workflow.Logger.LogInformation(
            "Message processed and responses sent: RequestId={RequestId}",
            message.Payload.RequestId);
    }

    /// <summary>
    /// Sends an error response back to the user via activity.
    /// </summary>
    private async Task SendErrorResponseAsync(InboundMessage message, string errorMessage)
    {
        // Extract tenant ID from WorkflowId
        var workflowId = Workflow.Info.WorkflowId;
        var workflowIdParts = workflowId.Split(':');
        var workflowTenantId = workflowIdParts.Length >= 2 ? workflowIdParts[0] : "unknown";
        
        await SendSimpleMessageAsync(
            message.Payload.ParticipantId,
            $"Error: {errorMessage}",
            message.Payload.RequestId,
            message.Payload.Scope,
            message.Payload.ThreadId,
            message.Payload.Authorization,
            message.Payload.Hint,
            workflowTenantId,
            workflowId,
            Workflow.Info.WorkflowType);
    }

    /// <summary>
    /// Helper method to send a simple text message via activity.
    /// </summary>
    private async Task SendSimpleMessageAsync(
        string participantId,
        string text,
        string requestId,
        string scope,
        string? threadId,
        string? authorization,
        string hint,
        string tenantId,
        string workflowId,
        string workflowType)
    {
        var request = new SendMessageRequest
        {
            ParticipantId = participantId,
            WorkflowId = workflowId,
            WorkflowType = workflowType,
            Text = text,
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

        await Workflow.ExecuteActivityAsync(
            (MessageActivities act) => act.SendMessageAsync(request),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(30),
                RetryPolicy = new()
                {
                    MaximumAttempts = 3,
                    InitialInterval = TimeSpan.FromSeconds(1),
                    MaximumInterval = TimeSpan.FromSeconds(10),
                    BackoffCoefficient = 2
                }
            });
    }
}

/// <summary>
/// Represents an inbound message signal from the Xians platform.
/// Matches the structure sent by the server.
/// </summary>
public class InboundMessage
{
    public required InboundMessagePayload Payload { get; set; }
    public required string SourceAgent { get; set; }
    public required string SourceWorkflowId { get; set; }
    public required string SourceWorkflowType { get; set; }
}

/// <summary>
/// Payload of an inbound message.
/// Must match MessagePayload structure from XiansAi.Lib.Src/Messenging/Models.cs
/// </summary>
public class InboundMessagePayload
{
    public required string Agent { get; set; }
    public required string ThreadId { get; set; }
    public required string ParticipantId { get; set; }
    public string? Authorization { get; set; }
    public required string Text { get; set; }
    public required string RequestId { get; set; }
    public required string Hint { get; set; }
    public required string Scope { get; set; }
    public required object Data { get; set; }
    public required string Type { get; set; }
    public List<DbMessage>? History { get; set; }
}

/// <summary>
/// Database message structure for conversation history.
/// Matches DbMessage from XiansAi.Lib.Src/Messenging/Models.cs
/// </summary>
public class DbMessage
{
    public string Id { get; set; } = null!;
    public required string ThreadId { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public required string Direction { get; set; }
    public string? Text { get; set; }
    public string? Status { get; set; }
    public object? Data { get; set; }
    public required string ParticipantId { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
}

/// <summary>
/// Metadata for a registered workflow handler including tenant isolation information.
/// Made internal to allow activities to access handler registry.
/// </summary>
internal class WorkflowHandlerMetadata
{
    public required Func<UserMessageContext, Task> Handler { get; set; }
    public required string AgentName { get; set; }
    public string? TenantId { get; set; }
    public required bool SystemScoped { get; set; }
}