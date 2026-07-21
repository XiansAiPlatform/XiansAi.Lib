using Temporalio.Common;

namespace Xians.Lib.Agents.Core;

/// <summary>
/// Controls whether a worker opts into Temporal Worker Deployment Versioning.
/// </summary>
public enum WorkerVersioningMode
{
    /// <summary>
    /// Versioning is off. The worker is created exactly as before (today's behavior). This is the default.
    /// </summary>
    Disabled,

    /// <summary>
    /// Versioning is enabled. A Build ID must be supplied via <see cref="WorkerVersioningOptions.BuildId"/>
    /// (the consuming agent project sources it, e.g. from the <c>TEMPORAL_WORKER_BUILD_ID</c> environment
    /// variable injected by the Temporal Worker Controller on Kubernetes). If no Build ID is supplied,
    /// worker startup fails fast with an <see cref="System.InvalidOperationException"/> rather than silently
    /// running unversioned. Consumers that should only version in some environments (e.g. on Kubernetes but
    /// not in local dev/CI) decide that themselves by setting <see cref="WorkerVersioningOptions.Mode"/> to
    /// <see cref="Disabled"/> when no Build ID is available.
    /// </summary>
    Enabled
}

/// <summary>
/// Opt-in configuration for Temporal Worker Deployment Versioning.
/// <para>
/// When enabled, each worker started by the library is tagged with a Worker Deployment Version
/// (a <see cref="DeploymentName"/> + <see cref="BuildId"/> pair) and opts into worker versioning,
/// so Temporal routes workflow tasks to compatible workers and new builds can be rolled out
/// (current/ramping versions) without breaking in-flight executions.
/// </para>
/// <para>
/// This type is additive and defaults to <see cref="WorkerVersioningMode.Disabled"/>: a consumer
/// that does not set <see cref="XiansOptions.WorkerVersioning"/> sees no behavior change.
/// </para>
/// </summary>
public class WorkerVersioningOptions
{
    /// <summary>
    /// Whether and how versioning is enabled. Defaults to <see cref="WorkerVersioningMode.Disabled"/>.
    /// </summary>
    public WorkerVersioningMode Mode { get; set; } = WorkerVersioningMode.Disabled;

    /// <summary>
    /// The Worker Deployment name grouping all builds of this service across task queues.
    /// When null/empty, falls back to the agent name. Supplied by the consumer (Agent.Lib / agent project).
    /// </summary>
    public string? DeploymentName { get; set; }

    /// <summary>
    /// The Build ID identifying this specific version within the deployment (e.g. a release tag or the
    /// controller-derived pod-template hash). Supplied by the consumer (Agent.Lib / agent project), which
    /// sources it — the library does not read it from the environment. Required when versioning is active.
    /// </summary>
    public string? BuildId { get; set; }

    /// <summary>
    /// Default versioning behavior applied to workflows that do not declare their own via
    /// <c>[Workflow(VersioningBehavior = ...)]</c> or <see cref="Xians.Lib.Agents.Workflows.Models.WorkflowOptions.VersioningBehavior"/>.
    /// Defaults to <see cref="VersioningBehavior.Pinned"/> — running executions complete on the version
    /// they started on, which is the behavior that replaces manual <c>Workflow.Patched</c> guards.
    /// </summary>
    public VersioningBehavior DefaultBehavior { get; set; } = VersioningBehavior.Pinned;
}
