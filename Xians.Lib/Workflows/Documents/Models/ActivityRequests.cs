using Xians.Lib.Agents.Documents.Models;

namespace Xians.Lib.Workflows.Documents.Models;

/// <summary>
/// Request object for saving a document via activity.
/// </summary>
public class SaveDocumentRequest
{
    public required Document Document { get; set; }
    public DocumentOptions? Options { get; set; }
    public required string TenantId { get; set; }
}

/// <summary>
/// Request object for getting a document via activity.
/// </summary>
public class GetDocumentRequest
{
    public required string Id { get; set; }
    public required string TenantId { get; set; }
}

/// <summary>
/// Request object for querying documents via activity.
/// </summary>
public class QueryDocumentsRequest
{
    public required DocumentQuery Query { get; set; }
    public required string TenantId { get; set; }
}

/// <summary>
/// Request object for updating a document via activity.
/// </summary>
public class UpdateDocumentRequest
{
    public required Document Document { get; set; }
    public required string TenantId { get; set; }
}

/// <summary>
/// Request object for deleting a document via activity.
/// </summary>
public class DeleteDocumentRequest
{
    public required string Id { get; set; }
    public required string TenantId { get; set; }
}

