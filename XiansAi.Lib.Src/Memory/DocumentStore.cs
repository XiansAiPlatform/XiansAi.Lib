using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using Server;
using Temporalio.Workflows;

namespace XiansAi.Memory;

/// <summary>
/// Implementation of document storage operations using the secure API backend.
/// </summary>
internal class DocumentStore : IDocumentStore {
    
    public async Task<Document> SaveAsync(Document document, DocumentOptions? options = null)
    {
        if (Workflow.InWorkflow) {
            return await Workflow.ExecuteLocalActivityAsync((SystemActivities a) => a.SaveDocument(document, options), new SystemLocalActivityOptions(600));
        } else {
            return await new DocumentStoreImpl().SaveAsync(document, options);
        }
    }

    public async Task<Document?> GetAsync(string id)
    {
        if (Workflow.InWorkflow) {
            return await Workflow.ExecuteLocalActivityAsync((SystemActivities a) => a.GetAsync(id), new SystemLocalActivityOptions());
        } else {
            return await new DocumentStoreImpl().GetAsync(id);
        }
    }

    public async Task<Document?> GetByKeyAsync(string type, string key)
    {
        if (Workflow.InWorkflow) {
            return await Workflow.ExecuteLocalActivityAsync((SystemActivities a) => a.GetByKeyAsync(type, key), new SystemLocalActivityOptions());
        } else {
            return await new DocumentStoreImpl().GetByKeyAsync(type, key);
        }
    }

    public async Task<List<Document>> QueryAsync(DocumentQuery query)
    {
        if (Workflow.InWorkflow) {
            return await Workflow.ExecuteLocalActivityAsync((SystemActivities a) => a.QueryAsync(query), new SystemLocalActivityOptions());
        } else {
            return await new DocumentStoreImpl().QueryAsync(query);
        }
    }

    public async Task<bool> UpdateAsync(Document document)
    {
        if (Workflow.InWorkflow) {
            return await Workflow.ExecuteLocalActivityAsync((SystemActivities a) => a.UpdateAsync(document), new SystemLocalActivityOptions());
        } else {
            return await new DocumentStoreImpl().UpdateAsync(document);
        }
    }

    public async Task<bool> DeleteAsync(string id)
    {
        if (Workflow.InWorkflow) {
            return await Workflow.ExecuteLocalActivityAsync((SystemActivities a) => a.DeleteAsync(id), new SystemLocalActivityOptions());
        } else {
            return await new DocumentStoreImpl().DeleteAsync(id);
        }
    }

    public async Task<int> DeleteManyAsync(IEnumerable<string> ids)
    {
        if (Workflow.InWorkflow) {
            return await Workflow.ExecuteLocalActivityAsync((SystemActivities a) => a.DeleteManyAsync(ids), new SystemLocalActivityOptions());
        } else {
            return await new DocumentStoreImpl().DeleteManyAsync(ids);
        }
    }

    public async Task<bool> ExistsAsync(string id)
    {
        if (Workflow.InWorkflow) {
            return await Workflow.ExecuteLocalActivityAsync((SystemActivities a) => a.ExistsAsync(id), new SystemLocalActivityOptions());
        } else {
            return await new DocumentStoreImpl().ExistsAsync(id);
        }
    }

}

public class DocumentStoreImpl : IDocumentStore
{
    private readonly ILogger<DocumentStore> _logger;
    private readonly string _baseEndpoint = "api/agent/documents";

    public DocumentStoreImpl()
    {
        _logger = Globals.LogFactory.CreateLogger<DocumentStore>();
    }

    public async Task<Document> SaveAsync(Document document, DocumentOptions? options = null)
    {
        _logger.LogInformation("Saving document{Id}", document.Id != null ? $" with ID: {document.Id}" : "");
        
        if (!SecureApi.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping document save operation");
            throw new InvalidOperationException("SecureApi is not ready");
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var request = new DocumentRequest 
            { 
                Document = document,
                Options = options
            };

            var response = await client.PostAsJsonAsync($"{_baseEndpoint}/save", request);
            response.EnsureSuccessStatusCode();

            var savedDocument = await response.Content.ReadFromJsonAsync<Document>();
            if (savedDocument == null)
            {
                throw new InvalidOperationException("Failed to deserialize saved document");
            }

            _logger.LogInformation("Document saved successfully with ID: {Id}", savedDocument.Id);
            return savedDocument;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving document");
            throw;
        }
    }

    public async Task<Document?> GetAsync(string id)
    {
        _logger.LogInformation("Getting document with ID: {Id}", id);
        
        if (!SecureApi.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping document get operation");
            return null;
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var request = new DocumentIdRequest { Id = id };
            
            var response = await client.PostAsJsonAsync($"{_baseEndpoint}/get", request);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Document not found with ID: {Id}", id);
                return null;
            }

            response.EnsureSuccessStatusCode();
            
            var document = await response.Content.ReadFromJsonAsync<Document>();
            _logger.LogInformation("Document retrieved successfully with ID: {Id}", id);
            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document with ID: {Id}", id);
            return null;
        }
    }

    public async Task<Document?> GetByKeyAsync(string type, string key)
    {
        _logger.LogInformation("Getting document with Type: {Type} and Key: {Key}", type, key);
        
        if (!SecureApi.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping document get by key operation");
            return null;
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var request = new DocumentKeyRequest { Type = type, Key = key };
            
            var response = await client.PostAsJsonAsync($"{_baseEndpoint}/get-by-key", request);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Document not found with Type: {Type} and Key: {Key}", type, key);
                return null;
            }

            response.EnsureSuccessStatusCode();
            
            var document = await response.Content.ReadFromJsonAsync<Document>();
            _logger.LogInformation("Document retrieved successfully with Type: {Type} and Key: {Key}", type, key);
            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document with Type: {Type} and Key: {Key}", type, key);
            return null;
        }
    }

    public async Task<List<Document>> QueryAsync(DocumentQuery query)
    {
        _logger.LogInformation("Querying documents with filters: Type={Type}, Limit={Limit}", 
            query.Type, query.Limit);
        
        if (!SecureApi.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping document query operation");
            return new List<Document>();
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var request = new DocumentQueryRequest 
            { 
                Query = query,
            };

            var response = await client.PostAsJsonAsync($"{_baseEndpoint}/query", request);
            response.EnsureSuccessStatusCode();

            var documents = await response.Content.ReadFromJsonAsync<List<Document>>();
            if (documents == null)
            {
                return new List<Document>();
            }

            _logger.LogInformation("Query returned {Count} documents", documents.Count);
            return documents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying documents");
            return new List<Document>();
        }
    }

    public async Task<bool> UpdateAsync(Document document)
    {
        if (string.IsNullOrEmpty(document.Id))
        {
            throw new ArgumentException("Document ID is required for update", nameof(document));
        }

        _logger.LogInformation("Updating document with ID: {Id}", document.Id);
        
        if (!SecureApi.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping document update operation");
            return false;
        }

        try
        {
            var client = SecureApi.Instance.Client;
            document.UpdatedAt = DateTime.UtcNow;

            var response = await client.PostAsJsonAsync($"{_baseEndpoint}/update", document);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Document not found for update with ID: {Id}", document.Id);
                return false;
            }

            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Document updated successfully with ID: {Id}", document.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document with ID: {Id}", document.Id);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(string id)
    {
        _logger.LogInformation("Deleting document with ID: {Id}", id);
        
        if (!SecureApi.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping document delete operation");
            return false;
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var request = new DocumentIdRequest { Id = id };

            var response = await client.PostAsJsonAsync($"{_baseEndpoint}/delete", request);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Document not found for deletion with ID: {Id}", id);
                return false;
            }

            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Document deleted successfully with ID: {Id}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document with ID: {Id}", id);
            return false;
        }
    }

    public async Task<int> DeleteManyAsync(IEnumerable<string> ids)
    {
        var idList = ids.ToList();
        _logger.LogInformation("Deleting {Count} documents", idList.Count);
        
        if (!SecureApi.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping bulk delete operation");
            return 0;
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var request = new DocumentIdsRequest { Ids = idList };

            var response = await client.PostAsJsonAsync($"{_baseEndpoint}/delete-many", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<BulkDeleteResult>();
            var deletedCount = result?.DeletedCount ?? 0;
            
            _logger.LogInformation("Deleted {DeletedCount} out of {RequestedCount} documents", 
                deletedCount, idList.Count);
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting multiple documents");
            return 0;
        }
    }

    public async Task<bool> ExistsAsync(string id)
    {
        _logger.LogInformation("Checking existence of document with ID: {Id}", id);
        
        if (!SecureApi.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping document exists operation");
            return false;
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var request = new DocumentIdRequest { Id = id };

            var response = await client.PostAsJsonAsync($"{_baseEndpoint}/exists", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ExistsResult>();
            var exists = result?.Exists ?? false;
            
            _logger.LogInformation("Document {Exists} with ID: {Id}", 
                exists ? "exists" : "does not exist", id);
            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking document existence with ID: {Id}", id);
            return false;
        }
    }

    private class BulkDeleteResult
    {
        public int DeletedCount { get; set; }
    }

    private class ExistsResult
    {
        public bool Exists { get; set; }
    }
}
