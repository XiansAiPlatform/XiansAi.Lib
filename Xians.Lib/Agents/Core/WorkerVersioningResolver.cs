using Temporalio.Common;

namespace Xians.Lib.Agents.Core;

/// <summary>
/// Pure resolution logic turning <see cref="WorkerVersioningOptions"/> into the concrete Worker
/// Deployment Version to apply, or <c>null</c> when the worker should stay unversioned.
/// <para>
/// The library never reads environment variables itself: the deployment name and Build ID are supplied
/// by the consumer (Agent.Lib / the agent project), which is responsible for sourcing them (e.g. from
/// controller-injected env vars) and passing them in via <see cref="WorkerVersioningOptions"/>.
/// </para>
/// Kept separate from <see cref="XiansWorkflow"/> so it is unit-testable via
/// <c>InternalsVisibleTo("Xians.Lib.Tests")</c>.
/// </summary>
internal static class WorkerVersioningResolver
{
    /// <summary>
    /// The concrete versioning identity to apply to a worker.
    /// </summary>
    internal sealed record ResolvedVersioning(
        string DeploymentName,
        string BuildId,
        VersioningBehavior DefaultBehavior);

    /// <summary>
    /// Resolves the effective worker-versioning identity for an agent.
    /// </summary>
    /// <param name="options">The configured options, or <c>null</c> if none were supplied.</param>
    /// <param name="agentName">The agent name, used as the fallback deployment name.</param>
    /// <returns>
    /// A resolved identity when the worker should be versioned; otherwise <c>null</c> (unversioned worker,
    /// today's behavior).
    /// </returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when <see cref="WorkerVersioningMode.Enabled"/> is configured but no Build ID (or deployment
    /// name) was supplied.
    /// </exception>
    internal static ResolvedVersioning? Resolve(WorkerVersioningOptions? options, string agentName)
    {
        // Disabled or unset -> unversioned worker, exactly today's behavior.
        if (options == null || options.Mode == WorkerVersioningMode.Disabled)
        {
            return null;
        }

        var buildId = options.BuildId;

        // Enabled requires a Build ID; fail fast if missing.
        if (string.IsNullOrWhiteSpace(buildId))
        {
            throw new System.InvalidOperationException(
                "Worker versioning mode is 'Enabled' but no Build ID was supplied. " +
                "Set WorkerVersioning.BuildId (the agent project should source it, e.g. from its environment).");
        }

        var deploymentName = FirstNonEmpty(options.DeploymentName, agentName);

        if (string.IsNullOrWhiteSpace(deploymentName))
        {
            throw new System.InvalidOperationException(
                "Worker versioning is enabled but no deployment name could be resolved. " +
                "Set WorkerVersioning.DeploymentName or provide an agent name.");
        }

        return new ResolvedVersioning(deploymentName!.Trim(), buildId!.Trim(), options.DefaultBehavior);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return null;
    }
}
