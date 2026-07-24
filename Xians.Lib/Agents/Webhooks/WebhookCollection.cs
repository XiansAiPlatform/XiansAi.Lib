using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Webhooks.Models;
using Xians.Lib.Common;
using Xians.Lib.Common.Infrastructure;

namespace Xians.Lib.Agents.Webhooks;

/// <summary>
/// Provides management of builtin (inbound) webhooks for an agent, scoped to the agent itself
/// ("self") in the calling certificate's tenant.
/// <para>
/// The agent name is always the owning agent, and the activation name is resolved automatically from
/// the current <see cref="XiansContext"/> when running inside a workflow/activity - so callers do not
/// need to pass them. Where an operation can run outside a specific activation context (listing), the
/// scope is broadened to all activations of the agent.
/// </para>
/// </summary>
/// <example>
/// <code>
/// // Create a webhook for the current activation (agent + activation resolved automatically)
/// var webhook = await agent.Webhooks.CreateAsync(webhookName: "EmailReceived");
/// Console.WriteLine(webhook.WebhookUrl);
///
/// // List this agent's webhooks (current activation when in context, otherwise all activations)
/// var all = await agent.Webhooks.ListAsync();
///
/// // Delete a webhook by id
/// await agent.Webhooks.DeleteAsync(webhook.Id);
/// </code>
/// </example>
public class WebhookCollection
{
    private readonly XiansAgent _agent;
    private readonly ILogger<WebhookCollection> _logger;

    internal WebhookCollection(XiansAgent agent)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<WebhookCollection>();
    }

    /// <summary>
    /// Lists builtin webhooks for this agent. When called inside a workflow/activity, results are scoped
    /// to the current activation; otherwise all activations of the agent are returned.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent's webhooks (empty list when none exist).</returns>
    /// <exception cref="InvalidOperationException">Thrown when the HTTP service is not configured.</exception>
    public async Task<List<WebhookInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        EnsureHttpService();

        var query = new List<string> { $"agentName={UrlEncoder.Default.Encode(_agent.Name)}" };
        var activationName = XiansContext.SafeIdPostfix;
        if (!string.IsNullOrEmpty(activationName))
        {
            query.Add($"activationName={UrlEncoder.Default.Encode(activationName)}");
        }

        var url = $"{WorkflowConstants.ApiEndpoints.AgentWebhooks}?{string.Join("&", query)}";

        _logger.LogDebug(
            "Listing webhooks for agent '{AgentName}'{ActivationScope}",
            _agent.Name,
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

    /// <summary>
    /// Creates a builtin webhook for this agent's current activation. The agent name and activation name
    /// are resolved automatically (activation from the current <see cref="XiansContext"/>).
    /// </summary>
    /// <param name="webhookName">Optional webhook name/scope (defaults to "Default" on the server).</param>
    /// <param name="workflowName">Optional target workflow name (defaults to "Integrator Workflow" on the server).</param>
    /// <param name="participantId">Optional participant id the webhook runs as (defaults to "webhook").</param>
    /// <param name="timeoutSeconds">Optional synchronous response timeout in seconds (1-300; server default 30).</param>
    /// <param name="name">Optional human-readable name for the webhook.</param>
    /// <param name="activationName">Optional explicit activation name. Defaults to the current activation from context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created webhook, including its <see cref="WebhookInfo.WebhookUrl"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the HTTP service is not configured or no activation can be resolved.</exception>
    public async Task<WebhookInfo> CreateAsync(
        string? webhookName = null,
        string? workflowName = null,
        string? participantId = null,
        int? timeoutSeconds = null,
        string? name = null,
        string? activationName = null,
        CancellationToken cancellationToken = default)
    {
        EnsureHttpService();

        var resolvedActivation = activationName ?? XiansContext.SafeIdPostfix
            ?? throw new InvalidOperationException(
                "Cannot create a webhook: no activation is available from the current context. " +
                "Call CreateAsync from within a workflow/activity, or pass an explicit activationName.");

        if (timeoutSeconds.HasValue)
        {
            ValidationHelper.ValidateRange(timeoutSeconds.Value, nameof(timeoutSeconds), 1, 300);
        }

        var body = new CreateBuiltinWebhookBody
        {
            AgentName = _agent.Name,
            ActivationName = resolvedActivation,
            Name = name,
            WorkflowName = workflowName,
            ParticipantId = participantId,
            TimeoutInSeconds = timeoutSeconds,
            WebhookName = webhookName
        };

        _logger.LogDebug(
            "Creating webhook for agent '{AgentName}', activation '{ActivationName}', webhookName '{WebhookName}'",
            _agent.Name,
            resolvedActivation,
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

    /// <summary>
    /// Deletes a builtin webhook by id (revokes its API key and removes the integration).
    /// </summary>
    /// <param name="id">The webhook id (see <see cref="WebhookInfo.Id"/>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted, false if not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the HTTP service is not configured.</exception>
    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateRequired(id, nameof(id));
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

    /// <summary>
    /// Adds the tenant header for system-scoped agents so the server can resolve the acting tenant.
    /// The tenant is taken from the current context (falling back to the certificate tenant).
    /// Mirrors the behavior of the activation and secret vault clients.
    /// </summary>
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
        // Keep the full server body in the log above, but throw a sanitized message so backend
        // implementation details aren't leaked if a caller surfaces ex.Message externally.
        throw new HttpRequestException($"Webhook {operation} failed. Status: {response.StatusCode}.");
    }

    /// <summary>Request body matching the server's builtin webhook creation contract.</summary>
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

    /// <summary>Envelope for the list endpoint response: <c>{ "webhooks": [...] }</c>.</summary>
    private sealed class WebhookListEnvelope
    {
        [JsonPropertyName("webhooks")]
        public List<WebhookInfo>? Webhooks { get; set; }
    }
}
