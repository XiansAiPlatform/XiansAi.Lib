namespace XiansAi.Models;

public class WorkflowRequest
{
    public required string WorkflowType { get; set; }

    public string? WorkflowId { get; set; }

    public string[]? Parameters { get; set; }

    public string? AgentName { get; set; }

    public string? Assignment { get; set; }

    public string? QueueName { get; set; }
}