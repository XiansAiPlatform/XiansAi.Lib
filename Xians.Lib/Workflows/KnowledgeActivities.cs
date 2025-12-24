using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using Temporalio.Activities;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Models;
using Xians.Lib.Workflows.Models;

namespace Xians.Lib.Workflows;

/// <summary>
/// Activities for managing knowledge in the Xians platform.
/// Activities can perform non-deterministic operations like HTTP calls.
/// </summary>
public class KnowledgeActivities
{
    private readonly HttpClient _httpClient;

    public KnowledgeActivities(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Retrieves knowledge by name from the server.
    /// </summary>
    [Activity]
    public async Task<Knowledge?> GetKnowledgeAsync(GetKnowledgeRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "GetKnowledge activity started: Name={Name}, Agent={Agent}, Tenant={Tenant}",
            request.KnowledgeName,
            request.AgentName,
            request.TenantId);

        try
        {
            // Validate inputs
            ValidateInput(request.KnowledgeName, nameof(request.KnowledgeName));
            ValidateInput(request.AgentName, nameof(request.AgentName));

            // Build URL: api/agent/knowledge/latest?name={name}&agent={agent}
            var endpoint = $"api/agent/knowledge/latest?" +
                          $"name={UrlEncoder.Default.Encode(request.KnowledgeName)}" +
                          $"&agent={UrlEncoder.Default.Encode(request.AgentName)}";

            ActivityExecutionContext.Current.Logger.LogTrace(
                "Fetching knowledge from {Endpoint}",
                endpoint);

            // Create HTTP request with tenant header
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
            httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", request.TenantId);

            var response = await _httpClient.SendAsync(httpRequest);

            // Handle 404 as knowledge not found
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                ActivityExecutionContext.Current.Logger.LogInformation(
                    "Knowledge not found: Name={Name}, Agent={Agent}",
                    request.KnowledgeName,
                    request.AgentName);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                ActivityExecutionContext.Current.Logger.LogError(
                    "Failed to fetch knowledge: StatusCode={StatusCode}, Error={Error}",
                    response.StatusCode,
                    error);
                throw new HttpRequestException(
                    $"Failed to fetch knowledge. Status: {response.StatusCode}");
            }

            var knowledge = await response.Content.ReadFromJsonAsync<Knowledge>();

            ActivityExecutionContext.Current.Logger.LogInformation(
                "Knowledge fetched successfully: Name={Name}",
                request.KnowledgeName);

            return knowledge;
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error fetching knowledge: Name={Name}",
                request.KnowledgeName);
            throw;
        }
    }

    /// <summary>
    /// Updates or creates knowledge on the server.
    /// </summary>
    [Activity]
    public async Task<bool> UpdateKnowledgeAsync(UpdateKnowledgeRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "UpdateKnowledge activity started: Name={Name}, Agent={Agent}, Type={Type}, Tenant={Tenant}",
            request.KnowledgeName,
            request.AgentName,
            request.Type,
            request.TenantId);

        try
        {
            // Validate inputs
            ValidateInput(request.KnowledgeName, nameof(request.KnowledgeName));
            ValidateInput(request.Content, nameof(request.Content));
            ValidateInput(request.AgentName, nameof(request.AgentName));

            // Build knowledge object
            var knowledge = new Knowledge
            {
                Name = request.KnowledgeName,
                Content = request.Content,
                Type = request.Type,
                Agent = request.AgentName,
                TenantId = request.TenantId
            };

            // POST to api/agent/knowledge
            var endpoint = "api/agent/knowledge";

            ActivityExecutionContext.Current.Logger.LogTrace(
                "Posting knowledge to {Endpoint}: ContentLength={ContentLength}",
                endpoint,
                request.Content.Length);

            // Create HTTP request with tenant header
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
            httpRequest.Content = JsonContent.Create(knowledge);
            httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", request.TenantId);

            var response = await _httpClient.SendAsync(httpRequest);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                ActivityExecutionContext.Current.Logger.LogError(
                    "Failed to update knowledge: StatusCode={StatusCode}, Error={Error}",
                    response.StatusCode,
                    error);
                throw new HttpRequestException(
                    $"Failed to update knowledge. Status: {response.StatusCode}");
            }

            ActivityExecutionContext.Current.Logger.LogInformation(
                "Knowledge updated successfully: Name={Name}",
                request.KnowledgeName);

            return true;
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error updating knowledge: Name={Name}",
                request.KnowledgeName);
            throw;
        }
    }

    /// <summary>
    /// Deletes knowledge from the server.
    /// </summary>
    [Activity]
    public async Task<bool> DeleteKnowledgeAsync(DeleteKnowledgeRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "DeleteKnowledge activity started: Name={Name}, Agent={Agent}, Tenant={Tenant}",
            request.KnowledgeName,
            request.AgentName,
            request.TenantId);

        try
        {
            // Validate inputs
            ValidateInput(request.KnowledgeName, nameof(request.KnowledgeName));
            ValidateInput(request.AgentName, nameof(request.AgentName));

            // Build URL: api/agent/knowledge?name={name}&agent={agent}
            var endpoint = $"api/agent/knowledge?" +
                          $"name={UrlEncoder.Default.Encode(request.KnowledgeName)}" +
                          $"&agent={UrlEncoder.Default.Encode(request.AgentName)}";

            ActivityExecutionContext.Current.Logger.LogTrace(
                "Deleting knowledge from {Endpoint}",
                endpoint);

            // Create HTTP request with tenant header
            using var httpRequest = new HttpRequestMessage(HttpMethod.Delete, endpoint);
            httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", request.TenantId);

            var response = await _httpClient.SendAsync(httpRequest);

            // Handle 404 as already deleted
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                ActivityExecutionContext.Current.Logger.LogInformation(
                    "Knowledge not found (already deleted?): Name={Name}, Agent={Agent}",
                    request.KnowledgeName,
                    request.AgentName);
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                ActivityExecutionContext.Current.Logger.LogError(
                    "Failed to delete knowledge: StatusCode={StatusCode}, Error={Error}",
                    response.StatusCode,
                    error);
                throw new HttpRequestException(
                    $"Failed to delete knowledge. Status: {response.StatusCode}");
            }

            ActivityExecutionContext.Current.Logger.LogInformation(
                "Knowledge deleted successfully: Name={Name}",
                request.KnowledgeName);

            return true;
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error deleting knowledge: Name={Name}",
                request.KnowledgeName);
            throw;
        }
    }

    /// <summary>
    /// Lists all knowledge for an agent.
    /// </summary>
    [Activity]
    public async Task<List<Knowledge>> ListKnowledgeAsync(ListKnowledgeRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "ListKnowledge activity started: Agent={Agent}, Tenant={Tenant}",
            request.AgentName,
            request.TenantId);

        try
        {
            // Validate inputs
            ValidateInput(request.AgentName, nameof(request.AgentName));

            // Build URL: api/agent/knowledge/list?agent={agent}
            var endpoint = $"api/agent/knowledge/list?" +
                          $"agent={UrlEncoder.Default.Encode(request.AgentName)}";

            ActivityExecutionContext.Current.Logger.LogTrace(
                "Listing knowledge from {Endpoint}",
                endpoint);

            // Create HTTP request with tenant header
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
            httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", request.TenantId);

            var response = await _httpClient.SendAsync(httpRequest);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                ActivityExecutionContext.Current.Logger.LogError(
                    "Failed to list knowledge: StatusCode={StatusCode}, Error={Error}",
                    response.StatusCode,
                    error);
                throw new HttpRequestException(
                    $"Failed to list knowledge. Status: {response.StatusCode}");
            }

            var knowledgeList = await response.Content.ReadFromJsonAsync<List<Knowledge>>();

            ActivityExecutionContext.Current.Logger.LogInformation(
                "Knowledge list fetched successfully: Count={Count}",
                knowledgeList?.Count ?? 0);

            return knowledgeList ?? new List<Knowledge>();
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error listing knowledge for Agent={Agent}",
                request.AgentName);
            throw;
        }
    }

    /// <summary>
    /// Validates input parameters to prevent invalid data.
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
}

