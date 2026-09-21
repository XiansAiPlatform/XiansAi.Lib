using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Webhooks.Models;
using Xians.Lib.Temporal;
using Xians.Lib.Temporal.Workflows.Webhooks;
using Xians.Lib.Temporal.Workflows.Webhooks.Models;

namespace Xians.Lib.Agents.Webhooks;

/// <summary>
/// Context-aware executor for webhook management SDK calls.
/// In a workflow the call is stubbed to <see cref="WebhookActivities"/>;
/// in an activity it uses <see cref="WebhookClient"/> HTTP directly.
/// </summary>
internal sealed class WebhookActivityExecutor : ContextAwareActivityExecutor<WebhookActivities, WebhookClient>
{
    private readonly XiansAgent _agent;

    public WebhookActivityExecutor(XiansAgent agent, ILogger logger)
        : base(logger)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
    }

    protected override WebhookClient CreateService() => new(_agent);

    public Task<List<WebhookInfo>> ListAsync(
        string? activationName,
        CancellationToken cancellationToken = default)
    {
        var request = new WebhookListActivityRequest
        {
            AgentName = _agent.Name,
            ActivationName = activationName
        };

        return ExecuteAsync(
            act => act.ListWebhooksAsync(request),
            svc => svc.ListAsync(request.AgentName, request.ActivationName, cancellationToken),
            operationName: "ListWebhooks");
    }

    public Task<WebhookInfo> CreateAsync(
        string activationName,
        string? webhookName,
        string? workflowName,
        string? participantId,
        int? timeoutSeconds,
        string? name,
        CancellationToken cancellationToken = default)
    {
        var request = new WebhookCreateActivityRequest
        {
            AgentName = _agent.Name,
            ActivationName = activationName,
            WebhookName = webhookName,
            WorkflowName = workflowName,
            ParticipantId = participantId,
            TimeoutSeconds = timeoutSeconds,
            Name = name
        };

        return ExecuteAsync(
            act => act.CreateWebhookAsync(request),
            svc => svc.CreateAsync(
                request.AgentName,
                request.ActivationName,
                request.WebhookName,
                request.WorkflowName,
                request.ParticipantId,
                request.TimeoutSeconds,
                request.Name,
                cancellationToken),
            operationName: "CreateWebhook");
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            act => act.DeleteWebhookAsync(id),
            svc => svc.DeleteAsync(id, cancellationToken),
            operationName: "DeleteWebhook");
    }
}
