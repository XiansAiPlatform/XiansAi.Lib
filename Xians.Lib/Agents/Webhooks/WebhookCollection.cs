using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Webhooks.Models;
using Xians.Lib.Common.Infrastructure;

namespace Xians.Lib.Agents.Webhooks;

/// <summary>
/// Provides management of builtin (inbound) webhooks for an agent, scoped to the agent itself
/// ("self") in the calling certificate's tenant.
/// <para>
/// The agent name is always the owning agent, and the activation name is resolved automatically from
/// the current <see cref="XiansContext"/> when running inside a workflow/activity - so callers do not
/// need to pass them. Where an operation can run outside a specific activation context (listing), the
/// scope is broadened to all activations of the agent.
/// </para>
/// Safe to call from Temporal workflows (HTTP is stubbed to <c>WebhookActivities</c>)
/// and from activities (direct HTTP).
/// </summary>
/// <remarks>
/// Calling <see cref="CreateAsync"/> from a workflow records the activity result (including
/// <see cref="WebhookInfo.WebhookUrl"/>) in workflow history. Prefer mapping to ids/names before
/// returning from an activity if you need to keep the webhook URL out of history.
/// </remarks>
/// <example>
/// <code>
/// // Create a webhook for the current activation (agent + activation resolved automatically)
/// var webhook = await agent.Webhooks.CreateAsync(webhookName: "EmailReceived");
/// Console.WriteLine(webhook.WebhookUrl);
///
/// // List this agent's webhooks (current activation when in context, otherwise all activations)
/// var all = await agent.Webhooks.ListAsync();
///
/// // Delete a webhook by id
/// await agent.Webhooks.DeleteAsync(webhook.Id);
/// </code>
/// </example>
public class WebhookCollection
{
    private readonly XiansAgent _agent;
    private readonly WebhookActivityExecutor _executor;

    internal WebhookCollection(XiansAgent agent)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        var logger = Common.Infrastructure.LoggerFactory.CreateLogger<WebhookActivityExecutor>();
        _executor = new WebhookActivityExecutor(_agent, logger);
    }

    /// <summary>
    /// Lists builtin webhooks for this agent. When called inside a workflow/activity, results are scoped
    /// to the current activation; otherwise all activations of the agent are returned.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent's webhooks (empty list when none exist).</returns>
    /// <exception cref="InvalidOperationException">Thrown when the HTTP service is not configured.</exception>
    public Task<List<WebhookInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        return _executor.ListAsync(XiansContext.SafeIdPostfix, cancellationToken);
    }

    /// <summary>
    /// Creates a builtin webhook for this agent's current activation. The agent name and activation name
    /// are resolved automatically (activation from the current <see cref="XiansContext"/>).
    /// </summary>
    /// <param name="webhookName">Optional webhook name/scope (defaults to "Default" on the server).</param>
    /// <param name="workflowName">Optional target workflow name (defaults to "Integrator Workflow" on the server).</param>
    /// <param name="participantId">Optional participant id the webhook runs as (defaults to "webhook").</param>
    /// <param name="timeoutSeconds">Optional synchronous response timeout in seconds (1-300; server default 30).</param>
    /// <param name="name">Optional human-readable name for the webhook.</param>
    /// <param name="activationName">Optional explicit activation name. Defaults to the current activation from context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created webhook, including its <see cref="WebhookInfo.WebhookUrl"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the HTTP service is not configured or no activation can be resolved.</exception>
    public Task<WebhookInfo> CreateAsync(
        string? webhookName = null,
        string? workflowName = null,
        string? participantId = null,
        int? timeoutSeconds = null,
        string? name = null,
        string? activationName = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedActivation = activationName ?? XiansContext.SafeIdPostfix
            ?? throw new InvalidOperationException(
                "Cannot create a webhook: no activation is available from the current context. " +
                "Call CreateAsync from within a workflow/activity, or pass an explicit activationName.");

        if (timeoutSeconds.HasValue)
        {
            ValidationHelper.ValidateRange(timeoutSeconds.Value, nameof(timeoutSeconds), 1, 300);
        }

        return _executor.CreateAsync(
            resolvedActivation,
            webhookName,
            workflowName,
            participantId,
            timeoutSeconds,
            name,
            cancellationToken);
    }

    /// <summary>
    /// Deletes a builtin webhook by id (revokes its API key and removes the integration).
    /// </summary>
    /// <param name="id">The webhook id (see <see cref="WebhookInfo.Id"/>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted, false if not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the HTTP service is not configured.</exception>
    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateRequired(id, nameof(id));
        return _executor.DeleteAsync(id, cancellationToken);
    }
}
