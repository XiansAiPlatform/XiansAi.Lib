using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core.Activations;
using Xians.Lib.Agents.Workflows;
using Xians.Lib.Common;
using Xians.Lib.Temporal.Workflows.Activations;

namespace Xians.Lib.Agents.Core;

/// <summary>
/// A reference to an agent in the current tenant, obtained via
/// <see cref="TenantAgents.Agent(string)"/>. Lets the calling agent inspect whether that
/// agent (and a given activation of it) exists and is active, and manage its activations,
/// without requiring the target agent to be registered in this process.
/// </summary>
/// <example>
/// <code>
/// var other = agent.Tenant.Agent("Fraud Detection Agent");
/// bool agentExists = await other.ExistsAsync();
/// ActivationCheckStatus status = await other.GetActivationStatusAsync("fraud-eu");
/// bool activationActive = await other.ActivationExistsAsync("fraud-eu");
///
/// var created = await other.CreateActivationAsync(name: "fraud-eu");
/// await created.ActivateAsync();
/// await created.DeactivateAsync();
/// </code>
/// </example>
public class AgentReference
{
    private readonly XiansAgent _owner;
    private readonly ILogger<AgentReference> _logger;

    /// <summary>
    /// Gets the name of the referenced agent.
    /// </summary>
    public string Name { get; }

    internal AgentReference(XiansAgent owner, string agentName)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrWhiteSpace(agentName))
            throw new ArgumentException("Agent name is required.", nameof(agentName));

        Name = IdentifierSanitizer.SanitizeAndValidateAgentName(agentName, nameof(agentName));

        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<AgentReference>();
    }

    /// <summary>
    /// Checks whether the referenced agent exists in the current tenant
    /// (<c>GET /api/agent/agents/exists</c>).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the agent exists; false if the server reports 404.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the HTTP service is not available
    /// or the server rejects the request (400).</exception>
    /// <exception cref="HttpRequestException">Thrown for transient/server errors so retry policies can apply.</exception>
    public async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        EnsureHttpService();

        var url = $"{WorkflowConstants.ApiEndpoints.AgentExists}?agentName={Uri.EscapeDataString(Name)}";
        _logger.LogDebug("Checking agent existence for '{AgentName}'", Name);

        var client = await _owner.HttpService!.GetHealthyClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddTenantHeader(request);

        using var response = await client.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
            return true;

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        await ThrowForResponseAsync(response, "check agent existence");
        return false; // unreachable
    }

    /// <summary>
    /// Checks whether the referenced agent has an activation of the given name in the current tenant.
    /// </summary>
    /// <param name="activationName">The activation name to check (required - there is no in-process
    /// context for another agent).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The activation status.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the HTTP service is not available
    /// or the server rejects the request (400).</exception>
    /// <exception cref="HttpRequestException">Thrown for transient/server errors so retry policies can apply.</exception>
    public async Task<ActivationCheckStatus> GetActivationStatusAsync(
        string activationName,
        CancellationToken cancellationToken = default)
    {
        var sanitizedActivationName = IdentifierSanitizer.SanitizeAndValidateActivationName(
            activationName, nameof(activationName));

        EnsureHttpService();

        var client = await _owner.HttpService!.GetHealthyClientAsync();
        var tenantId = XiansContext.SafeTenantId ?? _owner.Options?.CertificateTenantId;

        return await ActivationValidationService.CheckActivationStatusAsync(
            client, Name, sanitizedActivationName, tenantId, _owner.SystemScoped, cancellationToken);
    }

    /// <summary>
    /// Returns true when the referenced agent has an active activation of the given name.
    /// A missing or deactivated activation returns false.
    /// </summary>
    /// <param name="activationName">The activation name to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the activation exists and is active; otherwise false.</returns>
    public async Task<bool> ActivationExistsAsync(
        string activationName,
        CancellationToken cancellationToken = default)
    {
        return await GetActivationStatusAsync(activationName, cancellationToken) == ActivationCheckStatus.Active;
    }

    /// <summary>
    /// Lists activations for the referenced agent in the current tenant.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent's activations (empty list when none exist).</returns>
    public async Task<List<ActivationInfo>> ListActivationsAsync(CancellationToken cancellationToken = default)
    {
        EnsureHttpService();

        var url = $"{WorkflowConstants.ApiEndpoints.Activations}?agentName={Uri.EscapeDataString(Name)}";
        _logger.LogDebug("Listing activations for agent '{AgentName}'", Name);

        var client = await _owner.HttpService!.GetHealthyClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddTenantHeader(request);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowForResponseAsync(response, "list activations");

        var list = await response.Content.ReadFromJsonAsync<List<ActivationInfo>>(cancellationToken)
                   ?? new List<ActivationInfo>();
        foreach (var item in list)
        {
            item.Bind(this);
        }

        return list;
    }

    /// <summary>
    /// Creates a new (inactive) activation for the referenced agent in the current tenant.
    /// Call <see cref="ActivationInfo.ActivateAsync"/> on the returned handle to start its workflows.
    /// </summary>
    /// <param name="name">Activation name (idPostfix).</param>
    /// <param name="description">Optional description.</param>
    /// <param name="participantId">Optional participant id.</param>
    /// <param name="workflows">Optional workflow configurations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created activation (bound for further activate/deactivate calls).</returns>
    public async Task<ActivationInfo> CreateActivationAsync(
        string name,
        string? description = null,
        string? participantId = null,
        IEnumerable<WorkflowConfig>? workflows = null,
        CancellationToken cancellationToken = default)
    {
        var sanitizedName = IdentifierSanitizer.SanitizeAndValidateActivationName(name, nameof(name));

        EnsureHttpService();

        var body = new CreateActivationBody
        {
            Name = sanitizedName,
            AgentName = Name,
            Description = description,
            ParticipantId = participantId,
            WorkflowConfiguration = workflows == null
                ? null
                : new ActivationWorkflowConfig { Workflows = workflows.ToList() }
        };

        _logger.LogDebug(
            "Creating activation '{ActivationName}' for agent '{AgentName}'",
            body.Name,
            Name);

        var client = await _owner.HttpService!.GetHealthyClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, WorkflowConstants.ApiEndpoints.Activations)
        {
            Content = JsonContent.Create(body, options: UnicodeJson.SerializerOptions)
        };
        AddTenantHeader(request);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowForResponseAsync(response, "create activation");

        var created = await response.Content.ReadFromJsonAsync<ActivationInfo>(cancellationToken)
                      ?? throw new InvalidOperationException("Server returned an empty response for create activation.");
        created.Bind(this);
        return created;
    }

    /// <summary>
    /// Activates an activation by id (starts its workflows) in the current tenant.
    /// Prefer calling <see cref="ActivationInfo.ActivateAsync"/> on a handle from list/create when available.
    /// </summary>
    /// <param name="activationId">The activation id (see <see cref="ActivationInfo.Id"/>).</param>
    /// <param name="workflowConfiguration">Optional workflow configuration override for this activate call.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated activation returned by the server.</returns>
    public async Task<ActivationInfo> ActivateAsync(
        string activationId,
        IEnumerable<WorkflowConfig>? workflowConfiguration = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(activationId))
            throw new ArgumentException("Activation id is required.", nameof(activationId));

        EnsureHttpService();

        var url = $"{WorkflowConstants.ApiEndpoints.Activations}/{Uri.EscapeDataString(activationId)}/activate";
        _logger.LogDebug(
            "Activating activation '{ActivationId}' for agent '{AgentName}'",
            activationId,
            Name);

        HttpContent? content = null;
        if (workflowConfiguration != null)
        {
            content = JsonContent.Create(new ActivateBody
            {
                WorkflowConfiguration = new ActivationWorkflowConfig
                {
                    Workflows = workflowConfiguration.ToList()
                }
            });
        }

        var client = await _owner.HttpService!.GetHealthyClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        AddTenantHeader(request);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowForResponseAsync(response, "activate activation");

        return await ReadActivationFromEnvelopeAsync(response, cancellationToken);
    }

    /// <summary>
    /// Deactivates an activation by id (cancels its workflows) in the current tenant.
    /// Prefer calling <see cref="ActivationInfo.DeactivateAsync"/> on a handle from list/create when available.
    /// </summary>
    /// <param name="activationId">The activation id (see <see cref="ActivationInfo.Id"/>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated activation returned by the server.</returns>
    public async Task<ActivationInfo> DeactivateAsync(
        string activationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(activationId))
            throw new ArgumentException("Activation id is required.", nameof(activationId));

        EnsureHttpService();

        var url = $"{WorkflowConstants.ApiEndpoints.Activations}/{Uri.EscapeDataString(activationId)}/deactivate";
        _logger.LogDebug(
            "Deactivating activation '{ActivationId}' for agent '{AgentName}'",
            activationId,
            Name);

        var client = await _owner.HttpService!.GetHealthyClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        AddTenantHeader(request);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowForResponseAsync(response, "deactivate activation");

        return await ReadActivationFromEnvelopeAsync(response, cancellationToken);
    }

    private async Task<ActivationInfo> ReadActivationFromEnvelopeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ActivationActionEnvelope>(cancellationToken);
        var activation = envelope?.Activation
                         ?? throw new InvalidOperationException(
                             "Server returned an empty activation in the activate/deactivate response.");
        activation.Bind(this);
        return activation;
    }

    private void EnsureHttpService()
    {
        if (_owner.HttpService == null)
        {
            throw new InvalidOperationException(
                "HTTP service is not available. Cannot call agent/activation APIs.");
        }
    }

    /// <summary>
    /// Adds the tenant header for system-scoped owners so the server can resolve the acting tenant.
    /// Mirrors the behavior of the webhook and activation-validation clients.
    /// </summary>
    private void AddTenantHeader(HttpRequestMessage request)
    {
        if (!_owner.SystemScoped)
            return;

        var tenantId = XiansContext.SafeTenantId ?? _owner.Options?.CertificateTenantId;
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            request.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, tenantId);
        }
    }

    private async Task ThrowForResponseAsync(HttpResponseMessage response, string operation)
    {
        var body = await response.Content.ReadAsStringAsync();
        _logger.LogError(
            "Agent/activation {Operation} failed: StatusCode={StatusCode}, Body={Body}",
            operation,
            response.StatusCode,
            body);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new InvalidOperationException(
                $"Agent/activation {operation} failed. Status: {response.StatusCode}. {body}");
        }

        throw new HttpRequestException(
            $"Agent/activation {operation} failed. Status: {response.StatusCode}. {body}");
    }

    private sealed class CreateActivationBody
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("agentName")]
        public required string AgentName { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("participantId")]
        public string? ParticipantId { get; set; }

        [JsonPropertyName("workflowConfiguration")]
        public ActivationWorkflowConfig? WorkflowConfiguration { get; set; }
    }

    private sealed class ActivateBody
    {
        [JsonPropertyName("workflowConfiguration")]
        public ActivationWorkflowConfig? WorkflowConfiguration { get; set; }
    }

    private sealed class ActivationActionEnvelope
    {
        [JsonPropertyName("activation")]
        public ActivationInfo? Activation { get; set; }
    }
}
