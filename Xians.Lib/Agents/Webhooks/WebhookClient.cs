using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Webhooks.Models;
using Xians.Lib.Common;

namespace Xians.Lib.Agents.Webhooks;

/// <summary>
/// HTTP client for builtin webhook management APIs.
/// Used directly from activities and by the workflow activity stub.
/// </summary>
internal sealed class WebhookClient
{
    private readonly XiansAgent _agent;
    private readonly ILogger _logger;

    public WebhookClient(XiansAgent agent)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<WebhookClient>();
    }

    public async Task<List<WebhookInfo>> ListAsync(
        string agentName,
        string? activationName,
        CancellationToken cancellationToken = default)
    {
        EnsureHttpService();

        var query = new List<string> { $"agentName={UrlEncoder.Default.Encode(agentName)}" };
        if (!string.IsNullOrEmpty(activationName))
        {
            query.Add($"activationName={UrlEncoder.Default.Encode(activationName)}");
        }

        var url = $"{WorkflowConstants.ApiEndpoints.AgentWebhooks}?{string.Join("&", query)}";

        _logger.LogDebug(
            "Listing webhooks for agent '{AgentName}'{ActivationScope}",
            agentName,
            string.IsNullOrEmpty(activationName) ? " (all activations)" : $" (activation '{activationName}')");

        var client = await _agent.HttpService!.GetHealthyClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddTenantHeader(request);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowForResponseAsync(response, "list webhooks");

        var envelope = await response.Content.ReadFromJsonAsync<WebhookListEnvelope>(cancellationToken);
        return envelope?.Webhooks ?? new List<WebhookInfo>();
    }

    public async Task<WebhookInfo> CreateAsync(
        string agentName,
        string activationName,
        string? webhookName,
        string? workflowName,
        string? participantId,
        int? timeoutSeconds,
        string? name,
        CancellationToken cancellationToken = default)
    {
        EnsureHttpService();

        var body = new CreateBuiltinWebhookBody
        {
            AgentName = agentName,
            ActivationName = activationName,
            Name = name,
            WorkflowName = workflowName,
            ParticipantId = participantId,
            TimeoutInSeconds = timeoutSeconds,
            WebhookName = webhookName
        };

        _logger.LogDebug(
            "Creating webhook for agent '{AgentName}', activation '{ActivationName}', webhookName '{WebhookName}'",
            agentName,
            activationName,
            webhookName ?? "Default");

        var client = await _agent.HttpService!.GetHealthyClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, WorkflowConstants.ApiEndpoints.AgentWebhooks)
        {
            Content = JsonContent.Create(body)
        };
        AddTenantHeader(request);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowForResponseAsync(response, "create webhook");

        var created = await response.Content.ReadFromJsonAsync<WebhookInfo>(cancellationToken);
        return created ?? throw new InvalidOperationException("Server returned an empty response for create webhook.");
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureHttpService();

        _logger.LogDebug("Deleting webhook '{WebhookId}' for agent '{AgentName}'", id, _agent.Name);

        var client = await _agent.HttpService!.GetHealthyClientAsync();
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{WorkflowConstants.ApiEndpoints.AgentWebhooks}/{UrlEncoder.Default.Encode(id)}");
        AddTenantHeader(request);

        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;
        if (!response.IsSuccessStatusCode)
            await ThrowForResponseAsync(response, "delete webhook");

        return true;
    }

    private void EnsureHttpService()
    {
        if (_agent.HttpService == null)
            throw new InvalidOperationException(
                "HTTP service is not configured. Webhook management requires a connection to the Xians server.");
    }

    private void AddTenantHeader(HttpRequestMessage request)
    {
        if (!_agent.SystemScoped)
            return;

        var tenantId = XiansContext.SafeTenantId ?? _agent.Options?.CertificateTenantId;
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            request.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, tenantId);
        }
    }

    private async Task ThrowForResponseAsync(HttpResponseMessage response, string operation)
    {
        var body = await response.Content.ReadAsStringAsync();
        _logger.LogError(
            "Webhook {Operation} failed: StatusCode={StatusCode}, Body={Body}",
            operation,
            response.StatusCode,
            body);
        throw new HttpRequestException($"Webhook {operation} failed. Status: {response.StatusCode}.");
    }

    private sealed class CreateBuiltinWebhookBody
    {
        [JsonPropertyName("agentName")]
        public required string AgentName { get; set; }

        [JsonPropertyName("activationName")]
        public required string ActivationName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("workflowName")]
        public string? WorkflowName { get; set; }

        [JsonPropertyName("participantId")]
        public string? ParticipantId { get; set; }

        [JsonPropertyName("timeoutInSeconds")]
        public int? TimeoutInSeconds { get; set; }

        [JsonPropertyName("webhookName")]
        public string? WebhookName { get; set; }
    }

    private sealed class WebhookListEnvelope
    {
        [JsonPropertyName("webhooks")]
        public List<WebhookInfo>? Webhooks { get; set; }
    }
}
