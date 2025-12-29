using System.Text.Json;

namespace Xians.Lib.Agents.Documents.Models;

/// <summary>
/// Represents a document stored in the agent's document database.
/// Documents are scoped to the agent and can store any JSON-serializable content.
/// </summary>
public class Document
{
    /// <summary>
    /// Unique identifier for the document. Auto-generated if not provided.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Custom key that, combined with Type, creates a unique identifier.
    /// Optional - useful for semantic identifiers like "user-preferences" or "session-state".
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// The actual content of the document as a JSON element.
    /// Can represent any JSON-serializable object.
    /// </summary>
    public JsonElement? Content { get; set; }

    /// <summary>
    /// Optional metadata for categorization and querying.
    /// Use this for filterable properties that aren't part of the main content.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Document type for categorization (e.g., "memory", "context", "user-data").
    /// Useful for organizing different kinds of documents.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Optional expiration time for automatic cleanup (UTC).
    /// Documents will be automatically deleted after this time.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// The agent that owns this document.
    /// Automatically populated by the platform.
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// The workflow instance that created this document.
    /// Automatically populated by the platform.
    /// </summary>
    public string? WorkflowId { get; set; }

    /// <summary>
    /// The user that created this document.
    /// Automatically populated from workflow context.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// The user that last updated this document.
    /// Automatically populated from workflow context.
    /// </summary>
    public string? UpdatedBy { get; set; }
}

