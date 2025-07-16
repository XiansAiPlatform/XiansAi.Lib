using System.Text.Json;

public class RpcContext
{
    public required string ParticipantId { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
    public required string Agent { get; set; }
    public required string Authorization { get; set; }
    public string? Metadata { get; set; }
    public required string TenantId { get; set; }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }
}