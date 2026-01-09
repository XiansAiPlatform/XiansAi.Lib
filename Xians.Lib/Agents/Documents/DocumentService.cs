using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Documents.Models;
using Xians.Lib.Common;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Temporal.Workflows.Documents.Models;

namespace Xians.Lib.Agents.Documents;

/// <summary>
/// Core service for document storage operations.
/// Shared by DocumentCollection and activities to avoid code duplication.
/// </summary>
internal class DocumentService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private static string BaseEndpoint => WorkflowConstants.ApiEndpoints.Documents;

    public DocumentService(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Saves a document to the database.
    /// </summary>
    /// <param name="request">The save document request containing document, tenant ID, and options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The saved document with its assigned ID.</returns>
    public async Task<Document> SaveAsync(SaveDocumentRequest request, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateNotNull(request, nameof(request));
        ValidationHelper.ValidateNotNull(request.Document, nameof(request.Document));
        ValidationHelper.ValidateRequired(request.TenantId, nameof(request.TenantId));
        
        _logger.LogTrace("Saving document{Id}", request.Document.Id != null ? $" with ID: {request.Document.Id}" : "");

        // Validate UseKeyAsIdentifier requirements
        if (request.Options?.UseKeyAsIdentifier == true)
        {
            var missingFields = new List<string>();
            if (string.IsNullOrEmpty(request.Document.Type)) missingFields.Add("Type");
            if (string.IsNullOrEmpty(request.Document.Key)) missingFields.Add("Key");
            
            if (missingFields.Any())
            {
                var message = $"UseKeyAsIdentifier requires both Type and Key properties. Missing: {string.Join(", ", missingFields)}";
                _logger.LogError("Document save validation failed: {Message}", message);
                throw new ArgumentException(message);
            }
        }

        var documentRequest = new DocumentRequest 
        { 
            Document = request.Document,
            Options = request.Options
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseEndpoint}/save");
        httpRequest.Content = JsonContent.Create(documentRequest);
        httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, request.TenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Document save failed: StatusCode={StatusCode}, Error={Error}",
                response.StatusCode,
                error);
            throw new HttpRequestException(
                $"Failed to save document. Status: {response.StatusCode}");
        }

        var savedDocument = await response.Content.ReadFromJsonAsync<Document>();
        if (savedDocument == null)
        {
            throw new InvalidOperationException("Failed to deserialize saved document");
        }

        _logger.LogInformation("Document saved successfully with ID: {Id}", savedDocument.Id);
        return savedDocument;
    }

    /// <summary>
    /// Retrieves a document by its ID.
    /// </summary>
    /// <param name="request">The get document request containing document ID and tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document if found, null otherwise.</returns>
    public async Task<Document?> GetAsync(GetDocumentRequest request, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateNotNull(request, nameof(request));
        ValidationHelper.ValidateRequired(request.Id, nameof(request.Id));
        ValidationHelper.ValidateRequired(request.TenantId, nameof(request.TenantId));
        
        _logger.LogTrace("Getting document with ID: {Id}", request.Id);

        var documentIdRequest = new DocumentIdRequest { Id = request.Id };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseEndpoint}/get");
        httpRequest.Content = JsonContent.Create(documentIdRequest);
        httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, request.TenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogTrace("Document not found with ID: {Id}", request.Id);
            return null;
        }
        
        // Handle InternalServerError as "not found" (server may return 500 for non-existent documents)
        if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
        {
            _logger.LogTrace("Document not found (500) with ID: {Id}", request.Id);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Document get failed: StatusCode={StatusCode}, Error={Error}",
                response.StatusCode,
                error);
            throw new HttpRequestException(
                $"Failed to get document. Status: {response.StatusCode}");
        }
        
        var document = await response.Content.ReadFromJsonAsync<Document>();
        _logger.LogInformation("Document retrieved successfully with ID: {Id}", request.Id);
        return document;
    }

    /// <summary>
    /// Retrieves a document by its type and key combination.
    /// </summary>
    /// <param name="type">The document type.</param>
    /// <param name="key">The document key.</param>
    /// <param name="tenantId">The tenant ID for isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document if found, null otherwise.</returns>
    public async Task<Document?> GetByKeyAsync(string type, string key, string tenantId, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateRequired(type, nameof(type));
        ValidationHelper.ValidateRequired(key, nameof(key));
        ValidationHelper.ValidateRequired(tenantId, nameof(tenantId));
        
        _logger.LogTrace("Getting document with Type: {Type} and Key: {Key}", type, key);

        var request = new DocumentKeyRequest { Type = type, Key = key };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseEndpoint}/get-by-key");
        httpRequest.Content = JsonContent.Create(request);
        httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, tenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogTrace("Document not found with Type: {Type} and Key: {Key}", type, key);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Document get by key failed: StatusCode={StatusCode}, Error={Error}",
                response.StatusCode,
                error);
            throw new HttpRequestException(
                $"Failed to get document by key. Status: {response.StatusCode}");
        }
        
        var document = await response.Content.ReadFromJsonAsync<Document>();
        _logger.LogInformation("Document retrieved successfully with Type: {Type} and Key: {Key}", type, key);
        return document;
    }

    /// <summary>
    /// Queries documents based on filters.
    /// </summary>
    /// <param name="request">The query request containing query parameters and tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of matching documents.</returns>
    public async Task<List<Document>> QueryAsync(QueryDocumentsRequest request, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateNotNull(request, nameof(request));
        ValidationHelper.ValidateNotNull(request.Query, nameof(request.Query));
        ValidationHelper.ValidateRequired(request.TenantId, nameof(request.TenantId));
        
        _logger.LogTrace("Querying documents with filters: Type={Type}, Limit={Limit}", 
            request.Query.Type, request.Query.Limit);

        var queryRequest = new DocumentQueryRequest { Query = request.Query };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseEndpoint}/query");
        httpRequest.Content = JsonContent.Create(queryRequest);
        httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, request.TenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Document query failed: StatusCode={StatusCode}, Error={Error}",
                response.StatusCode,
                error);
            throw new HttpRequestException(
                $"Failed to query documents. Status: {response.StatusCode}");
        }

        var documents = await response.Content.ReadFromJsonAsync<List<Document>>();
        
        _logger.LogInformation("Query returned {Count} documents", documents?.Count ?? 0);
        return documents ?? new List<Document>();
    }

    /// <summary>
    /// Updates an existing document.
    /// </summary>
    /// <param name="request">The update request containing document and tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if updated successfully, false if not found.</returns>
    public async Task<bool> UpdateAsync(UpdateDocumentRequest request, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateNotNull(request, nameof(request));
        ValidationHelper.ValidateNotNull(request.Document, nameof(request.Document));
        ValidationHelper.ValidateRequired(request.TenantId, nameof(request.TenantId));
        
        if (string.IsNullOrEmpty(request.Document.Id))
        {
            throw new ArgumentException("Document ID is required for update", nameof(request.Document));
        }

        _logger.LogTrace("Updating document with ID: {Id}", request.Document.Id);

        request.Document.UpdatedAt = DateTime.UtcNow;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseEndpoint}/update");
        httpRequest.Content = JsonContent.Create(request.Document);
        httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, request.TenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogTrace("Document not found for update with ID: {Id}", request.Document.Id);
            return false;
        }
        
        // Handle BadRequest as "not found" for update operations (server may return 400 for non-existent documents)
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            _logger.LogTrace("Document not found for update (400) with ID: {Id}", request.Document.Id);
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Document update failed: StatusCode={StatusCode}, Error={Error}",
                response.StatusCode,
                error);
            throw new HttpRequestException(
                $"Failed to update document. Status: {response.StatusCode}");
        }

        _logger.LogInformation("Document updated successfully with ID: {Id}", request.Document.Id);
        return true;
    }

    /// <summary>
    /// Deletes a document by its ID.
    /// </summary>
    /// <param name="request">The delete request containing document ID and tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted successfully, false if not found.</returns>
    public async Task<bool> DeleteAsync(DeleteDocumentRequest request, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateNotNull(request, nameof(request));
        ValidationHelper.ValidateRequired(request.Id, nameof(request.Id));
        ValidationHelper.ValidateRequired(request.TenantId, nameof(request.TenantId));
        
        _logger.LogTrace("Deleting document with ID: {Id}", request.Id);

        var documentIdRequest = new DocumentIdRequest { Id = request.Id };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseEndpoint}/delete");
        httpRequest.Content = JsonContent.Create(documentIdRequest);
        httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, request.TenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogTrace("Document not found for deletion with ID: {Id}", request.Id);
            return false;
        }
        
        // Handle BadRequest as "not found" for delete operations (server may return 400 for non-existent documents)
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            _logger.LogTrace("Document not found for deletion (400) with ID: {Id}", request.Id);
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Document delete failed: StatusCode={StatusCode}, Error={Error}",
                response.StatusCode,
                error);
            throw new HttpRequestException(
                $"Failed to delete document. Status: {response.StatusCode}");
        }

        _logger.LogInformation("Document deleted successfully with ID: {Id}", request.Id);
        return true;
    }

    /// <summary>
    /// Deletes multiple documents by their IDs.
    /// </summary>
    /// <param name="ids">The document IDs to delete.</param>
    /// <param name="tenantId">The tenant ID for isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of documents successfully deleted.</returns>
    public async Task<int> DeleteManyAsync(IEnumerable<string> ids, string tenantId, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateNotNull(ids, nameof(ids));
        ValidationHelper.ValidateRequired(tenantId, nameof(tenantId));
        
        var idList = ids.ToList();
        _logger.LogTrace("Deleting {Count} documents", idList.Count);

        var request = new DocumentIdsRequest { Ids = idList };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseEndpoint}/delete-many");
        httpRequest.Content = JsonContent.Create(request);
        httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, tenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Bulk delete failed: StatusCode={StatusCode}, Error={Error}",
                response.StatusCode,
                error);
            throw new HttpRequestException(
                $"Failed to delete documents. Status: {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<BulkDeleteResult>();
        var deletedCount = result?.DeletedCount ?? 0;
        
        _logger.LogInformation("Deleted {DeletedCount} out of {RequestedCount} documents", 
            deletedCount, idList.Count);
        return deletedCount;
    }

    /// <summary>
    /// Checks if a document exists.
    /// </summary>
    /// <param name="id">The document ID to check.</param>
    /// <param name="tenantId">The tenant ID for isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the document exists, false otherwise.</returns>
    public async Task<bool> ExistsAsync(string id, string tenantId, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateRequired(id, nameof(id));
        ValidationHelper.ValidateRequired(tenantId, nameof(tenantId));
        
        _logger.LogTrace("Checking existence of document with ID: {Id}", id);

        var request = new DocumentIdRequest { Id = id };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseEndpoint}/exists");
        httpRequest.Content = JsonContent.Create(request);
        httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, tenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Document exists check failed: StatusCode={StatusCode}, Error={Error}",
                response.StatusCode,
                error);
            throw new HttpRequestException(
                $"Failed to check document existence. Status: {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<ExistsResult>();
        var exists = result?.Exists ?? false;
        
        _logger.LogTrace("Document {Exists} with ID: {Id}", 
            exists ? "exists" : "does not exist", id);
        return exists;
    }

    #region Internal Request/Response Models

    private class DocumentRequest
    {
        public required Document Document { get; set; }
        public DocumentOptions? Options { get; set; }
    }

    private class DocumentIdRequest
    {
        public required string Id { get; set; }
    }

    private class DocumentKeyRequest
    {
        public required string Type { get; set; }
        public required string Key { get; set; }
    }

    private class DocumentIdsRequest
    {
        public required IEnumerable<string> Ids { get; set; }
    }

    private class DocumentQueryRequest
    {
        public required DocumentQuery Query { get; set; }
    }

    private class BulkDeleteResult
    {
        public int DeletedCount { get; set; }
    }

    private class ExistsResult
    {
        public bool Exists { get; set; }
    }

    #endregion
}

