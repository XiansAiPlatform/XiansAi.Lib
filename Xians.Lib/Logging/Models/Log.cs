using Microsoft.Extensions.Logging;

namespace Xians.Lib.Logging.Models;

/// <summary>
/// Represents a log entry that will be sent to the application server for storage.
/// </summary>
public class Log
{
    /// <summary>
    /// Unique identifier for the log entry.
    /// </summary>
    public string? Id { get; set; }
    
    /// <summary>
    /// Timestamp when the log was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// The severity level of the log.
    /// </summary>
    public required LogLevel Level { get; set; }
    
    /// <summary>
    /// The log message content.
    /// </summary>
    public required string Message { get; set; }
    
    /// <summary>
    /// The workflow ID associated with this log entry.
    /// </summary>
    public string? WorkflowId { get; set; }

    /// <summary>
    /// The workflow run ID associated with this log entry.
    /// </summary>
    public string? WorkflowRunId { get; set; }

    /// <summary>
    /// The workflow Type associated with this log entry.
    /// </summary>
    public string? WorkflowType { get; set; }
    
    /// <summary>
    /// The agent name associated with this log entry.
    /// </summary>
    public required string Agent { get; set; }

    /// <summary>
    /// The tenant ID from the current workflow/activity context (XiansContext).
    /// Populated from XiansContext.SafeTenantId when the log is created.
    /// Used for tenant isolation when storing logs; preferred over certificate-derived tenant.
    /// </summary>
    public string? TenantId { get; set; }
    
    /// <summary>
    /// The participant ID (user ID) associated with this log entry.
    /// </summary>
    public string? ParticipantId { get; set; }
    
    /// <summary>
    /// Additional properties for the log entry.
    /// </summary>
    public string? Activation { get; set; }
    
    /// <summary>
    /// Exception details if an exception was logged.
    /// </summary>
    public string? Exception { get; set; }
}
