using Temporalio.Activities;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Documents;
using Xians.Lib.Agents.Documents.Models;
using Xians.Lib.Temporal.Workflows.Documents.Models;

namespace Xians.Lib.Temporal.Workflows.Documents;

/// <summary>
/// Temporal activities for document operations.
/// Activities can perform non-deterministic operations like HTTP calls.
/// Delegates to shared DocumentService to avoid code duplication.
/// </summary>
public class DocumentActivities
{
    private readonly HttpClient _httpClient;
    private readonly DocumentService _documentService;

    public DocumentActivities(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        // Create shared document service
        var logger = Common.Infrastructure.LoggerFactory.CreateLogger<DocumentService>();
        _documentService = new DocumentService(httpClient, logger);
    }

    /// <summary>
    /// Saves a document to the database.
    /// </summary>
    [Activity]
    public async Task<Document> SaveDocumentAsync(SaveDocumentRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "SaveDocument activity started: TenantId={TenantId}",
            request.TenantId);
        
        try
        {
            return await _documentService.SaveAsync(request);
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error saving document: TenantId={TenantId}",
                request.TenantId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves a document by its ID.
    /// </summary>
    [Activity]
    public async Task<Document?> GetDocumentAsync(GetDocumentRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "GetDocument activity started: Id={Id}, TenantId={TenantId}",
            request.Id,
            request.TenantId);
        
        try
        {
            return await _documentService.GetAsync(request);
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error getting document: Id={Id}, TenantId={TenantId}",
                request.Id,
                request.TenantId);
            throw;
        }
    }

    /// <summary>
    /// Queries documents based on filters.
    /// </summary>
    [Activity]
    public async Task<List<Document>> QueryDocumentsAsync(QueryDocumentsRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "QueryDocuments activity started: Type={Type}, TenantId={TenantId}",
            request.Query.Type,
            request.TenantId);
        
        try
        {
            return await _documentService.QueryAsync(request);
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error querying documents: TenantId={TenantId}",
                request.TenantId);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing document.
    /// </summary>
    [Activity]
    public async Task<bool> UpdateDocumentAsync(UpdateDocumentRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "UpdateDocument activity started: Id={Id}, TenantId={TenantId}",
            request.Document.Id,
            request.TenantId);
        
        try
        {
            return await _documentService.UpdateAsync(request);
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error updating document: Id={Id}, TenantId={TenantId}",
                request.Document.Id,
                request.TenantId);
            throw;
        }
    }

    /// <summary>
    /// Deletes a document by its ID.
    /// </summary>
    [Activity]
    public async Task<bool> DeleteDocumentAsync(DeleteDocumentRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "DeleteDocument activity started: Id={Id}, TenantId={TenantId}",
            request.Id,
            request.TenantId);
        
        try
        {
            return await _documentService.DeleteAsync(request);
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error deleting document: Id={Id}, TenantId={TenantId}",
                request.Id,
                request.TenantId);
            throw;
        }
    }
}

