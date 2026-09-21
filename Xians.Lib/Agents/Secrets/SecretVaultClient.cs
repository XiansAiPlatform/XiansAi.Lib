using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Secrets.Models;
using Xians.Lib.Common;
using Xians.Lib.Temporal.Workflows.Secrets.Models;

namespace Xians.Lib.Agents.Secrets;

/// <summary>
/// HTTP client for Secret Vault CRUD. Used directly from activities and by the workflow activity stub.
/// </summary>
internal sealed class SecretVaultClient
{
    private readonly XiansAgent _agent;
    private readonly ILogger _logger;

    public SecretVaultClient(XiansAgent agent)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<SecretVaultClient>();
    }

    public async Task<SecretVaultGetResponse> CreateAsync(
        SecretVaultCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureHttpService();

        var client = await _agent.HttpService!.GetHealthyClientAsync();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, WorkflowConstants.ApiEndpoints.Secrets);
        httpRequest.Content = JsonContent.Create(request);
        AddTenantHeader(httpRequest, request.TenantId);

        var response = await client.SendAsync(httpRequest, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new InvalidOperationException("A secret with this key already exists.");
        if (!response.IsSuccessStatusCode)
            ThrowForResponse(response, "create secret");

        var result = await response.Content.ReadFromJsonAsync<SecretVaultGetResponse>(cancellationToken);
        return result ?? throw new InvalidOperationException("Server returned empty response for create secret.");
    }

    public async Task<SecretVaultFetchResponse?> FetchByKeyAsync(
        SecretVaultFetchActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureHttpService();

        var scope = request.Scope;
        var query = $"key={UrlEncoder.Default.Encode(request.Key)}";
        if (scope.TenantId != null) query += $"&tenantId={UrlEncoder.Default.Encode(scope.TenantId)}";
        if (scope.AgentId != null) query += $"&agentId={UrlEncoder.Default.Encode(scope.AgentId)}";
        if (scope.UserId != null) query += $"&userId={UrlEncoder.Default.Encode(scope.UserId)}";
        if (scope.ActivationName != null) query += $"&activationName={UrlEncoder.Default.Encode(scope.ActivationName)}";

        var client = await _agent.HttpService!.GetHealthyClientAsync();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{WorkflowConstants.ApiEndpoints.Secrets}/fetch?{query}");
        AddTenantHeader(httpRequest, scope.TenantId);

        var response = await client.SendAsync(httpRequest, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (!response.IsSuccessStatusCode)
            ThrowForResponse(response, "fetch secret");

        return await response.Content.ReadFromJsonAsync<SecretVaultFetchResponse>(cancellationToken);
    }

    public async Task<List<SecretVaultListItem>> ListAsync(
        SecretVaultScopePayload scope,
        CancellationToken cancellationToken = default)
    {
        EnsureHttpService();

        var query = new List<string>();
        if (scope.TenantId != null) query.Add($"tenantId={UrlEncoder.Default.Encode(scope.TenantId)}");
        if (scope.AgentId != null) query.Add($"agentId={UrlEncoder.Default.Encode(scope.AgentId)}");
        if (scope.ActivationName != null) query.Add($"activationName={UrlEncoder.Default.Encode(scope.ActivationName)}");
        var queryString = query.Count > 0 ? "?" + string.Join("&", query) : "";

        var client = await _agent.HttpService!.GetHealthyClientAsync();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{WorkflowConstants.ApiEndpoints.Secrets}{queryString}");
        AddTenantHeader(httpRequest, scope.TenantId);

        var response = await client.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
            ThrowForResponse(response, "list secrets");

        var list = await response.Content.ReadFromJsonAsync<List<SecretVaultListItem>>(cancellationToken);
        return list ?? new List<SecretVaultListItem>();
    }

    public async Task<SecretVaultGetResponse?> GetByIdAsync(
        SecretVaultIdActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureHttpService();

        var client = await _agent.HttpService!.GetHealthyClientAsync();
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{WorkflowConstants.ApiEndpoints.Secrets}/{UrlEncoder.Default.Encode(request.Id)}");
        AddTenantHeader(httpRequest, request.Scope.TenantId);

        var response = await client.SendAsync(httpRequest, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (!response.IsSuccessStatusCode)
            ThrowForResponse(response, "get secret");

        return await response.Content.ReadFromJsonAsync<SecretVaultGetResponse>(cancellationToken);
    }

    public async Task<SecretVaultGetResponse> UpdateAsync(
        SecretVaultUpdateActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureHttpService();

        var body = new SecretVaultUpdateRequest
        {
            Value = request.Value,
            AdditionalData = request.AdditionalData,
            TenantId = request.Scope.TenantId,
            AgentId = request.Scope.AgentId,
            UserId = request.Scope.UserId,
            ActivationName = request.Scope.ActivationName
        };

        var client = await _agent.HttpService!.GetHealthyClientAsync();
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Put,
            $"{WorkflowConstants.ApiEndpoints.Secrets}/{UrlEncoder.Default.Encode(request.Id)}");
        httpRequest.Content = JsonContent.Create(body);
        AddTenantHeader(httpRequest, request.Scope.TenantId);

        var response = await client.SendAsync(httpRequest, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException("Secret not found.");
        if (!response.IsSuccessStatusCode)
            ThrowForResponse(response, "update secret");

        var result = await response.Content.ReadFromJsonAsync<SecretVaultGetResponse>(cancellationToken);
        return result ?? throw new InvalidOperationException("Server returned empty response for update secret.");
    }

    public async Task<bool> DeleteAsync(
        SecretVaultIdActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureHttpService();

        var client = await _agent.HttpService!.GetHealthyClientAsync();
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{WorkflowConstants.ApiEndpoints.Secrets}/{UrlEncoder.Default.Encode(request.Id)}");
        AddTenantHeader(httpRequest, request.Scope.TenantId);

        var response = await client.SendAsync(httpRequest, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;
        if (!response.IsSuccessStatusCode)
            ThrowForResponse(response, "delete secret");

        return true;
    }

    private void EnsureHttpService()
    {
        if (_agent.HttpService == null)
            throw new InvalidOperationException(
                "HTTP service is not configured. Secret Vault requires a connection to the Xians server.");
    }

    private static void AddTenantHeader(HttpRequestMessage request, string? tenantId)
    {
        if (!string.IsNullOrEmpty(tenantId))
            request.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, tenantId);
    }

    private void ThrowForResponse(HttpResponseMessage response, string operation)
    {
        // Do not log or throw the response body: Secret Vault error payloads can echo the secret
        // value, and an activity exception is persisted in Temporal workflow history.
        _logger.LogError(
            "Secret Vault {Operation} failed: StatusCode={StatusCode}",
            operation,
            response.StatusCode);
        throw new HttpRequestException($"Secret Vault {operation} failed. Status: {response.StatusCode}.");
    }
}
