using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Documents.Models;
using Xians.Lib.Common;
using Xians.Lib.Common.Infrastructure;

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
    /// <param name="document">The document to save.</param>
    /// <param name="tenantId">The tenant ID for isolation.</param>
    /// <param name="options">Optional document storage options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The saved document with its assigned ID.</returns>
    public async Task<Document> SaveAsync(Document document, string tenantId, DocumentOptions? options = null, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateNotNull(document, nameof(document));
        ValidationHelper.ValidateRequired(tenantId, nameof(tenantId));
        
        _logger.LogTrace("Saving document{Id}", document.Id != null ? $" with ID: {document.Id}" : "");

        // Validate UseKeyAsIdentifier requirements
        if (options?.UseKeyAsIdentifier == true)
        {
            var missingFields = new List<string>();
            if (string.IsNullOrEmpty(document.Type)) missingFields.Add("Type");
            if (string.IsNullOrEmpty(document.Key)) missingFields.Add("Key");
            
            if (missingFields.Any())
            {
                var message = $"UseKeyAsIdentifier requires both Type and Key properties. Missing: {string.Join(", ", missingFields)}";
                _logger.LogError("Document save validation failed: {Message}", message);
                throw new ArgumentException(message);
            }
        }

        var request = new DocumentRequest 
        { 
            Document = document,
            Options = options
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseEndpoint}/save");
        httpRequest.Content = JsonContent.Create(request);
        httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, tenantId);

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
    /// <param name="id">The document ID.</param>
    /// <param name="tenantId">The tenant ID for isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document if found, null otherwise.</returns>
    public async Task<Document?> GetAsync(string id, string tenantId, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateRequired(id, nameof(id));
        ValidationHelper.ValidateRequired(tenantId, nameof(tenantId));
        
        _logger.LogTrace("Getting document with ID: {Id}", id);

        var request = new DocumentIdRequest { Id = id };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseEndpoint}/get");
        httpRequest.Content = JsonContent.Create(request);
        httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, tenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogTrace("Document not found with ID: {Id}", id);
            return null;
        }
        
        // Handle InternalServerError as "not found" (server may return 500 for non-existent documents)
        if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
        {
            _logger.LogTrace("Document not found (500) with ID: {Id}", id);
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
        _logger.LogInformation("Document retrieved successfully with ID: {Id}", id);
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
    /// <param name="query">The query parameters.</param>
    /// <param name="tenantId">The tenant ID for isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of matching documents.</returns>
    public async Task<List<Document>> QueryAsync(DocumentQuery query, string tenantId, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateNotNull(query, nameof(query));
        ValidationHelper.ValidateRequired(tenantId, nameof(tenantId));
        
        _logger.LogTrace("Querying documents with filters: Type={Type}, Limit={Limit}", 
            query.Type, query.Limit);

        var request = new DocumentQueryRequest { Query = query };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseEndpoint}/query");
        httpRequest.Content = JsonContent.Create(request);
        httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, tenantId);

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
    /// <param name="document">The document to update (must have an ID).</param>
    /// <param name="tenantId">The tenant ID for isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if updated successfully, false if not found.</returns>
    public async Task<bool> UpdateAsync(Document document, string tenantId, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateNotNull(document, nameof(document));
        ValidationHelper.ValidateRequired(tenantId, nameof(tenantId));
        
        if (string.IsNullOrEmpty(document.Id))
        {
            throw new ArgumentException("Document ID is required for update", nameof(document));
        }

        _logger.LogTrace("Updating document with ID: {Id}", document.Id);

        document.UpdatedAt = DateTime.UtcNow;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseEndpoint}/update");
        httpRequest.Content = JsonContent.Create(document);
        httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, tenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogTrace("Document not found for update with ID: {Id}", document.Id);
            return false;
        }
        
        // Handle BadRequest as "not found" for update operations (server may return 400 for non-existent documents)
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            _logger.LogTrace("Document not found for update (400) with ID: {Id}", document.Id);
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

        _logger.LogInformation("Document updated successfully with ID: {Id}", document.Id);
        return true;
    }

    /// <summary>
    /// Deletes a document by its ID.
    /// </summary>
    /// <param name="id">The document ID to delete.</param>
    /// <param name="tenantId">The tenant ID for isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted successfully, false if not found.</returns>
    public async Task<bool> DeleteAsync(string id, string tenantId, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateRequired(id, nameof(id));
        ValidationHelper.ValidateRequired(tenantId, nameof(tenantId));
        
        _logger.LogTrace("Deleting document with ID: {Id}", id);

        var request = new DocumentIdRequest { Id = id };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseEndpoint}/delete");
        httpRequest.Content = JsonContent.Create(request);
        httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, tenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogTrace("Document not found for deletion with ID: {Id}", id);
            return false;
        }
        
        // Handle BadRequest as "not found" for delete operations (server may return 400 for non-existent documents)
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            _logger.LogTrace("Document not found for deletion (400) with ID: {Id}", id);
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

        _logger.LogInformation("Document deleted successfully with ID: {Id}", id);
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

