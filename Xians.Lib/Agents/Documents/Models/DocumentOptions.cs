using System.Text.Json.Serialization;

namespace Xians.Lib.Agents.Documents.Models;

/// <summary>
/// Options for document storage operations.
/// </summary>
public class DocumentOptions
{
    /// <summary>
    /// Time-to-live in minutes. Document will be automatically deleted after this time.
    /// Default is 30 days (43,200 minutes).
    /// Set to null for no expiration.
    /// </summary>
    [JsonPropertyName("ttlMinutes")]
    public int? TtlMinutes { get; set; } = 60 * 24 * 30; // 30 days

    /// <summary>
    /// Whether to overwrite if a document with the same ID exists.
    /// Default is false (will fail if document exists).
    /// </summary>
    [JsonPropertyName("overwrite")]
    public bool Overwrite { get; set; } = false;

    /// <summary>
    /// When true, uses the combination of Type and Key as the unique identifier.
    /// If a document with the same Type and Key exists, it will be updated.
    /// Requires both Type and Key to be set on the document.
    /// Useful for semantic keys like "user-123-preferences".
    /// </summary>
    [JsonPropertyName("useKeyAsIdentifier")]
    public bool UseKeyAsIdentifier { get; set; } = false;
}

