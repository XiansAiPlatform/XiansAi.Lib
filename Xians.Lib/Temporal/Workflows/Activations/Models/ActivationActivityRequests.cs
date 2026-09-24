using Xians.Lib.Agents.Core.Activations;

namespace Xians.Lib.Temporal.Workflows.Activations.Models;

/// <summary>
/// Activity payload for creating an activation from workflow code.
/// </summary>
public class ActivationCreateActivityRequest
{
    /// <summary>Name of the agent that will own the activation.</summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>Activation name (idPostfix).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>Optional participant id the activation should run as.</summary>
    public string? ParticipantId { get; set; }

    /// <summary>Optional workflow configurations.</summary>
    public List<WorkflowConfig>? Workflows { get; set; }
}

/// <summary>
/// Activity payload for activating an activation from workflow code.
/// </summary>
public class ActivationActivateActivityRequest
{
    /// <summary>Name of the agent that owns the activation.</summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>Activation id (see <c>ActivationInfo.Id</c>).</summary>
    public string ActivationId { get; set; } = string.Empty;

    /// <summary>Optional workflow configuration override for this activate call.</summary>
    public List<WorkflowConfig>? Workflows { get; set; }
}
