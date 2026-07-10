namespace Xians.Lib.Agents.Workflows;

/// <summary>
/// Thrown when a target activation does not exist for an agent in the acting tenant
/// (e.g. the agent is not activated in that tenant).
/// Derives from <see cref="InvalidOperationException"/> for backward compatibility with
/// existing catch blocks.
/// This exception is thrown in all contexts: outside workflows it comes directly from the
/// server check, and inside workflows the validation activity reports a status which the SDK
/// converts into this typed exception, so a plain <c>catch (ActivationNotFoundException)</c>
/// works everywhere. Workers register this type as a workflow failure exception type, so
/// when uncaught it fails the workflow execution instead of suspending it via task retries.
/// </summary>
public class ActivationNotFoundException : InvalidOperationException
{
    /// <summary>The target agent name (owner of the activation).</summary>
    public string AgentName { get; }

    /// <summary>The activation name that was not found.</summary>
    public string ActivationName { get; }

    /// <summary>The tenant the activation was looked up in, when known.</summary>
    public string? TenantId { get; }

    public ActivationNotFoundException(
        string agentName,
        string activationName,
        string? tenantId,
        Exception? innerException = null)
        : base(
            $"Activation '{activationName}' not found for agent '{agentName}'" +
            (string.IsNullOrWhiteSpace(tenantId) ? "" : $" in tenant '{tenantId}'") +
            ". The agent may not be activated in this tenant.",
            innerException)
    {
        AgentName = agentName;
        ActivationName = activationName;
        TenantId = tenantId;
    }
}

/// <summary>
/// Thrown when a target activation exists but has been deactivated.
/// Derives from <see cref="InvalidOperationException"/> for backward compatibility with
/// existing catch blocks.
/// This exception is thrown in all contexts: outside workflows it comes directly from the
/// server check, and inside workflows the validation activity reports a status which the SDK
/// converts into this typed exception, so a plain <c>catch (ActivationDeactivatedException)</c>
/// works everywhere. Workers register this type as a workflow failure exception type, so
/// when uncaught it fails the workflow execution instead of suspending it via task retries.
/// </summary>
public class ActivationDeactivatedException : InvalidOperationException
{
    /// <summary>The target agent name (owner of the activation).</summary>
    public string AgentName { get; }

    /// <summary>The activation name that is deactivated.</summary>
    public string ActivationName { get; }

    public ActivationDeactivatedException(
        string agentName,
        string activationName,
        Exception? innerException = null)
        : base(
            $"Activation '{activationName}' for agent '{agentName}' is deactivated.",
            innerException)
    {
        AgentName = agentName;
        ActivationName = activationName;
    }
}
