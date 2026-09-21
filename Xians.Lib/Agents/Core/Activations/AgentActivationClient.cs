using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Workflows;
using Xians.Lib.Common;
using Xians.Lib.Temporal.Workflows.Activations;

namespace Xians.Lib.Agents.Core.Activations;

/// <summary>
/// HTTP client for agent existence and activation management APIs.
/// Used directly from activities and by the workflow activity stub.
/// </summary>
internal sealed class AgentActivationClient
{
    private readonly XiansAgent _owner;
    private readonly string _agentName;
    private readonly ILogger _logger;

    public AgentActivationClient(XiansAgent owner, string agentName)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrWhiteSpace(agentName))
            throw new ArgumentException("Agent name is required.", nameof(agentName));

        _agentName = agentName;
        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<AgentActivationClient>();
    }

    public async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        EnsureHttpService();

        var url = $"{WorkflowConstants.ApiEndpoints.AgentExists}?agentName={Uri.EscapeDataString(_agentName)}";
        _logger.LogDebug("Checking agent existence for '{AgentName}'", _agentName);

        var client = await _owner.HttpService!.GetHealthyClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddTenantHeader(request);

        using var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return true;
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        await ThrowForResponseAsync(response, "check agent existence");
        return false;
    }

    public async Task<ActivationCheckStatus> GetStatusAsync(
        string activationName,
        CancellationToken cancellationToken = default)
    {
        EnsureHttpService();

        var client = await _owner.HttpService!.GetHealthyClientAsync();
        var tenantId = XiansContext.SafeTenantId ?? _owner.Options?.CertificateTenantId;

        return await ActivationValidationService.CheckActivationStatusAsync(
            client, _agentName, activationName, tenantId, _owner.SystemScoped, cancellationToken);
    }

    public async Task<List<ActivationInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        EnsureHttpService();

        var url = $"{WorkflowConstants.ApiEndpoints.Activations}?agentName={Uri.EscapeDataString(_agentName)}";
        _logger.LogDebug("Listing activations for agent '{AgentName}'", _agentName);

        var client = await _owner.HttpService!.GetHealthyClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddTenantHeader(request);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowForResponseAsync(response, "list activations");

        return await response.Content.ReadFromJsonAsync<List<ActivationInfo>>(cancellationToken)
               ?? new List<ActivationInfo>();
    }

    public async Task<ActivationInfo> CreateAsync(
        string name,
        string? description,
        string? participantId,
        IEnumerable<WorkflowConfig>? workflows,
        CancellationToken cancellationToken = default)
    {
        EnsureHttpService();

        var body = new CreateActivationBody
        {
            Name = name.Trim(),
            AgentName = _agentName,
            Description = description,
            ParticipantId = participantId,
            WorkflowConfiguration = workflows == null
                ? null
                : new ActivationWorkflowConfig { Workflows = workflows.ToList() }
        };

        _logger.LogDebug(
            "Creating activation '{ActivationName}' for agent '{AgentName}'",
            body.Name,
            _agentName);

        var client = await _owner.HttpService!.GetHealthyClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, WorkflowConstants.ApiEndpoints.Activations)
        {
            Content = JsonContent.Create(body, options: UnicodeJson.SerializerOptions)
        };
        AddTenantHeader(request);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowForResponseAsync(response, "create activation");

        return await response.Content.ReadFromJsonAsync<ActivationInfo>(cancellationToken)
               ?? throw new InvalidOperationException("Server returned an empty response for create activation.");
    }

    public async Task<ActivationInfo> ActivateAsync(
        string activationId,
        IEnumerable<WorkflowConfig>? workflowConfiguration,
        CancellationToken cancellationToken = default)
    {
        EnsureHttpService();

        var url = $"{WorkflowConstants.ApiEndpoints.Activations}/{Uri.EscapeDataString(activationId)}/activate";
        _logger.LogDebug(
            "Activating activation '{ActivationId}' for agent '{AgentName}'",
            activationId,
            _agentName);

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

    public async Task<ActivationInfo> DeactivateAsync(
        string activationId,
        CancellationToken cancellationToken = default)
    {
        EnsureHttpService();

        var url = $"{WorkflowConstants.ApiEndpoints.Activations}/{Uri.EscapeDataString(activationId)}/deactivate";
        _logger.LogDebug(
            "Deactivating activation '{ActivationId}' for agent '{AgentName}'",
            activationId,
            _agentName);

        var client = await _owner.HttpService!.GetHealthyClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        AddTenantHeader(request);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowForResponseAsync(response, "deactivate activation");

        return await ReadActivationFromEnvelopeAsync(response, cancellationToken);
    }

    private static async Task<ActivationInfo> ReadActivationFromEnvelopeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ActivationActionEnvelope>(cancellationToken);
        return envelope?.Activation
               ?? throw new InvalidOperationException(
                   "Server returned an empty activation in the activate/deactivate response.");
    }

    private void EnsureHttpService()
    {
        if (_owner.HttpService == null)
        {
            throw new InvalidOperationException(
                "HTTP service is not available. Cannot call agent/activation APIs.");
        }
    }

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
