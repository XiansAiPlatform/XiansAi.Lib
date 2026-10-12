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
    /// name) was supplied, or when the agent-name fallback is not a valid deployment name.
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

        // An explicitly supplied deployment name (e.g. injected by the Temporal Worker Controller as
        // "<k8s namespace>/<WorkerDeployment name>") is passed through as-is.
        if (!string.IsNullOrWhiteSpace(options.DeploymentName))
        {
            return new ResolvedVersioning(options.DeploymentName!.Trim(), buildId!.Trim(), options.DefaultBehavior);
        }

        if (string.IsNullOrWhiteSpace(agentName))
        {
            throw new System.InvalidOperationException(
                "Worker versioning is enabled but no deployment name could be resolved. " +
                "Set WorkerVersioning.DeploymentName or provide an agent name.");
        }

        var deploymentName = agentName.Trim();

        // The agent-name fallback is restricted to [A-Za-z0-9._-]. Agent names may contain spaces or
        // Unicode (e.g. "Sales Assist Agent", "Kjøpsassistent") that do not make a safe deployment name.
        var invalid = DescribeInvalidCharacters(deploymentName);
        if (invalid != null)
        {
            throw new System.InvalidOperationException(
                $"Cannot use agent name '{deploymentName}' as the worker versioning deployment name: it contains {invalid}. " +
                "Fallback deployment names may only contain ASCII letters, digits, '.', '_' or '-'. " +
                "Set WorkerVersioning.DeploymentName explicitly.");
        }

        return new ResolvedVersioning(deploymentName, buildId!.Trim(), options.DefaultBehavior);
    }

    /// <summary>
    /// Returns a human-readable description of the characters outside <c>[A-Za-z0-9._-]</c>, or
    /// <c>null</c> when the name is valid.
    /// </summary>
    private static string? DescribeInvalidCharacters(string name)
    {
        var hasWhitespace = false;
        var hasNonAscii = false;
        var other = new System.Collections.Generic.SortedSet<char>();

        foreach (var c in name)
        {
            if (c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '_' or '-')
            {
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                hasWhitespace = true;
            }
            else if (c > 127)
            {
                hasNonAscii = true;
            }
            else
            {
                other.Add(c);
            }
        }

        var parts = new System.Collections.Generic.List<string>();
        if (hasWhitespace)
        {
            parts.Add("whitespace");
        }
        if (hasNonAscii)
        {
            parts.Add("non-ASCII characters");
        }
        if (other.Count > 0)
        {
            parts.Add("invalid characters " + string.Join(" ", System.Linq.Enumerable.Select(other, c => $"'{c}'")));
        }

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }
}
