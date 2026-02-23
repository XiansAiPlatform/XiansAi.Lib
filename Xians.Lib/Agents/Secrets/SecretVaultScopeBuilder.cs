using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Secrets.Models;
using Xians.Lib.Common;
using Xians.Lib.Common.Infrastructure;

namespace Xians.Lib.Agents.Secrets;

/// <summary>
/// Fluent scope builder for Secret Vault operations.
/// Set tenant, agent, and user scope via <see cref="TenantScope"/>, <see cref="AgentScope"/>, and <see cref="UserScope"/>,
/// then perform CRUD: <see cref="CreateAsync"/>, <see cref="FetchByKeyAsync"/>, <see cref="ListAsync"/>,
/// <see cref="GetByIdAsync"/>, <see cref="UpdateAsync"/>, <see cref="DeleteAsync"/>.
/// </summary>
public class SecretVaultScopeBuilder
{
    private readonly XiansAgent _agent;
    private readonly ILogger<SecretVaultScopeBuilder> _logger;
    private string? _tenantId;
    private string? _agentId;
    private string? _userId;

    internal SecretVaultScopeBuilder(XiansAgent agent, string? tenantId, string? agentId, string? userId)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _tenantId = tenantId;
        _agentId = agentId;
        _userId = userId;
        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<SecretVaultScopeBuilder>();
    }

    /// <summary>
    /// Sets the tenant scope for subsequent operations. Null = cross-tenant.
    /// </summary>
    public SecretVaultScopeBuilder TenantScope(string? tenantId)
    {
        _tenantId = tenantId;
        return this;
    }

    /// <summary>
    /// Sets the agent scope for subsequent operations. Null = across all agents.
    /// </summary>
    public SecretVaultScopeBuilder AgentScope(string? agentId)
    {
        _agentId = agentId;
        return this;
    }

    /// <summary>
    /// Sets the user scope for subsequent operations. Null = any user may access.
    /// </summary>
    public SecretVaultScopeBuilder UserScope(string? userId)
    {
        _userId = userId;
        return this;
    }

    /// <summary>
    /// Creates a secret with the current scope. Key must be unique.
    /// </summary>
    /// <param name="key">Unique secret key.</param>
    /// <param name="value">Secret value (encrypted at rest by the server).</param>
    /// <param name="additionalData">Optional flat key-value metadata (string, number, or boolean values only).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created secret (with decrypted value).</returns>
    public async Task<SecretVaultGetResponse> CreateAsync(
        string key,
        string value,
        object? additionalData = null,
        CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateRequiredWithMaxLength(key, nameof(key), 512);
        ValidationHelper.ValidateRequired(value, nameof(value));
        EnsureHttpService();

        var request = new SecretVaultCreateRequest
        {
            Key = key,
            Value = value,
            TenantId = _tenantId,
            AgentId = _agentId,
            UserId = _userId,
            AdditionalData = additionalData
        };

        var client = await _agent.HttpService!.GetHealthyClientAsync();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, WorkflowConstants.ApiEndpoints.Secrets);
        httpRequest.Content = JsonContent.Create(request);
        AddTenantHeader(httpRequest);

        var response = await client.SendAsync(httpRequest, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new InvalidOperationException("A secret with this key already exists.");
        if (!response.IsSuccessStatusCode)
            await ThrowForResponseAsync(response, "create secret");

        var result = await response.Content.ReadFromJsonAsync<SecretVaultGetResponse>(cancellationToken);
        return result ?? throw new InvalidOperationException("Server returned empty response for create secret.");
    }

    /// <summary>
    /// Fetches a secret by key with strict scope match. Returns decrypted value and optional additionalData only.
    /// </summary>
    /// <param name="key">Secret key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Value and additionalData, or null if not found or access denied.</returns>
    public async Task<SecretVaultFetchResponse?> FetchByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateRequiredWithMaxLength(key, nameof(key), 512);
        EnsureHttpService();

        var query = $"key={UrlEncoder.Default.Encode(key)}";
        if (_tenantId != null) query += $"&tenantId={UrlEncoder.Default.Encode(_tenantId)}";
        if (_agentId != null) query += $"&agentId={UrlEncoder.Default.Encode(_agentId)}";
        if (_userId != null) query += $"&userId={UrlEncoder.Default.Encode(_userId)}";

        var client = await _agent.HttpService!.GetHealthyClientAsync();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{WorkflowConstants.ApiEndpoints.Secrets}/fetch?{query}");
        AddTenantHeader(httpRequest);

        var response = await client.SendAsync(httpRequest, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (!response.IsSuccessStatusCode)
            await ThrowForResponseAsync(response, "fetch secret");

        return await response.Content.ReadFromJsonAsync<SecretVaultFetchResponse>(cancellationToken);
    }

    /// <summary>
    /// Lists secrets with optional tenant/agent filter (current scope values).
    /// </summary>
    public async Task<List<SecretVaultListItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        EnsureHttpService();

        var query = new List<string>();
        if (_tenantId != null) query.Add($"tenantId={UrlEncoder.Default.Encode(_tenantId)}");
        if (_agentId != null) query.Add($"agentId={UrlEncoder.Default.Encode(_agentId)}");
        var queryString = query.Count > 0 ? "?" + string.Join("&", query) : "";

        var client = await _agent.HttpService!.GetHealthyClientAsync();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{WorkflowConstants.ApiEndpoints.Secrets}{queryString}");
        AddTenantHeader(httpRequest);

        var response = await client.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowForResponseAsync(response, "list secrets");

        var list = await response.Content.ReadFromJsonAsync<List<SecretVaultListItem>>(cancellationToken);
        return list ?? new List<SecretVaultListItem>();
    }

    /// <summary>
    /// Gets a secret by id (full record including decrypted value).
    /// </summary>
    public async Task<SecretVaultGetResponse?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateRequired(id, nameof(id));
        EnsureHttpService();

        var client = await _agent.HttpService!.GetHealthyClientAsync();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{WorkflowConstants.ApiEndpoints.Secrets}/{UrlEncoder.Default.Encode(id)}");
        AddTenantHeader(httpRequest);

        var response = await client.SendAsync(httpRequest, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (!response.IsSuccessStatusCode)
            await ThrowForResponseAsync(response, "get secret");

        return await response.Content.ReadFromJsonAsync<SecretVaultGetResponse>(cancellationToken);
    }

    /// <summary>
    /// Updates a secret by id. Omitted properties leave existing values unchanged.
    /// </summary>
    public async Task<SecretVaultGetResponse> UpdateAsync(
        string id,
        string? value = null,
        object? additionalData = null,
        string? tenantId = null,
        string? agentId = null,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateRequired(id, nameof(id));
        EnsureHttpService();

        var request = new SecretVaultUpdateRequest
        {
            Value = value,
            AdditionalData = additionalData,
            TenantId = tenantId ?? _tenantId,
            AgentId = agentId ?? _agentId,
            UserId = userId ?? _userId
        };

        var client = await _agent.HttpService!.GetHealthyClientAsync();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Put, $"{WorkflowConstants.ApiEndpoints.Secrets}/{UrlEncoder.Default.Encode(id)}");
        httpRequest.Content = JsonContent.Create(request);
        AddTenantHeader(httpRequest);

        var response = await client.SendAsync(httpRequest, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException("Secret not found.");
        if (!response.IsSuccessStatusCode)
            await ThrowForResponseAsync(response, "update secret");

        var result = await response.Content.ReadFromJsonAsync<SecretVaultGetResponse>(cancellationToken);
        return result ?? throw new InvalidOperationException("Server returned empty response for update secret.");
    }

    /// <summary>
    /// Deletes a secret by id.
    /// </summary>
    /// <returns>True if deleted, false if not found.</returns>
    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateRequired(id, nameof(id));
        EnsureHttpService();

        var client = await _agent.HttpService!.GetHealthyClientAsync();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"{WorkflowConstants.ApiEndpoints.Secrets}/{UrlEncoder.Default.Encode(id)}");
        AddTenantHeader(httpRequest);

        var response = await client.SendAsync(httpRequest, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;
        if (!response.IsSuccessStatusCode)
            await ThrowForResponseAsync(response, "delete secret");

        return true;
    }

    private void EnsureHttpService()
    {
        if (_agent.HttpService == null)
            throw new InvalidOperationException("HTTP service is not configured. Secret Vault requires a connection to the Xians server.");
    }

    private void AddTenantHeader(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(_tenantId))
            request.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, _tenantId);
    }

    private async Task ThrowForResponseAsync(HttpResponseMessage response, string operation)
    {
        var body = await response.Content.ReadAsStringAsync();
        _logger.LogError("Secret Vault {Operation} failed: StatusCode={StatusCode}, Body={Body}", operation, response.StatusCode, body);
        throw new HttpRequestException($"Secret Vault {operation} failed. Status: {response.StatusCode}. {body}");
    }
}
