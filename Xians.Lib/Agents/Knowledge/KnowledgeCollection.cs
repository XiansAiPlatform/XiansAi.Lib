using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Knowledge.Models;
using Xians.Lib.Http;
using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common.Caching;
using Xians.Lib.Common.Infrastructure;

namespace Xians.Lib.Agents.Knowledge;

/// <summary>
/// Manages knowledge operations for a specific agent.
/// Provides methods to retrieve, update, delete, and list knowledge items.
/// All operations are automatically scoped to the agent's name and tenant.
/// </summary>
public class KnowledgeCollection
{
    private readonly XiansAgent _agent;
    private readonly IHttpClientService? _httpService;
    private readonly KnowledgeService? _knowledgeService;
    private readonly ILogger<KnowledgeCollection> _logger;

    internal KnowledgeCollection(XiansAgent agent, IHttpClientService? httpService, Xians.Lib.Common.Caching.CacheService? cacheService)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _httpService = httpService;
        _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<KnowledgeCollection>();
        
        // Create shared knowledge service if HTTP service is available
        if (httpService != null)
        {
            var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<KnowledgeService>();
            _knowledgeService = new KnowledgeService(httpService.Client, cacheService, logger);
        }
    }

    /// <summary>
    /// Retrieves knowledge by name.
    /// Automatically scoped to this agent's tenant and agent name.
    /// </summary>
    /// <param name="knowledgeName">The name of the knowledge to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The knowledge object, or null if not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown if HTTP service is not available.</exception>
    public async Task<Xians.Lib.Agents.Knowledge.Models.Knowledge?> GetAsync(string knowledgeName, CancellationToken cancellationToken = default)
    {
        // Validate parameters first
        ValidationHelper.ValidateRequiredWithMaxLength(knowledgeName, nameof(knowledgeName), 256);
        
        EnsureKnowledgeService();

        try
        {
            var tenantId = GetTenantId();
            return await _knowledgeService!.GetAsync(knowledgeName, _agent.Name, tenantId, cancellationToken);
        }
        catch (Exception ex) when (ex is not HttpRequestException and not ArgumentException)
        {
            throw new InvalidOperationException($"Failed to fetch knowledge '{knowledgeName}'", ex);
        }
    }

    /// <summary>
    /// Updates or creates knowledge.
    /// Automatically scoped to this agent's tenant and agent name.
    /// </summary>
    /// <param name="knowledgeName">The name of the knowledge.</param>
    /// <param name="content">The knowledge content.</param>
    /// <param name="type">Optional knowledge type (e.g., "instruction", "document", "json", "markdown").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the operation succeeds.</returns>
    /// <exception cref="InvalidOperationException">Thrown if HTTP service is not available.</exception>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    public async Task<bool> UpdateAsync(string knowledgeName, string content, string? type = null, CancellationToken cancellationToken = default)
    {
        // Validate parameters first
        ValidationHelper.ValidateRequiredWithMaxLength(knowledgeName, nameof(knowledgeName), 256);
        ValidationHelper.ValidateRequired(content, nameof(content));
        
        EnsureKnowledgeService();

        try
        {
            var tenantId = GetTenantId();
            return await _knowledgeService!.UpdateAsync(knowledgeName, content, type, _agent.Name, tenantId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Knowledge update operation was cancelled for '{KnowledgeName}'", knowledgeName);
            throw;
        }
        catch (Exception ex) when (ex is not HttpRequestException and not ArgumentException)
        {
            throw new InvalidOperationException($"Failed to update knowledge '{knowledgeName}'", ex);
        }
    }

    /// <summary>
    /// Deletes knowledge by name.
    /// Automatically scoped to this agent's tenant and agent name.
    /// </summary>
    /// <param name="knowledgeName">The name of the knowledge to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted, false if not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown if HTTP service is not available.</exception>
    public async Task<bool> DeleteAsync(string knowledgeName, CancellationToken cancellationToken = default)
    {
        // Validate parameters first
        ValidationHelper.ValidateRequiredWithMaxLength(knowledgeName, nameof(knowledgeName), 256);
        
        EnsureKnowledgeService();

        try
        {
            var tenantId = GetTenantId();
            return await _knowledgeService!.DeleteAsync(knowledgeName, _agent.Name, tenantId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Knowledge delete operation was cancelled for '{KnowledgeName}'", knowledgeName);
            throw;
        }
        catch (Exception ex) when (ex is not HttpRequestException and not ArgumentException)
        {
            throw new InvalidOperationException($"Failed to delete knowledge '{knowledgeName}'", ex);
        }
    }

    /// <summary>
    /// Lists all knowledge for this agent.
    /// Automatically scoped to this agent's tenant and agent name.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of knowledge items.</returns>
    /// <exception cref="InvalidOperationException">Thrown if HTTP service is not available.</exception>
    public async Task<List<Xians.Lib.Agents.Knowledge.Models.Knowledge>> ListAsync(CancellationToken cancellationToken = default)
    {
        EnsureKnowledgeService();

        try
        {
            var tenantId = GetTenantId();
            return await _knowledgeService!.ListAsync(_agent.Name, tenantId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Knowledge list operation was cancelled for agent '{AgentName}'", _agent.Name);
            throw;
        }
        catch (Exception ex) when (ex is not HttpRequestException and not ArgumentException)
        {
            throw new InvalidOperationException($"Failed to list knowledge for agent '{_agent.Name}'", ex);
        }
    }

    /// <summary>
    /// Ensures knowledge service is available.
    /// </summary>
    private void EnsureKnowledgeService()
    {
        if (_knowledgeService == null)
        {
            throw new InvalidOperationException(
                "Knowledge service is not available. Ensure the agent was registered through XiansPlatform.Agents.");
        }
    }

    /// <summary>
    /// Gets the tenant ID for cache and HTTP operations.
    /// For non-system-scoped agents, uses the agent's certificate tenant ID.
    /// For system-scoped agents, extracts tenant from workflow context at runtime.
    /// </summary>
    private string GetTenantId()
    {
        // For non-system-scoped agents, use the agent's certificate tenant ID
        // For system-scoped agents, the tenant ID must come from workflow context
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
                "Knowledge API for system-scoped agents can only be used within a workflow or activity context. " +
                "The tenant ID is extracted from the workflow ID at runtime.");
        }
    }
}

