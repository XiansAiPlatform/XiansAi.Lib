using System.Net;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common;
using Xians.Lib.Temporal.Workflows.Activations;

namespace Xians.Lib.Agents.Workflows;

/// <summary>
/// Logger helper class for ActivationValidationService (needed because static classes can't be used as generic type arguments).
/// </summary>
internal class ActivationValidationServiceLogger { }

/// <summary>
/// Validates that a target activation exists (and is active) on the server before a workflow
/// is started under it. Without this check, Temporal would create the workflow on a task queue
/// no worker listens on, leaving an orphaned "Running" workflow.
/// </summary>
internal static class ActivationValidationService
{
    private static readonly ILogger _logger =
        Common.Infrastructure.LoggerFactory.CreateLogger<ActivationValidationServiceLogger>();

    /// <summary>
    /// Ensures the given activation exists and is active for the target agent in the current tenant.
    /// Resolves the shared HTTP service from the target agent when it is registered in this process,
    /// otherwise from any registered agent. Skips validation (with a debug log) when no HTTP service
    /// is available (e.g. local mode), so local development keeps working.
    /// </summary>
    /// <param name="agentName">The target agent name (owner of the activation).</param>
    /// <param name="activationName">The activation name to validate.</param>
    /// <exception cref="ActivationNotFoundException">Thrown when the activation does not exist.</exception>
    /// <exception cref="ActivationDeactivatedException">Thrown when the activation exists but is deactivated.</exception>
    internal static async Task EnsureActivationActiveAsync(string agentName, string activationName)
    {
        var agent = ResolveAgentForHttpAccess(agentName);
        if (agent?.HttpService == null)
        {
            _logger.LogDebug(
                "Skipping activation validation for '{ActivationName}' (agent '{AgentName}') - no HTTP service available.",
                activationName,
                agentName);
            return;
        }

        var client = await agent.HttpService.GetHealthyClientAsync();
        var tenantId = XiansContext.SafeTenantId ?? agent.Options?.CertificateTenantId;

        await EnsureActivationActiveAsync(client, agentName, activationName, tenantId, agent.SystemScoped);
    }

    /// <summary>
    /// Core validation logic against the server's activation-exists endpoint.
    /// Definitive failures throw typed exceptions: <see cref="ActivationNotFoundException"/> (404),
    /// <see cref="ActivationDeactivatedException"/> (409), or <see cref="InvalidOperationException"/> (400).
    /// Other non-success responses throw <see cref="HttpRequestException"/> so callers
    /// (e.g. activity retry policies) can retry them.
    /// </summary>
    internal static async Task EnsureActivationActiveAsync(
        HttpClient client,
        string agentName,
        string activationName,
        string? tenantId,
        bool systemScoped)
    {
        var status = await CheckActivationStatusAsync(client, agentName, activationName, tenantId, systemScoped);

        switch (status)
        {
            case ActivationCheckStatus.Active:
                _logger.LogDebug(
                    "Activation '{ActivationName}' for agent '{AgentName}' validated successfully.",
                    activationName,
                    agentName);
                return;

            case ActivationCheckStatus.NotFound:
                throw new ActivationNotFoundException(agentName, activationName, tenantId);

            case ActivationCheckStatus.Deactivated:
                throw new ActivationDeactivatedException(agentName, activationName);
        }
    }

    /// <summary>
    /// Non-throwing variant of the core activation check. Returns a definitive
    /// <see cref="ActivationCheckStatus"/> for the expected outcomes (200 → <see cref="ActivationCheckStatus.Active"/>,
    /// 404 → <see cref="ActivationCheckStatus.NotFound"/>, 409 → <see cref="ActivationCheckStatus.Deactivated"/>).
    /// Genuine errors still throw: 400 → <see cref="InvalidOperationException"/>, and other non-success
    /// responses → <see cref="HttpRequestException"/> so retry policies can apply. Used both by
    /// <see cref="EnsureActivationActiveAsync(HttpClient, string, string, string?, bool)"/> (which maps the status to
    /// typed exceptions) and by the public agent self-info APIs that surface the status directly.
    /// </summary>
    internal static async Task<ActivationCheckStatus> CheckActivationStatusAsync(
        HttpClient client,
        string agentName,
        string activationName,
        string? tenantId,
        bool systemScoped,
        CancellationToken cancellationToken = default)
    {
        var url = $"{WorkflowConstants.ApiEndpoints.ActivationExists}" +
                  $"?activationName={Uri.EscapeDataString(activationName)}" +
                  $"&agentName={Uri.EscapeDataString(agentName)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (systemScoped && !string.IsNullOrWhiteSpace(tenantId))
        {
            request.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, tenantId);
        }

        var response = await client.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return ActivationCheckStatus.Active;
        }

        var errorContent = await response.Content.ReadAsStringAsync();

        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                return ActivationCheckStatus.NotFound;

            case HttpStatusCode.Conflict:
                return ActivationCheckStatus.Deactivated;

            case HttpStatusCode.BadRequest:
                throw new InvalidOperationException(
                    $"Activation validation failed for '{activationName}' (agent '{agentName}'). Error: {errorContent}");

            default:
                // Transient/server errors: surface as HttpRequestException so retry policies apply.
                throw new HttpRequestException(
                    $"Failed to validate activation '{activationName}' for agent '{agentName}'. " +
                    $"Status: {response.StatusCode}, Error: {errorContent}");
        }
    }

    /// <summary>
    /// Resolves an agent that provides HTTP access. Prefers the target agent when registered
    /// in this process (its SystemScoped flag drives tenant-header behavior); the HTTP service
    /// itself is shared across all agents on the platform.
    /// </summary>
    private static XiansAgent? ResolveAgentForHttpAccess(string agentName)
    {
        if (XiansContext.TryGetAgent(agentName, out var targetAgent) && targetAgent != null)
        {
            return targetAgent;
        }

        return XiansContext.GetAllAgents().FirstOrDefault();
    }
}
