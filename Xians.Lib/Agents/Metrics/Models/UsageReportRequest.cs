namespace Xians.Lib.Agents.Metrics.Models;

/// <summary>
/// Request model for flexible metrics reporting.
/// Supports standard and custom metrics in a scalable array format.
/// </summary>
public class UsageReportRequest
{
    public string? TenantId { get; set; }
    public string? ParticipantId { get; set; }
    public string? WorkflowId { get; set; }
    public string? RequestId { get; set; }
    public string? WorkflowType { get; set; }
    public string? Model { get; set; }
    public string? CustomIdentifier { get; set; }
    public string? AgentName { get; set; }
    public string? ActivationName { get; set; }
    public required List<MetricValue> Metrics { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}
