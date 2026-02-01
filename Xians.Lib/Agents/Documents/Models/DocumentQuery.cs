namespace Xians.Lib.Agents.Documents.Models;

/// <summary>
/// Query parameters for searching documents.
/// All filters are combined with AND logic.
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
    /// Filter by agent ID.
    /// Automatically set by the SDK to scope documents to the current agent.
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// Filter by activation name.
    /// Automatically set when available from workflow context.
    /// </summary>
    public string? ActivationName { get; set; }

    /// <summary>
    /// Filter by participant ID.
    /// Automatically set when available from workflow context.
    /// </summary>
    public string? ParticipantId { get; set; }

    /// <summary>
    /// Filter by metadata key-value pairs.
    /// All metadata filters must match for a document to be included.
    /// </summary>
    public Dictionary<string, object>? MetadataFilters { get; set; }

    /// <summary>
    /// Maximum number of results to return.
    /// Default is 100.
    /// </summary>
    public int? Limit { get; set; } = 100;

    /// <summary>
    /// Number of results to skip for pagination.
    /// Default is 0.
    /// </summary>
    public int? Skip { get; set; } = 0;

    /// <summary>
    /// Sort field (e.g., "CreatedAt", "UpdatedAt").
    /// Default is "CreatedAt".
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort direction.
    /// Default is true (descending - newest first).
    /// </summary>
    public bool SortDescending { get; set; } = true;

    /// <summary>
    /// Include only documents created after this date (UTC).
    /// </summary>
    public DateTime? CreatedAfter { get; set; }

    /// <summary>
    /// Include only documents created before this date (UTC).
    /// </summary>
    public DateTime? CreatedBefore { get; set; }
}

