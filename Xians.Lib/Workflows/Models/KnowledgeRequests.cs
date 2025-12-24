namespace Xians.Lib.Workflows.Models;

/// <summary>
/// Request for getting knowledge from the server.
/// </summary>
public class GetKnowledgeRequest
{
    public required string KnowledgeName { get; set; }
    public required string AgentName { get; set; }
    public required string TenantId { get; set; }
}

/// <summary>
/// Request for updating/creating knowledge on the server.
/// </summary>
public class UpdateKnowledgeRequest
{
    public required string KnowledgeName { get; set; }
    public required string Content { get; set; }
    public string? Type { get; set; }
    public required string AgentName { get; set; }
    public required string TenantId { get; set; }
}

/// <summary>
/// Request for deleting knowledge from the server.
/// </summary>
public class DeleteKnowledgeRequest
{
    public required string KnowledgeName { get; set; }
    public required string AgentName { get; set; }
    public required string TenantId { get; set; }
}

/// <summary>
/// Request for listing all knowledge for an agent.
/// </summary>
public class ListKnowledgeRequest
{
    public required string AgentName { get; set; }
    public required string TenantId { get; set; }
}

