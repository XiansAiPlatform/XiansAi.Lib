using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Documents.Models;
using Xians.Lib.Workflows.Documents;
using Xians.Lib.Workflows.Documents.Models;

namespace Xians.Lib.Agents.Documents;

/// <summary>
/// Activity executor for document operations.
/// Handles context-aware execution of document activities.
/// Eliminates duplication of Workflow.InWorkflow checks in DocumentCollection.
/// </summary>
internal class DocumentActivityExecutor : ContextAwareActivityExecutor<DocumentActivities, DocumentService>
{
    private readonly XiansAgent _agent;

    public DocumentActivityExecutor(XiansAgent agent, ILogger logger)
        : base(logger)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
    }

    protected override DocumentService CreateService()
    {
        if (_agent.HttpService == null)
        {
            throw new InvalidOperationException(
                "Document service is not available. Ensure HTTP service is configured for the agent.");
        }

        var logger = Common.Infrastructure.LoggerFactory.CreateLogger<DocumentService>();
        return new DocumentService(_agent.HttpService.Client, logger);
    }

    /// <summary>
    /// Saves a document using context-aware execution.
    /// </summary>
    public async Task<Document> SaveAsync(
        Document document,
        string tenantId,
        DocumentOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = new SaveDocumentRequest
        {
            Document = document,
            TenantId = tenantId,
            Options = options
        };

        return await ExecuteAsync(
            act => act.SaveDocumentAsync(request),
            svc => svc.SaveAsync(request, cancellationToken),
            operationName: "SaveDocument");
    }

    /// <summary>
    /// Gets a document by ID using context-aware execution.
    /// </summary>
    public async Task<Document?> GetAsync(
        string id,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var request = new GetDocumentRequest
        {
            Id = id,
            TenantId = tenantId
        };

        return await ExecuteAsync(
            act => act.GetDocumentAsync(request),
            svc => svc.GetAsync(request, cancellationToken),
            operationName: "GetDocument");
    }

    /// <summary>
    /// Queries documents using context-aware execution.
    /// </summary>
    public async Task<List<Document>> QueryAsync(
        DocumentQuery query,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var request = new QueryDocumentsRequest
        {
            Query = query,
            TenantId = tenantId
        };

        return await ExecuteAsync(
            act => act.QueryDocumentsAsync(request),
            svc => svc.QueryAsync(request, cancellationToken),
            operationName: "QueryDocuments");
    }

    /// <summary>
    /// Updates a document using context-aware execution.
    /// </summary>
    public async Task<bool> UpdateAsync(
        Document document,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateDocumentRequest
        {
            Document = document,
            TenantId = tenantId
        };

        return await ExecuteAsync(
            act => act.UpdateDocumentAsync(request),
            svc => svc.UpdateAsync(request, cancellationToken),
            operationName: "UpdateDocument");
    }

    /// <summary>
    /// Deletes a document using context-aware execution.
    /// </summary>
    public async Task<bool> DeleteAsync(
        string id,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var request = new DeleteDocumentRequest
        {
            Id = id,
            TenantId = tenantId
        };

        return await ExecuteAsync(
            act => act.DeleteDocumentAsync(request),
            svc => svc.DeleteAsync(request, cancellationToken),
            operationName: "DeleteDocument");
    }
}

