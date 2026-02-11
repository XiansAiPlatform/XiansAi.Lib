namespace Xians.Lib.Temporal.Workflows.Messaging.Models;

/// <summary>
/// Request object for processing messages via activity.
/// </summary>
public class ProcessMessageActivityRequest
{
    public required string MessageText { get; set; }
    public required string ParticipantId { get; set; }
    public required string RequestId { get; set; }
    public string? Scope { get; set; }
    public string? Hint { get; set; }
    public  object? Data { get; set; }
    public required string TenantId { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
    public string? Authorization { get; set; }
    public string? ThreadId { get; set; }
    /// <summary>
    /// Optional metadata for the message.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
    /// <summary>
    /// The type of message: "Chat" or "Data"
    /// </summary>
    public string MessageType { get; set; } = "Chat";
    // Handler is looked up from static registry using WorkflowType - not passed to avoid serialization issues
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
    public string? TaskId { get; set; }
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
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
    public required string ParticipantId { get; set; }
    public string? Scope { get; set; }
    public required string TenantId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// Request object for retrieving the last task ID via activity.
/// </summary>
public class GetLastTaskIdRequest
{
    public required string WorkflowType { get; set; }
    public required string ParticipantId { get; set; }
    public string? Scope { get; set; }
    public required string TenantId { get; set; }
}

/// <summary>
/// Request object for sending handoff messages via activity.
/// </summary>
public class SendHandoffRequest
{
    public string? TargetWorkflowId { get; set; }
    public string? TargetWorkflowType { get; set; }
    public required string SourceAgent { get; set; }
    public required string SourceWorkflowType { get; set; }
    public required string SourceWorkflowId { get; set; }
    public required string ThreadId { get; set; }
    public required string ParticipantId { get; set; }
    public string? Authorization { get; set; }
    public required string Text { get; set; }
    public object? Data { get; set; }
    public required string TenantId { get; set; }
}
