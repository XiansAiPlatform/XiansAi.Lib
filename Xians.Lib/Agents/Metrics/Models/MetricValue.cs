namespace Xians.Lib.Agents.Metrics.Models;

/// <summary>
/// Represents a single metric value with category, type, and unit.
/// </summary>
public class MetricValue
{
    public required string Category { get; set; }
    public required string Type { get; set; }
    public required double Value { get; set; }
    public string Unit { get; set; } = "count";
}
