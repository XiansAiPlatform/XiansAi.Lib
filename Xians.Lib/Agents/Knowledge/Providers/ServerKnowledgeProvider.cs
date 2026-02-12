using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Logging;
using Xians.Lib.Common;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Common.Caching;

namespace Xians.Lib.Agents.Knowledge.Providers;

/// <summary>
/// Provides knowledge from the Xians server via HTTP.
/// This is the production implementation that makes real HTTP calls.
/// </summary>
internal class ServerKnowledgeProvider : IKnowledgeProvider
{
    private readonly HttpClient _httpClient;
    private readonly CacheService? _cacheService;
    private readonly ILogger _logger;

    public ServerKnowledgeProvider(
        HttpClient httpClient,
        CacheService? cacheService,
        ILogger logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cacheService = cacheService;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Models.Knowledge?> GetAsync(
        string knowledgeName,
        string agentName,
        string? tenantId,
        string? activationName,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        ValidationHelper.ValidateRequiredWithMaxLength(knowledgeName, nameof(knowledgeName), 256);
        ValidationHelper.ValidateRequiredWithMaxLength(agentName, nameof(agentName), 256);

        // Check cache first
        var cacheKey = GetCacheKey(tenantId, agentName, activationName, knowledgeName);
        var cached = _cacheService?.GetKnowledge<Models.Knowledge>(cacheKey);
        if (cached != null)
        {
            _logger.LogDebug(
                "Cache hit for knowledge: Name={Name}, Agent={Agent}, Tenant={Tenant}, ActivationName={ActivationName}",
                knowledgeName,
                agentName,
                tenantId,
                activationName);
            return cached;
        }

        // Build URL
        var endpoint = $"{WorkflowConstants.ApiEndpoints.KnowledgeLatest}?" +
                      $"name={UrlEncoder.Default.Encode(knowledgeName)}" +
                      $"&agent={UrlEncoder.Default.Encode(agentName)}";

        if (!string.IsNullOrEmpty(activationName))
        {
            endpoint += $"&activationName={UrlEncoder.Default.Encode(activationName)}";
        }

        _logger.LogDebug(
            "Fetching knowledge from server: Name={Name}, Agent={Agent}, Tenant={Tenant}, ActivationName={ActivationName}",
            knowledgeName,
            agentName,
            tenantId,
            activationName);

        // Create HTTP request with tenant header
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, tenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        // Handle 404 as knowledge not found
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogDebug(
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

        _logger.LogDebug(
            "Knowledge fetched successfully: Name={Name}",
            knowledgeName);

        return knowledge;
    }

    public async Task<Models.Knowledge?> GetSystemAsync(
        string knowledgeName,
        string agentName,
        string? activationName,
        CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateRequiredWithMaxLength(knowledgeName, nameof(knowledgeName), 256);
        ValidationHelper.ValidateRequiredWithMaxLength(agentName, nameof(agentName), 256);

        var cacheKey = GetCacheKey(null, agentName, activationName, knowledgeName);
        var cached = _cacheService?.GetKnowledge<Models.Knowledge>(cacheKey);
        if (cached != null)
        {
            _logger.LogDebug(
                "Cache hit for system knowledge: Name={Name}, Agent={Agent}, ActivationName={ActivationName}",
                knowledgeName,
                agentName,
                activationName);
            return cached;
        }

        var endpoint = $"{WorkflowConstants.ApiEndpoints.KnowledgeLatestSystem}?" +
                      $"name={UrlEncoder.Default.Encode(knowledgeName)}" +
                      $"&agent={UrlEncoder.Default.Encode(agentName)}";

        if (!string.IsNullOrEmpty(activationName))
        {
            endpoint += $"&activationName={UrlEncoder.Default.Encode(activationName)}";
        }

        _logger.LogDebug(
            "Fetching system knowledge from server: Name={Name}, Agent={Agent}, ActivationName={ActivationName}",
            knowledgeName,
            agentName,
            activationName);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogDebug(
                "System knowledge not found: Name={Name}, Agent={Agent}",
                knowledgeName,
                agentName);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Failed to fetch system knowledge: StatusCode={StatusCode}, Error={Error}",
                response.StatusCode,
                error);
            throw new HttpRequestException(
                $"Failed to fetch system knowledge '{knowledgeName}'. Status: {response.StatusCode}");
        }

        var knowledge = await response.Content.ReadFromJsonAsync<Models.Knowledge>(cancellationToken);

        if (knowledge == null)
        {
            _logger.LogWarning(
                "System knowledge response deserialization returned null: Name={Name}, Agent={Agent}",
                knowledgeName,
                agentName);
            return null;
        }

        _cacheService?.SetKnowledge(cacheKey, knowledge);

        _logger.LogDebug(
            "System knowledge fetched successfully: Name={Name}",
            knowledgeName);

        return knowledge;
    }

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
        // Validate inputs
        ValidationHelper.ValidateRequiredWithMaxLength(knowledgeName, nameof(knowledgeName), 256);
        ValidationHelper.ValidateRequiredWithMaxLength(agentName, nameof(agentName), 256);
        ValidationHelper.ValidateRequired(content, nameof(content));
        if (!systemScoped)
        {
            ValidationHelper.ValidateRequired(tenantId, nameof(tenantId));
        }

        // Build knowledge object
        var knowledge = new Models.Knowledge
        {
            Name = knowledgeName,
            Content = content,
            Type = type,
            Agent = agentName,
            SystemScoped = systemScoped,
            Description = description,
            Visible = visible
        };

        _logger.LogDebug(
            "Updating knowledge: Name={Name}, Agent={Agent}, Type={Type}, SystemScoped={SystemScoped}, ContentLength={Length}, ActivationName={ActivationName}",
            knowledgeName,
            agentName,
            type,
            systemScoped,
            content.Length,
            activationName);

        // POST to api/agent/knowledge
        var endpoint = WorkflowConstants.ApiEndpoints.Knowledge;

        if (!string.IsNullOrEmpty(activationName))
        {
            endpoint += $"?activationName={UrlEncoder.Default.Encode(activationName)}";
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Content = JsonContent.Create(knowledge);
        httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, tenantId);

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
        var cacheKey = GetCacheKey(tenantId, agentName, activationName, knowledgeName);
        _cacheService?.RemoveKnowledge(cacheKey);

        _logger.LogDebug(
            "Knowledge updated successfully: Name={Name}",
            knowledgeName);

        return true;
    }

    public async Task<bool> DeleteAsync(
        string knowledgeName,
        string agentName,
        string? tenantId,
        string? activationName,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        ValidationHelper.ValidateRequiredWithMaxLength(knowledgeName, nameof(knowledgeName), 256);
        ValidationHelper.ValidateRequiredWithMaxLength(agentName, nameof(agentName), 256);

        // Build URL
        var endpoint = $"{WorkflowConstants.ApiEndpoints.Knowledge}?" +
                      $"name={UrlEncoder.Default.Encode(knowledgeName)}" +
                      $"&agent={UrlEncoder.Default.Encode(agentName)}";

        if (!string.IsNullOrEmpty(activationName))
        {
            endpoint += $"&activationName={UrlEncoder.Default.Encode(activationName)}";
        }

        _logger.LogDebug(
            "Deleting knowledge: Name={Name}, Agent={Agent}, Tenant={Tenant}, ActivationName={ActivationName}",
            knowledgeName,
            agentName,
            tenantId,
            activationName);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Delete, endpoint);
        httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, tenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        // Handle 404 as already deleted
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogDebug(
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
        var cacheKey = GetCacheKey(tenantId, agentName, activationName, knowledgeName);
        _cacheService?.RemoveKnowledge(cacheKey);

        _logger.LogDebug(
            "Knowledge deleted successfully: Name={Name}",
            knowledgeName);

        return true;
    }

    public async Task<List<Models.Knowledge>> ListAsync(
        string agentName,
        string? tenantId,
        string? activationName,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        ValidationHelper.ValidateRequiredWithMaxLength(agentName, nameof(agentName), 256);

        // Build URL
        var endpoint = $"{WorkflowConstants.ApiEndpoints.KnowledgeList}?" +
                      $"agent={UrlEncoder.Default.Encode(agentName)}";

        if (!string.IsNullOrEmpty(activationName))
        {
            endpoint += $"&activationName={UrlEncoder.Default.Encode(activationName)}";
        }

        _logger.LogDebug(
            "Listing knowledge: Agent={Agent}, Tenant={Tenant}, ActivationName={ActivationName}",
            agentName,
            tenantId,
            activationName);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, tenantId);

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

        _logger.LogDebug(
            "Knowledge list fetched successfully: Count={Count}",
            knowledgeList.Count);

        return knowledgeList;
    }

    /// <summary>
    /// Generates a cache key for knowledge items.
    /// Format: "knowledge:{tenantId}:{agentName}:{activationName}:{knowledgeName}"
    /// </summary>
    private string GetCacheKey(string? tenantId, string agentName, string? activationName, string knowledgeName)
    {
        return $"knowledge:{tenantId}:{agentName}:{activationName}:{knowledgeName}";
    }
}
