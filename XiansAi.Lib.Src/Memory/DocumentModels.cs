using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agentri.Memory;

/// <summary>
/// Represents a document stored in the database with metadata.
/// </summary>
/// <typeparam name="T">The type of the document content.</typeparam>
public class Document
{
    /// <summary>
    /// Unique identifier for the document. Auto-generated if not provided.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Custom key that, combined with Type, creates a unique identifier for the document.
    /// Optional - if not provided, only Id will be used for identification.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// The actual content of the document.
    /// </summary>
    public JsonElement? Content { get; set; }

    /// <summary>
    /// Optional metadata for categorization and querying.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// The agent that created or owns this document.
    /// </summary>
    public string? AgentId { get; } = AgentContext.AgentName;

    /// <summary>
    /// The workflow instance that created this document.
    /// </summary>
    public string? WorkflowId { get; } = AgentContext.WorkflowId;

    /// <summary>
    /// Document type for categorization (e.g., "memory", "context", "knowledge").
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Optional expiration time for automatic cleanup.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// The user that created this document.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// The user that updated this document.
    /// </summary>
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Options for document storage operations.
/// </summary>
public class DocumentOptions
{
    /// <summary>
    /// Time-to-live in minutes. Document will be automatically deleted after this time. Default is 30 days.
    /// </summary>
    public int? TtlMinutes { get; set; } = 60 * 24 * 30; // 30 days

    /// <summary>
    /// Whether to overwrite if a document with the same ID exists.
    /// </summary>
    public bool Overwrite { get; set; } = false;

    /// <summary>
    /// When true, uses the combination of Type and Key as the unique identifier.
    /// If a document with the same Type and Key exists, it will be updated.
    /// Requires both Type and Key to be set on the document.
    /// </summary>
    public bool UseKeyAsIdentifier { get; set; } = false;
}

/// <summary>
/// Query parameters for searching documents.
/// </summary>
public class DocumentQuery
{
    /// <summary>
    /// Filter by document type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Filter by document key.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Filter by metadata key-value pairs.
    /// </summary>
    public Dictionary<string, object>? MetadataFilters { get; set; }

    /// <summary>
    /// Maximum number of results to return.
    /// </summary>
    public int? Limit { get; set; } = 100;

    /// <summary>
    /// Number of results to skip for pagination.
    /// </summary>
    public int? Skip { get; set; } = 0;

    /// <summary>
    /// Sort field (e.g., "CreatedAt", "UpdatedAt").
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort direction.
    /// </summary>
    public bool SortDescending { get; set; } = true;

    /// <summary>
    /// Include only documents created after this date.
    /// </summary>
    public DateTime? CreatedAfter { get; set; }

    /// <summary>
    /// Include only documents created before this date.
    /// </summary>
    public DateTime? CreatedBefore { get; set; }
}

/// <summary>
/// Request models for API communication.
/// </summary>
internal class DocumentRequest
{
    public required Document Document { get; set; }
    public DocumentOptions? Options { get; set; }
}

internal class DocumentIdRequest
{
    public required string Id { get; set; }
}

internal class DocumentKeyRequest
{
    public required string Type { get; set; }
    public required string Key { get; set; }
}

internal class DocumentIdsRequest
{
    public required IEnumerable<string> Ids { get; set; }
}

internal class DocumentQueryRequest
{
    public required DocumentQuery Query { get; set; }
    public string? ContentType { get; } = "JsonElement";
}
