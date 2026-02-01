using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Documents.Models;
using Xians.Lib.Temporal.Workflows.Documents;
using Xians.Lib.Temporal.Workflows.Documents.Models;

namespace Xians.Lib.Agents.Documents;

/// <summary>
/// Provides document storage operations for an agent.
/// Documents are scoped to the agent and tenant.
/// REFACTORED: Uses DocumentActivityExecutor for context-aware execution.
/// </summary>
public class DocumentCollection
{
    private readonly XiansAgent _agent;
    private readonly DocumentActivityExecutor _executor;
    private readonly ILogger<DocumentCollection> _logger;

    internal DocumentCollection(XiansAgent agent, Http.IHttpClientService? httpService)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<DocumentCollection>();

        // Initialize executor for context-aware execution
        var executorLogger = Common.Infrastructure.LoggerFactory.CreateLogger<DocumentActivityExecutor>();
        _executor = new DocumentActivityExecutor(agent, executorLogger);
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
        var tenantId = GetTenantId();
        
        // Shared business logic: Populate AgentId and WorkflowId automatically
        PrepareDocumentForSave(document);
        
        _logger.LogDebug(
            "Saving document for agent '{AgentName}', tenant '{TenantId}'",
            _agent.Name,
            tenantId);

        // Context-aware execution via executor
        return await _executor.SaveAsync(document, tenantId, options, cancellationToken);
    }

    /// <summary>
    /// Retrieves a document by its ID.
    /// </summary>
    /// <param name="id">The document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document if found, null otherwise.</returns>
    public async Task<Document?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        
        _logger.LogDebug(
            "Getting document '{Id}' for agent '{AgentName}'",
            id,
            _agent.Name);

        // Context-aware execution via executor
        var document = await _executor.GetAsync(id, tenantId, cancellationToken);

        // Shared business logic: Filter by agent
        return FilterDocumentByAgent(document, id);
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
        var tenantId = GetTenantId();
        
        _logger.LogDebug(
            "Getting document by key: Type='{Type}', Key='{Key}', Agent='{AgentName}'",
            type,
            key,
            _agent.Name);

        // Use QueryAsync for consistency - shared business logic
        var query = new DocumentQuery
        {
            Type = type,
            Key = key,
            AgentId = _agent.Name,
            Limit = 1
        };
        
        // Auto-populate context values for more specific queries
        if (XiansContext.InWorkflowOrActivity)
        {
            query.ActivationName = XiansContext.SafeIdPostfix;
            query.ParticipantId = XiansContext.SafeParticipantId;
        }

        var docs = await _executor.QueryAsync(query, tenantId, cancellationToken);
        return docs?.FirstOrDefault();
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
        var tenantId = GetTenantId();
        
        // Shared business logic: Automatically scope query to current agent
        query.AgentId = _agent.Name;
        
        // Auto-populate ActivationName and ParticipantId from XiansContext if available and not already set
        if (XiansContext.InWorkflowOrActivity)
        {
            query.ActivationName ??= XiansContext.SafeIdPostfix;
            query.ParticipantId ??= XiansContext.SafeParticipantId;
        }
        
        _logger.LogDebug(
            "Querying documents for agent '{AgentName}': Type='{Type}', Limit={Limit}",
            _agent.Name,
            query.Type,
            query.Limit);

        // Context-aware execution via executor
        return await _executor.QueryAsync(query, tenantId, cancellationToken);
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
        var tenantId = GetTenantId();
        
        // Shared business logic: Ensure AgentId and WorkflowId are set
        PrepareDocumentForSave(document);
        
        _logger.LogInformation(
            "Updating document '{Id}' for agent '{AgentName}'",
            document.Id,
            _agent.Name);

        // Context-aware execution via executor
        return await _executor.UpdateAsync(document, tenantId, cancellationToken);
    }

    /// <summary>
    /// Deletes a document by its ID.
    /// </summary>
    /// <param name="id">The document ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted successfully, false if not found.</returns>
    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        
        _logger.LogInformation(
            "Deleting document '{Id}' for agent '{AgentName}'",
            id,
            _agent.Name);

        // Shared business logic: Verify the document belongs to this agent before deletion
        var document = await GetAsync(id, cancellationToken);
        if (document == null)
        {
            _logger.LogDebug(
                "Document '{Id}' not found or doesn't belong to agent '{AgentName}'",
                id,
                _agent.Name);
            return false;
        }

        // Context-aware execution via executor
        return await _executor.DeleteAsync(id, tenantId, cancellationToken);
    }

    /// <summary>
    /// Deletes multiple documents by their IDs.
    /// </summary>
    /// <param name="ids">The document IDs to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of documents successfully deleted.</returns>
    public async Task<int> DeleteManyAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        
        _logger.LogInformation(
            "Deleting {Count} documents for agent '{AgentName}'",
            idList.Count,
            _agent.Name);

        // Shared business logic: Filter IDs to only include documents belonging to this agent
        var validIds = await FilterValidDocumentIdsAsync(idList, cancellationToken);

        if (validIds.Count == 0)
        {
            return 0;
        }

        // Delete each valid document (reuses DeleteAsync for consistency)
        int deletedCount = 0;
        foreach (var id in validIds)
        {
            if (await DeleteAsync(id, cancellationToken))
            {
                deletedCount++;
            }
        }

        return deletedCount;
    }

    /// <summary>
    /// Checks if a document exists.
    /// </summary>
    /// <param name="id">The document ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the document exists, false otherwise.</returns>
    public async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Checking existence of document '{Id}' for agent '{AgentName}'",
            id,
            _agent.Name);

        // Reuse GetAsync - shared business logic with filtering
        var document = await GetAsync(id, cancellationToken);
        return document != null;
    }

    #region Shared Business Logic Methods

    /// <summary>
    /// Prepares a document for save/update by setting agent and workflow metadata.
    /// This is shared business logic used by both SaveAsync and UpdateAsync.
    /// </summary>
    private void PrepareDocumentForSave(Document document)
    {
        document.AgentId = _agent.Name;
        if (XiansContext.InWorkflowOrActivity)
        {
            document.WorkflowId = XiansContext.WorkflowId;
            
            // Populate ActivationName and ParticipantId from XiansContext
            document.ActivationName = XiansContext.SafeIdPostfix;
            document.ParticipantId = XiansContext.SafeParticipantId;
        }
    }

    /// <summary>
    /// Filters a document by agent ownership.
    /// Returns null if document doesn't belong to this agent.
    /// This is shared business logic used by GetAsync and GetByKeyAsync.
    /// </summary>
    private Document? FilterDocumentByAgent(Document? document, string? documentId = null)
    {
        if (document == null)
        {
            return null;
        }

        if (document.AgentId != _agent.Name)
        {
            var idInfo = documentId != null ? $"'{documentId}'" : "";
            _logger.LogWarning(
                "Document {Id} found but belongs to different agent. Expected: '{Expected}', Found: '{Found}'",
                idInfo,
                _agent.Name,
                document.AgentId);
            return null;
        }

        return document;
    }

    /// <summary>
    /// Filters a list of document IDs to only include documents belonging to this agent.
    /// This is shared business logic used by DeleteManyAsync.
    /// </summary>
    private async Task<List<string>> FilterValidDocumentIdsAsync(
        List<string> ids,
        CancellationToken cancellationToken)
    {
        var validIds = new List<string>();
        
        foreach (var id in ids)
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
        }
        else if (validIds.Count < ids.Count)
        {
            _logger.LogWarning(
                "Filtered out {FilteredCount} documents that don't belong to agent '{AgentName}'",
                ids.Count - validIds.Count,
                _agent.Name);
        }

        return validIds;
    }

    #endregion

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

