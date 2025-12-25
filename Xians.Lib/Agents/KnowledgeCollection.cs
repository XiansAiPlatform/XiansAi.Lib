using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Models;
using Xians.Lib.Http;
using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;

namespace Xians.Lib.Agents;

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

    internal KnowledgeCollection(XiansAgent agent, IHttpClientService? httpService, Common.CacheService? cacheService)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _httpService = httpService;
        
        // Create shared knowledge service if HTTP service is available
        if (httpService != null)
        {
            var logger = Common.LoggerFactory.CreateLogger<KnowledgeService>();
            _knowledgeService = new KnowledgeService(httpService.Client, cacheService, logger);
        }
    }

    /// <summary>
    /// Retrieves knowledge by name.
    /// Automatically scoped to this agent's tenant and agent name.
    /// </summary>
    /// <param name="knowledgeName">The name of the knowledge to retrieve.</param>
    /// <returns>The knowledge object, or null if not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown if HTTP service is not available.</exception>
    public async Task<Knowledge?> GetAsync(string knowledgeName)
    {
        EnsureKnowledgeService();

        try
        {
            var tenantId = GetTenantId();
            return await _knowledgeService!.GetAsync(knowledgeName, _agent.Name, tenantId);
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
    /// <returns>True if successful, false otherwise.</returns>
    /// <exception cref="InvalidOperationException">Thrown if HTTP service is not available.</exception>
    public async Task<bool> UpdateAsync(string knowledgeName, string content, string? type = null)
    {
        EnsureKnowledgeService();

        try
        {
            var tenantId = GetTenantId();
            return await _knowledgeService!.UpdateAsync(knowledgeName, content, type, _agent.Name, tenantId);
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
    /// <returns>True if deleted, false if not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown if HTTP service is not available.</exception>
    public async Task<bool> DeleteAsync(string knowledgeName)
    {
        EnsureKnowledgeService();

        try
        {
            var tenantId = GetTenantId();
            return await _knowledgeService!.DeleteAsync(knowledgeName, _agent.Name, tenantId);
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
    /// <returns>A list of knowledge items.</returns>
    /// <exception cref="InvalidOperationException">Thrown if HTTP service is not available.</exception>
    public async Task<List<Knowledge>> ListAsync()
    {
        EnsureKnowledgeService();

        try
        {
            var tenantId = GetTenantId();
            return await _knowledgeService!.ListAsync(_agent.Name, tenantId);
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
    /// </summary>
    private string GetTenantId()
    {
        try
        {
            return _agent.Options?.TenantId ?? "default";
        }
        catch
        {
            // If tenant ID cannot be determined (e.g., in tests), use default
            return "default";
        }
    }
}

