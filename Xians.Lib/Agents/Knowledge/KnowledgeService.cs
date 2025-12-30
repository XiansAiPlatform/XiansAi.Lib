using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Knowledge.Models;
using Xians.Lib.Common.Caching;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Workflows.Knowledge;

namespace Xians.Lib.Agents.Knowledge;

/// <summary>
/// Core service for knowledge operations with caching support.
/// Shared by both KnowledgeCollection and KnowledgeActivities to avoid code duplication.
/// </summary>
internal class KnowledgeService
{
    private readonly HttpClient _httpClient;
    private readonly Xians.Lib.Common.Caching.CacheService? _cacheService;
    private readonly ILogger _logger;

    public KnowledgeService(
        HttpClient httpClient, 
        Xians.Lib.Common.Caching.CacheService? cacheService,
        ILogger logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cacheService = cacheService;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves knowledge from server with caching.
    /// </summary>
    /// <param name="knowledgeName">The name of the knowledge to retrieve.</param>
    /// <param name="agentName">The agent name.</param>
    /// <param name="tenantId">The tenant ID for isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The knowledge object, or null if not found.</returns>
    public async Task<Xians.Lib.Agents.Knowledge.Models.Knowledge?> GetAsync(string knowledgeName, string agentName, string tenantId, CancellationToken cancellationToken = default)
    {
        // Validate inputs
        ValidationHelper.ValidateRequiredWithMaxLength(knowledgeName, nameof(knowledgeName), 256);
        ValidationHelper.ValidateRequiredWithMaxLength(agentName, nameof(agentName), 256);
        ValidationHelper.ValidateRequired(tenantId, nameof(tenantId));

        // Check cache first
        var cacheKey = GetCacheKey(tenantId, agentName, knowledgeName);
        var cached = _cacheService?.GetKnowledge<Models.Knowledge>(cacheKey);
        if (cached != null)
        {
            _logger.LogDebug(
                "Cache hit for knowledge: Name={Name}, Agent={Agent}, Tenant={Tenant}",
                knowledgeName,
                agentName,
                tenantId);
            return cached;
        }

        // Build URL
        var endpoint = $"api/agent/knowledge/latest?" +
                      $"name={UrlEncoder.Default.Encode(knowledgeName)}" +
                      $"&agent={UrlEncoder.Default.Encode(agentName)}";

        _logger.LogDebug(
            "Fetching knowledge from server: Name={Name}, Agent={Agent}, Tenant={Tenant}",
            knowledgeName,
            agentName,
            tenantId);

        // Create HTTP request with tenant header
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        // Handle 404 as knowledge not found
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation(
                "Knowledge not found: Name={Name}, Agent={Agent}",
                knowledgeName,
                agentName);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Failed to fetch knowledge: StatusCode={StatusCode}, Error={Error}",
                response.StatusCode,
                error);
            throw new HttpRequestException(
                $"Failed to fetch knowledge '{knowledgeName}'. Status: {response.StatusCode}");
        }

        var knowledge = await response.Content.ReadFromJsonAsync<Models.Knowledge>(cancellationToken);

        if (knowledge == null)
        {
            _logger.LogWarning(
                "Knowledge response deserialization returned null: Name={Name}, Agent={Agent}",
                knowledgeName,
                agentName);
            return null;
        }

        // Cache the result
        _cacheService?.SetKnowledge(cacheKey, knowledge);

        _logger.LogInformation(
            "Knowledge fetched successfully: Name={Name}",
            knowledgeName);

        return knowledge;
    }

    /// <summary>
    /// Updates or creates knowledge on the server.
    /// </summary>
    /// <param name="knowledgeName">The name of the knowledge.</param>
    /// <param name="content">The knowledge content.</param>
    /// <param name="type">Optional knowledge type (e.g., "instruction", "document").</param>
    /// <param name="agentName">The agent name.</param>
    /// <param name="tenantId">The tenant ID for isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the operation succeeds.</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    public async Task<bool> UpdateAsync(
        string knowledgeName, 
        string content, 
        string? type, 
        string agentName, 
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        ValidationHelper.ValidateRequiredWithMaxLength(knowledgeName, nameof(knowledgeName), 256);
        ValidationHelper.ValidateRequiredWithMaxLength(agentName, nameof(agentName), 256);
        ValidationHelper.ValidateRequired(content, nameof(content));
        ValidationHelper.ValidateRequired(tenantId, nameof(tenantId));

        // Build knowledge object
        var knowledge = new Models.Knowledge
        {
            Name = knowledgeName,
            Content = content,
            Type = type,
            Agent = agentName,
            TenantId = tenantId
        };

        _logger.LogDebug(
            "Updating knowledge: Name={Name}, Agent={Agent}, Type={Type}, ContentLength={Length}, Tenant={Tenant}",
            knowledgeName,
            agentName,
            type,
            content.Length,
            tenantId);

        // POST to api/agent/knowledge
        var endpoint = "api/agent/knowledge";
        
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Content = JsonContent.Create(knowledge);
        httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Failed to update knowledge: StatusCode={StatusCode}, Error={Error}",
                response.StatusCode,
                error);
            throw new HttpRequestException(
                $"Failed to update knowledge '{knowledgeName}'. Status: {response.StatusCode}");
        }

        // Invalidate cache after update
        var cacheKey = GetCacheKey(tenantId, agentName, knowledgeName);
        _cacheService?.RemoveKnowledge(cacheKey);

        _logger.LogInformation(
            "Knowledge updated successfully: Name={Name}",
            knowledgeName);
        
        return true;
    }

    /// <summary>
    /// Deletes knowledge from the server.
    /// </summary>
    /// <param name="knowledgeName">The name of the knowledge to delete.</param>
    /// <param name="agentName">The agent name.</param>
    /// <param name="tenantId">The tenant ID for isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted, false if not found.</returns>
    public async Task<bool> DeleteAsync(string knowledgeName, string agentName, string tenantId, CancellationToken cancellationToken = default)
    {
        // Validate inputs
        ValidationHelper.ValidateRequiredWithMaxLength(knowledgeName, nameof(knowledgeName), 256);
        ValidationHelper.ValidateRequiredWithMaxLength(agentName, nameof(agentName), 256);
        ValidationHelper.ValidateRequired(tenantId, nameof(tenantId));

        // Build URL
        var endpoint = $"api/agent/knowledge?" +
                      $"name={UrlEncoder.Default.Encode(knowledgeName)}" +
                      $"&agent={UrlEncoder.Default.Encode(agentName)}";

        _logger.LogDebug(
            "Deleting knowledge: Name={Name}, Agent={Agent}, Tenant={Tenant}",
            knowledgeName,
            agentName,
            tenantId);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Delete, endpoint);
        httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        // Handle 404 as already deleted
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation(
                "Knowledge not found (already deleted?): Name={Name}, Agent={Agent}",
                knowledgeName,
                agentName);
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Failed to delete knowledge: StatusCode={StatusCode}, Error={Error}",
                response.StatusCode,
                error);
            throw new HttpRequestException(
                $"Failed to delete knowledge '{knowledgeName}'. Status: {response.StatusCode}");
        }

        // Invalidate cache after deletion
        var cacheKey = GetCacheKey(tenantId, agentName, knowledgeName);
        _cacheService?.RemoveKnowledge(cacheKey);

        _logger.LogInformation(
            "Knowledge deleted successfully: Name={Name}",
            knowledgeName);

        return true;
    }

    /// <summary>
    /// Lists all knowledge for an agent.
    /// </summary>
    /// <param name="agentName">The agent name.</param>
    /// <param name="tenantId">The tenant ID for isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of knowledge items.</returns>
    public async Task<List<Xians.Lib.Agents.Knowledge.Models.Knowledge>> ListAsync(string agentName, string tenantId, CancellationToken cancellationToken = default)
    {
        // Validate inputs
        ValidationHelper.ValidateRequiredWithMaxLength(agentName, nameof(agentName), 256);
        ValidationHelper.ValidateRequired(tenantId, nameof(tenantId));

        // Build URL
        var endpoint = $"api/agent/knowledge/list?" +
                      $"agent={UrlEncoder.Default.Encode(agentName)}";

        _logger.LogDebug(
            "Listing knowledge: Agent={Agent}, Tenant={Tenant}",
            agentName,
            tenantId);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Failed to list knowledge: StatusCode={StatusCode}, Error={Error}",
                response.StatusCode,
                error);
            throw new HttpRequestException(
                $"Failed to list knowledge for agent '{agentName}'. Status: {response.StatusCode}");
        }

        var knowledgeList = await response.Content.ReadFromJsonAsync<List<Models.Knowledge>>(cancellationToken);

        if (knowledgeList == null)
        {
            _logger.LogWarning(
                "Knowledge list deserialization returned null for agent '{Agent}'",
                agentName);
            return new List<Models.Knowledge>();
        }

        _logger.LogInformation(
            "Knowledge list fetched successfully: Count={Count}",
            knowledgeList.Count);

        return knowledgeList;
    }

    /// <summary>
    /// Generates a cache key for knowledge items.
    /// Format: "knowledge:{tenantId}:{agentName}:{knowledgeName}"
    /// </summary>
    private string GetCacheKey(string tenantId, string agentName, string knowledgeName)
    {
        return $"knowledge:{tenantId}:{agentName}:{knowledgeName}";
    }
}

