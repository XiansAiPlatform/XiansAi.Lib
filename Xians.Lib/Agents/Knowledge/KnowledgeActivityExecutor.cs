using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Knowledge.Models;
using Xians.Lib.Temporal;
using Xians.Lib.Temporal.Workflows.Knowledge;
using Xians.Lib.Temporal.Workflows.Knowledge.Models;

namespace Xians.Lib.Agents.Knowledge;

/// <summary>
/// Activity executor for knowledge operations.
/// Handles context-aware execution of knowledge activities.
/// Eliminates duplication of Workflow.InWorkflow checks in KnowledgeCollection.
/// Pattern matches DocumentActivityExecutor for consistency.
/// </summary>
internal class KnowledgeActivityExecutor : ContextAwareActivityExecutor<KnowledgeActivities, KnowledgeService>
{
    private readonly XiansAgent _agent;

    public KnowledgeActivityExecutor(XiansAgent agent, ILogger logger)
        : base(logger)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
    }

    protected override KnowledgeService CreateService()
    {
        if (_agent.HttpService == null)
        {
            throw new InvalidOperationException(
                "Knowledge service is not available. Ensure HTTP service is configured for the agent.");
        }

        var logger = Common.Infrastructure.LoggerFactory.CreateLogger<KnowledgeService>();
        // Use the agent's cache service instead of static one to respect per-platform cache settings
        var cacheService = _agent.CacheService;
        return new KnowledgeService(_agent.HttpService.Client, cacheService, logger);
    }

    /// <summary>
    /// Gets knowledge using context-aware execution.
    /// </summary>
    public async Task<Models.Knowledge?> GetAsync(
        string knowledgeName,
        string agentName,
        string? tenantId,
        string? activationName,
        CancellationToken cancellationToken = default)
    {
        var request = new GetKnowledgeRequest
        {
            KnowledgeName = knowledgeName,
            AgentName = agentName,
            TenantId = tenantId,
            ActivationName = activationName
        };

        return await ExecuteAsync(
            act => act.GetKnowledgeAsync(request),
            svc => svc.GetAsync(knowledgeName, agentName, tenantId, activationName, cancellationToken),
            operationName: "GetKnowledge");
    }
    
    /// <summary>
    /// Gets system-scoped knowledge (no tenant) using context-aware execution.
    /// </summary>
    public async Task<Models.Knowledge?> GetSystemAsync(
        string knowledgeName,
        string agentName,
        string? activationName,
        CancellationToken cancellationToken = default)
    {
        var request = new GetKnowledgeRequest
        {
            KnowledgeName = knowledgeName,
            AgentName = agentName,
            TenantId = null,
            ActivationName = activationName
        };

        return await ExecuteAsync(
            act => act.GetSystemKnowledgeAsync(request),
            svc => svc.GetSystemAsync(knowledgeName, agentName, activationName, cancellationToken),
            operationName: "GetSystemKnowledge");
    }

    /// <summary>
    /// Updates knowledge using context-aware execution.
    /// </summary>
    public async Task<bool> UpdateAsync(
        string knowledgeName,
        string content,
        string? type,
        string agentName,
        string? tenantId,
        bool systemScoped = false,
        string? activationName = null,
        string? description = null,
        bool visible = true,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateKnowledgeRequest
        {
            KnowledgeName = knowledgeName,
            Content = content,
            Type = type,
            AgentName = agentName,
            TenantId = tenantId,
            SystemScoped = systemScoped,
            ActivationName = activationName,
            Description = description,
            Visible = visible
        };

        return await ExecuteAsync(
            act => act.UpdateKnowledgeAsync(request),
            svc => svc.UpdateAsync(knowledgeName, content, type, agentName, tenantId, systemScoped, activationName, description, visible, cancellationToken),
            operationName: "UpdateKnowledge");
    }

    /// <summary>
    /// Deletes knowledge using context-aware execution.
    /// </summary>
    public async Task<bool> DeleteAsync(
        string knowledgeName,
        string agentName,
        string? tenantId,
        string? activationName,
        CancellationToken cancellationToken = default)
    {
        var request = new DeleteKnowledgeRequest
        {
            KnowledgeName = knowledgeName,
            AgentName = agentName,
            TenantId = tenantId,
            ActivationName = activationName
        };

        return await ExecuteAsync(
            act => act.DeleteKnowledgeAsync(request),
            svc => svc.DeleteAsync(knowledgeName, agentName, tenantId, activationName, cancellationToken),
            operationName: "DeleteKnowledge");
    }

    /// <summary>
    /// Lists knowledge using context-aware execution.
    /// </summary>
    public async Task<List<Models.Knowledge>> ListAsync(
        string agentName,
        string? tenantId,
        string? activationName,
        CancellationToken cancellationToken = default)
    {
        var request = new ListKnowledgeRequest
        {
            AgentName = agentName,
            TenantId = tenantId,
            ActivationName = activationName
        };

        return await ExecuteAsync(
            act => act.ListKnowledgeAsync(request),
            svc => svc.ListAsync(agentName, tenantId, activationName, cancellationToken),
            operationName: "ListKnowledge");
    }
}

