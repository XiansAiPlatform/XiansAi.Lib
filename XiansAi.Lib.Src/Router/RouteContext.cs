public class RouteContext {
    public required string Agent { get; set; }
    public string? QueueName { get; set; }
    public string? AssignmentId { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
}