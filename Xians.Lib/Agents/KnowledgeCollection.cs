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
    private readonly ILogger<KnowledgeCollection> _logger;

    internal KnowledgeCollection(XiansAgent agent, IHttpClientService? httpService)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _httpService = httpService;
        _logger = Common.LoggerFactory.CreateLogger<KnowledgeCollection>();
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
        ValidateInput(knowledgeName, nameof(knowledgeName));
        EnsureHttpService();

        try
        {
            // Build URL: api/agent/knowledge/latest?name={name}&agent={agent}
            var endpoint = $"api/agent/knowledge/latest?" +
                          $"name={UrlEncoder.Default.Encode(knowledgeName)}" +
                          $"&agent={UrlEncoder.Default.Encode(_agent.Name)}";

            _logger.LogDebug(
                "Fetching knowledge: Name={Name}, Agent={Agent}",
                knowledgeName,
                _agent.Name);

            var response = await _httpService!.Client.GetAsync(endpoint);

            // Handle 404 as knowledge not found
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation(
                    "Knowledge not found: Name={Name}, Agent={Agent}",
                    knowledgeName,
                    _agent.Name);
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

            var knowledge = await response.Content.ReadFromJsonAsync<Knowledge>();

            _logger.LogInformation(
                "Knowledge fetched successfully: Name={Name}",
                knowledgeName);

            return knowledge;
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            _logger.LogError(ex,
                "Error fetching knowledge: Name={Name}",
                knowledgeName);
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
        ValidateInput(knowledgeName, nameof(knowledgeName));
        
        // Validate content separately (no length limit)
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content cannot be null or empty", nameof(content));
        }
        
        EnsureHttpService();

        try
        {
            // Build knowledge object
            var knowledge = new Knowledge
            {
                Name = knowledgeName,
                Content = content,
                Type = type,
                Agent = _agent.Name,
                // TenantId will be set by the server from X-Tenant-Id header
            };

            _logger.LogDebug(
                "Updating knowledge: Name={Name}, Agent={Agent}, Type={Type}, ContentLength={Length}",
                knowledgeName,
                _agent.Name,
                type,
                content.Length);

            // POST to api/agent/knowledge
            var endpoint = "api/agent/knowledge";
            var response = await _httpService!.Client.PostAsJsonAsync(endpoint, knowledge);

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

            _logger.LogInformation(
                "Knowledge updated successfully: Name={Name}",
                knowledgeName);

            return true;
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            _logger.LogError(ex,
                "Error updating knowledge: Name={Name}",
                knowledgeName);
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
        ValidateInput(knowledgeName, nameof(knowledgeName));
        EnsureHttpService();

        try
        {
            // Build URL: api/agent/knowledge?name={name}&agent={agent}
            var endpoint = $"api/agent/knowledge?" +
                          $"name={UrlEncoder.Default.Encode(knowledgeName)}" +
                          $"&agent={UrlEncoder.Default.Encode(_agent.Name)}";

            _logger.LogDebug(
                "Deleting knowledge: Name={Name}, Agent={Agent}",
                knowledgeName,
                _agent.Name);

            var response = await _httpService!.Client.DeleteAsync(endpoint);

            // Handle 404 as already deleted
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation(
                    "Knowledge not found (already deleted?): Name={Name}, Agent={Agent}",
                    knowledgeName,
                    _agent.Name);
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

            _logger.LogInformation(
                "Knowledge deleted successfully: Name={Name}",
                knowledgeName);

            return true;
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            _logger.LogError(ex,
                "Error deleting knowledge: Name={Name}",
                knowledgeName);
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
        EnsureHttpService();

        try
        {
            // Build URL: api/agent/knowledge/list?agent={agent}
            var endpoint = $"api/agent/knowledge/list?" +
                          $"agent={UrlEncoder.Default.Encode(_agent.Name)}";

            _logger.LogDebug(
                "Listing knowledge: Agent={Agent}",
                _agent.Name);

            var response = await _httpService!.Client.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Failed to list knowledge: StatusCode={StatusCode}, Error={Error}",
                    response.StatusCode,
                    error);
                throw new HttpRequestException(
                    $"Failed to list knowledge for agent '{_agent.Name}'. Status: {response.StatusCode}");
            }

            var knowledgeList = await response.Content.ReadFromJsonAsync<List<Knowledge>>();

            _logger.LogInformation(
                "Knowledge list fetched successfully: Count={Count}",
                knowledgeList?.Count ?? 0);

            return knowledgeList ?? new List<Knowledge>();
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            _logger.LogError(ex,
                "Error listing knowledge for Agent={Agent}",
                _agent.Name);
            throw new InvalidOperationException($"Failed to list knowledge for agent '{_agent.Name}'", ex);
        }
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
    /// Ensures HTTP service is available.
    /// </summary>
    private void EnsureHttpService()
    {
        if (_httpService == null)
        {
            throw new InvalidOperationException(
                "HTTP service is not available. Ensure the agent was registered through XiansPlatform.Agents.");
        }
    }
}

