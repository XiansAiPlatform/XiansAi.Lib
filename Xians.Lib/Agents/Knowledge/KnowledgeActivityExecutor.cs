using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Knowledge.Models;
using Xians.Lib.Agents.Knowledge.Providers;
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
        var logger = Common.Infrastructure.LoggerFactory.CreateLogger<KnowledgeService>();
        
        // Ensure options is not null (should never be null from a properly initialized agent)
        if (_agent.Options == null)
        {
            throw new InvalidOperationException(
                "Agent options are not set. Ensure the agent was properly initialized.");
        }
        
        // Create provider based on agent options (supports local mode)
        var provider = KnowledgeProviderFactory.Create(
            _agent.Options,
            _agent.HttpService?.Client,
            _agent.CacheService,
            logger);

        return new KnowledgeService(provider);
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
            ActivationName = activationName
        };

        return await ExecuteAsync(
            act => act.UpdateKnowledgeAsync(request),
            svc => svc.UpdateAsync(knowledgeName, content, type, agentName, tenantId, systemScoped, activationName, cancellationToken),
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

