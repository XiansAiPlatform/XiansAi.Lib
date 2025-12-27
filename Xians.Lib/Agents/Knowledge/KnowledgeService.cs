using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Knowledge.Models;
using Xians.Lib.Common.Caching;
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
    public async Task<Xians.Lib.Agents.Knowledge.Models.Knowledge?> GetAsync(string knowledgeName, string agentName, string tenantId)
    {
        // Validate inputs
        ValidateInput(knowledgeName, nameof(knowledgeName));
        ValidateInput(agentName, nameof(agentName));

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

        var response = await _httpClient.SendAsync(httpRequest);

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

        var knowledge = await response.Content.ReadFromJsonAsync<Models.Knowledge>();

        // Cache the result if found
        if (knowledge != null)
        {
            _cacheService?.SetKnowledge(cacheKey, knowledge);
        }

        _logger.LogInformation(
            "Knowledge fetched successfully: Name={Name}",
            knowledgeName);

        return knowledge;
    }

    /// <summary>
    /// Updates or creates knowledge on the server.
    /// </summary>
    public async Task<bool> UpdateAsync(
        string knowledgeName, 
        string content, 
        string? type, 
        string agentName, 
        string tenantId)
    {
        // Validate inputs
        ValidateInput(knowledgeName, nameof(knowledgeName));
        ValidateInput(agentName, nameof(agentName));
        
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content cannot be null or empty", nameof(content));
        }

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

        var response = await _httpClient.SendAsync(httpRequest);

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
    public async Task<bool> DeleteAsync(string knowledgeName, string agentName, string tenantId)
    {
        // Validate inputs
        ValidateInput(knowledgeName, nameof(knowledgeName));
        ValidateInput(agentName, nameof(agentName));

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

        var response = await _httpClient.SendAsync(httpRequest);

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
    public async Task<List<Xians.Lib.Agents.Knowledge.Models.Knowledge>> ListAsync(string agentName, string tenantId)
    {
        // Validate inputs
        ValidateInput(agentName, nameof(agentName));

        // Build URL
        var endpoint = $"api/agent/knowledge/list?" +
                      $"agent={UrlEncoder.Default.Encode(agentName)}";

        _logger.LogDebug(
            "Listing knowledge: Agent={Agent}, Tenant={Tenant}",
            agentName,
            tenantId);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);

        var response = await _httpClient.SendAsync(httpRequest);

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

        var knowledgeList = await response.Content.ReadFromJsonAsync<List<Models.Knowledge>>();

        _logger.LogInformation(
            "Knowledge list fetched successfully: Count={Count}",
            knowledgeList?.Count ?? 0);

        return knowledgeList ?? new List<Models.Knowledge>();
    }

    /// <summary>
    /// Validates input parameters.
    /// </summary>
    private void ValidateInput(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} cannot be null or empty", paramName);
        }

        if (value.Length > 256)
        {
            throw new ArgumentException($"{paramName} exceeds maximum length of 256 characters", paramName);
        }
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

