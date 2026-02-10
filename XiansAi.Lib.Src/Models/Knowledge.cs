namespace XiansAi.Models;

public class Knowledge
{
    public string? Id { get; set; }
    public required string Name { get; set; }
    public string? Version { get; set; }
    public required string Content { get; set; }
    public string? Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Agent { get; set; }
    public string? TenantId { get; set; }

    /// <summary>
    /// Optional description of the knowledge item.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether the knowledge item is visible. Defaults to true.
    /// </summary>
    public bool Visible { get; set; } = true;
}
