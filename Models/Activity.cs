public class Activity
{
    public required string ActivityId { get; set; }
    public required string ActivityName { get; set; }
    public required DateTime StartedTime { get; set; }
    public DateTime? EndedTime { get; set; }
    public Dictionary<string, object?> Inputs { get; set; } = [];
    public object? Result { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
    public required string TaskQueue { get; set; }
}