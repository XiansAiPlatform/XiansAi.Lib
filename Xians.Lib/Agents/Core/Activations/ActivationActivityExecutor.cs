using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Workflows;
using Xians.Lib.Temporal;
using Xians.Lib.Temporal.Workflows.Activations;
using Xians.Lib.Temporal.Workflows.Activations.Models;

namespace Xians.Lib.Agents.Core.Activations;

/// <summary>
/// Context-aware executor for agent/activation SDK calls.
/// In a workflow the call is stubbed to <see cref="ActivationActivities"/>;
/// in an activity it uses <see cref="AgentActivationClient"/> HTTP directly.
/// </summary>
internal sealed class ActivationActivityExecutor : ContextAwareActivityExecutor<ActivationActivities, AgentActivationClient>
{
    private readonly XiansAgent _owner;
    private readonly string _agentName;

    public ActivationActivityExecutor(XiansAgent owner, string agentName, ILogger logger)
        : base(logger)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrWhiteSpace(agentName))
            throw new ArgumentException("Agent name is required.", nameof(agentName));

        _agentName = agentName;
    }

    protected override AgentActivationClient CreateService() => new(_owner, _agentName);

    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        var agentName = _agentName;
        return ExecuteAsync(
            act => act.AgentExistsAsync(agentName),
            svc => svc.ExistsAsync(cancellationToken),
            operationName: "AgentExists");
    }

    public Task<ActivationCheckStatus> GetActivationStatusAsync(
        string activationName,
        CancellationToken cancellationToken = default)
    {
        var agentName = _agentName;
        return ExecuteAsync(
            act => act.GetActivationStatusAsync(agentName, activationName),
            svc => svc.GetStatusAsync(activationName, cancellationToken),
            operationName: "GetActivationStatus");
    }

    public Task<List<ActivationInfo>> ListActivationsAsync(CancellationToken cancellationToken = default)
    {
        var agentName = _agentName;
        return ExecuteAsync(
            act => act.ListActivationsAsync(agentName),
            svc => svc.ListAsync(cancellationToken),
            operationName: "ListActivations");
    }

    public Task<ActivationInfo> CreateActivationAsync(
        string name,
        string? description,
        string? participantId,
        IEnumerable<WorkflowConfig>? workflows,
        CancellationToken cancellationToken = default)
    {
        var request = new ActivationCreateActivityRequest
        {
            AgentName = _agentName,
            Name = name,
            Description = description,
            ParticipantId = participantId,
            Workflows = workflows?.ToList()
        };

        return ExecuteAsync(
            act => act.CreateActivationAsync(request),
            svc => svc.CreateAsync(
                request.Name,
                request.Description,
                request.ParticipantId,
                request.Workflows,
                cancellationToken),
            operationName: "CreateActivation");
    }

    public Task<ActivationInfo> ActivateAsync(
        string activationId,
        IEnumerable<WorkflowConfig>? workflowConfiguration,
        CancellationToken cancellationToken = default)
    {
        var request = new ActivationActivateActivityRequest
        {
            AgentName = _agentName,
            ActivationId = activationId,
            Workflows = workflowConfiguration?.ToList()
        };

        return ExecuteAsync(
            act => act.ActivateActivationAsync(request),
            svc => svc.ActivateAsync(request.ActivationId, request.Workflows, cancellationToken),
            operationName: "ActivateActivation");
    }

    public Task<ActivationInfo> DeactivateAsync(
        string activationId,
        CancellationToken cancellationToken = default)
    {
        var agentName = _agentName;
        return ExecuteAsync(
            act => act.DeactivateActivationAsync(agentName, activationId),
            svc => svc.DeactivateAsync(activationId, cancellationToken),
            operationName: "DeactivateActivation");
    }
}
