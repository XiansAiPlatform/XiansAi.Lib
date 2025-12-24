namespace Xians.Lib.Agents.Models;

/// <summary>
/// Represents a knowledge item in the Xians platform.
/// Knowledge can include instructions, documents, or other agent-specific content.
/// </summary>
public class Knowledge
{
    /// <summary>
    /// Gets or sets the unique identifier of the knowledge.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the knowledge.
    /// This is the primary identifier for retrieving knowledge.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the version of the knowledge.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets the content of the knowledge.
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Gets or sets the type of the knowledge (e.g., "instruction", "document", "json", "markdown").
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the agent name this knowledge belongs to.
    /// </summary>
    public string? Agent { get; set; }

    /// <summary>
    /// Gets or sets the tenant ID this knowledge belongs to.
    /// </summary>
    public string? TenantId { get; set; }
}

