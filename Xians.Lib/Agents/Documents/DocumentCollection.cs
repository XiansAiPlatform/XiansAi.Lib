using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Documents.Models;
using Xians.Lib.Workflows.Documents;
using Xians.Lib.Workflows.Documents.Models;

namespace Xians.Lib.Agents.Documents;

/// <summary>
/// Provides document storage operations for an agent.
/// Documents are scoped to the agent and tenant.
/// </summary>
public class DocumentCollection
{
    private readonly XiansAgent _agent;
    private readonly DocumentService? _documentService;
    private readonly ILogger<DocumentCollection> _logger;

    internal DocumentCollection(XiansAgent agent, Http.IHttpClientService? httpService)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<DocumentCollection>();

        if (httpService != null)
        {
            var serviceLogger = Common.Infrastructure.LoggerFactory.CreateLogger<DocumentService>();
            _documentService = new DocumentService(httpService.Client, serviceLogger);
        }
    }

    /// <summary>
    /// Saves a document to the database.
    /// If the document has no ID, one will be generated.
    /// </summary>
    /// <param name="document">The document to save.</param>
    /// <param name="options">Optional storage options (TTL, overwrite, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The saved document with its assigned ID.</returns>
    /// <exception cref="InvalidOperationException">Thrown when HTTP service is not configured.</exception>
    public async Task<Document> SaveAsync(Document document, DocumentOptions? options = null, CancellationToken cancellationToken = default)
    {
        EnsureServiceAvailable();

        var tenantId = GetTenantId();
        
        // Populate AgentId and WorkflowId automatically
        document.AgentId = _agent.Name;
        if (Workflow.InWorkflow || ActivityExecutionContext.HasCurrent)
        {
            document.WorkflowId = XiansContext.WorkflowId;
        }
        
        _logger.LogInformation(
            "Saving document for agent '{AgentName}', tenant '{TenantId}'",
            _agent.Name,
            tenantId);

        // If in workflow, execute as activity for determinism
        if (Workflow.InWorkflow)
        {
            return await Workflow.ExecuteActivityAsync(
                (DocumentActivities act) => act.SaveDocumentAsync(new SaveDocumentRequest
                {
                    Document = document,
                    TenantId = tenantId,
                    Options = options
                }),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(30)
                });
        }

        return await _documentService!.SaveAsync(document, tenantId, options, cancellationToken);
    }

    /// <summary>
    /// Retrieves a document by its ID.
    /// </summary>
    /// <param name="id">The document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document if found, null otherwise.</returns>
    public async Task<Document?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureServiceAvailable();

        var tenantId = GetTenantId();
        
        _logger.LogDebug(
            "Getting document '{Id}' for agent '{AgentName}'",
            id,
            _agent.Name);

        Document? document;

        // If in workflow, execute as activity for determinism
        if (Workflow.InWorkflow)
        {
            document = await Workflow.ExecuteActivityAsync(
                (DocumentActivities act) => act.GetDocumentAsync(new GetDocumentRequest
                {
                    Id = id,
                    TenantId = tenantId
                }),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(30)
                });
        }
        else
        {
            document = await _documentService!.GetAsync(id, tenantId, cancellationToken);
        }

        // Filter by agent - only return if it belongs to this agent
        if (document != null && document.AgentId != _agent.Name)
        {
            _logger.LogWarning(
                "Document '{Id}' found but belongs to different agent. Expected: '{Expected}', Found: '{Found}'",
                id,
                _agent.Name,
                document.AgentId);
            return null;
        }

        return document;
    }

    /// <summary>
    /// Retrieves a document by its type and key combination.
    /// Useful for semantic keys like "user-123-preferences".
    /// </summary>
    /// <param name="type">The document type.</param>
    /// <param name="key">The document key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document if found, null otherwise.</returns>
    public async Task<Document?> GetByKeyAsync(string type, string key, CancellationToken cancellationToken = default)
    {
        EnsureServiceAvailable();

        var tenantId = GetTenantId();
        
        _logger.LogDebug(
            "Getting document by key: Type='{Type}', Key='{Key}', Agent='{AgentName}'",
            type,
            key,
            _agent.Name);

        // If in workflow, use query activity (no direct GetByKey activity exists)
        if (Workflow.InWorkflow)
        {
            var docs = await Workflow.ExecuteActivityAsync(
                (DocumentActivities act) => act.QueryDocumentsAsync(new QueryDocumentsRequest
                {
                    Query = new DocumentQuery
                    {
                        Type = type,
                        Key = key,
                        AgentId = _agent.Name,
                        Limit = 1
                    },
                    TenantId = tenantId
                }),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(30)
                });
            
            return docs?.FirstOrDefault();
        }

        // For non-workflow calls, we need to verify the returned document belongs to this agent
        var document = await _documentService!.GetByKeyAsync(type, key, tenantId, cancellationToken);
        
        // Filter by agent - only return if it belongs to this agent
        if (document != null && document.AgentId != _agent.Name)
        {
            _logger.LogWarning(
                "Document found but belongs to different agent. Expected: '{Expected}', Found: '{Found}'",
                _agent.Name,
                document.AgentId);
            return null;
        }
        
        return document;
    }

    /// <summary>
    /// Queries documents based on filters.
    /// All filters are combined with AND logic.
    /// </summary>
    /// <param name="query">The query parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of matching documents.</returns>
    public async Task<List<Document>> QueryAsync(DocumentQuery query, CancellationToken cancellationToken = default)
    {
        EnsureServiceAvailable();

        var tenantId = GetTenantId();
        
        // Automatically scope query to current agent
        query.AgentId = _agent.Name;
        
        _logger.LogDebug(
            "Querying documents for agent '{AgentName}': Type='{Type}', Limit={Limit}",
            _agent.Name,
            query.Type,
            query.Limit);

        // If in workflow, execute as activity for determinism
        if (Workflow.InWorkflow)
        {
            return await Workflow.ExecuteActivityAsync(
                (DocumentActivities act) => act.QueryDocumentsAsync(new QueryDocumentsRequest
                {
                    Query = query,
                    TenantId = tenantId
                }),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(30)
                });
        }

        return await _documentService!.QueryAsync(query, tenantId, cancellationToken);
    }

    /// <summary>
    /// Updates an existing document.
    /// The document must have an ID.
    /// </summary>
    /// <param name="document">The document to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if updated successfully, false if not found.</returns>
    public async Task<bool> UpdateAsync(Document document, CancellationToken cancellationToken = default)
    {
        EnsureServiceAvailable();

        var tenantId = GetTenantId();
        
        // Ensure AgentId and WorkflowId are set
        document.AgentId = _agent.Name;
        if (Workflow.InWorkflow || ActivityExecutionContext.HasCurrent)
        {
            document.WorkflowId = XiansContext.WorkflowId;
        }
        
        _logger.LogInformation(
            "Updating document '{Id}' for agent '{AgentName}'",
            document.Id,
            _agent.Name);

        // If in workflow, execute as activity for determinism
        if (Workflow.InWorkflow)
        {
            return await Workflow.ExecuteActivityAsync(
                (DocumentActivities act) => act.UpdateDocumentAsync(new UpdateDocumentRequest
                {
                    Document = document,
                    TenantId = tenantId
                }),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(30)
                });
        }

        return await _documentService!.UpdateAsync(document, tenantId, cancellationToken);
    }

    /// <summary>
    /// Deletes a document by its ID.
    /// </summary>
    /// <param name="id">The document ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted successfully, false if not found.</returns>
    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureServiceAvailable();

        var tenantId = GetTenantId();
        
        _logger.LogInformation(
            "Deleting document '{Id}' for agent '{AgentName}'",
            id,
            _agent.Name);

        // First verify the document belongs to this agent
        var document = await GetAsync(id, cancellationToken);
        if (document == null)
        {
            _logger.LogDebug(
                "Document '{Id}' not found or doesn't belong to agent '{AgentName}'",
                id,
                _agent.Name);
            return false;
        }

        // If in workflow, execute as activity for determinism
        if (Workflow.InWorkflow)
        {
            return await Workflow.ExecuteActivityAsync(
                (DocumentActivities act) => act.DeleteDocumentAsync(new DeleteDocumentRequest
                {
                    Id = id,
                    TenantId = tenantId
                }),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(30)
                });
        }

        return await _documentService!.DeleteAsync(id, tenantId, cancellationToken);
    }

    /// <summary>
    /// Deletes multiple documents by their IDs.
    /// </summary>
    /// <param name="ids">The document IDs to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of documents successfully deleted.</returns>
    public async Task<int> DeleteManyAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        EnsureServiceAvailable();

        var tenantId = GetTenantId();
        var idList = ids.ToList();
        
        _logger.LogInformation(
            "Deleting {Count} documents for agent '{AgentName}'",
            idList.Count,
            _agent.Name);

        // Filter IDs to only include documents belonging to this agent
        var validIds = new List<string>();
        foreach (var id in idList)
        {
            var doc = await GetAsync(id, cancellationToken);
            if (doc != null)
            {
                validIds.Add(id);
            }
        }

        if (validIds.Count == 0)
        {
            _logger.LogDebug("No valid documents to delete for agent '{AgentName}'", _agent.Name);
            return 0;
        }

        if (validIds.Count < idList.Count)
        {
            _logger.LogWarning(
                "Filtered out {FilteredCount} documents that don't belong to agent '{AgentName}'",
                idList.Count - validIds.Count,
                _agent.Name);
        }

        return await _documentService!.DeleteManyAsync(validIds, tenantId, cancellationToken);
    }

    /// <summary>
    /// Checks if a document exists.
    /// </summary>
    /// <param name="id">The document ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the document exists, false otherwise.</returns>
    public async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureServiceAvailable();

        var tenantId = GetTenantId();
        
        _logger.LogDebug(
            "Checking existence of document '{Id}' for agent '{AgentName}'",
            id,
            _agent.Name);

        // Use GetAsync which already filters by agent
        var document = await GetAsync(id, cancellationToken);
        return document != null;
    }

    private void EnsureServiceAvailable()
    {
        if (_documentService == null)
        {
            throw new InvalidOperationException(
                "Document service is not available. Ensure HTTP service is configured for the agent.");
        }
    }

    private string GetTenantId()
    {
        // For non-system-scoped agents, use the agent's certificate tenant ID
        // For system-scoped agents, the tenant ID must come from workflow context
        // (extracted from workflow ID during workflow execution)
        if (!_agent.SystemScoped)
        {
            return _agent.Options?.CertificateTenantId 
                ?? throw new InvalidOperationException(
                    "Tenant ID cannot be determined. XiansOptions must be properly configured with an API key.");
        }

        // System-scoped agent - must be called from workflow/activity context
        try
        {
            return XiansContext.TenantId;
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Documents API for system-scoped agents can only be used within a workflow or activity context. " +
                "The tenant ID is extracted from the workflow ID at runtime.");
        }
    }
}

