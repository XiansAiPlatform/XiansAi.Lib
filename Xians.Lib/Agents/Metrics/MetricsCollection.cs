using Xians.Lib.Agents.A2A;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Agents.Metrics.Models;

namespace Xians.Lib.Agents.Metrics;

/// <summary>
/// Collection wrapper for metrics operations.
/// Provides instance-level access to metrics functionality.
/// Uses activity executor pattern for context-aware operations.
/// </summary>
public class MetricsCollection
{
    private readonly XiansAgent _agent;

    internal MetricsCollection(XiansAgent agent)
    {
        _agent = agent;
    }

    private MetricsActivityExecutor GetExecutor()
    {
        var logger = Common.Infrastructure.LoggerFactory.CreateLogger<MetricsActivityExecutor>();
        return new MetricsActivityExecutor(_agent, logger);
    }

    /// <summary>
    /// Reports usage metrics with automatic context detection.
    /// - In workflows: Uses UsageActivities (deterministic, no direct HTTP calls)
    /// - Outside workflows: Directly calls MetricsService (HTTP)
    /// </summary>
    /// <param name="request">The usage report request containing metrics and metadata.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ReportAsync(UsageReportRequest request)
    {
        var executor = GetExecutor();
        await executor.ReportAsync(request);
    }

    /// <summary>
    /// Starts a fluent builder for tracking metrics with automatic context population.
    /// Auto-populates tenant ID, workflow ID, user ID, etc. from XiansContext when available.
    /// </summary>
    /// <param name="context">Optional message context for A2A-aware workflow ID detection.</param>
    /// <returns>A fluent builder for constructing and reporting usage metrics.</returns>
    /// <example>
    /// <code>
    /// // Direct usage without Track()
    /// await XiansContext.Metrics
    ///     .ForModel("gpt-4")
    ///     .WithMetric("tokens", "total", 150, "tokens")
    ///     .ReportAsync();
    /// 
    /// // With explicit context (for A2A scenarios)
    /// await XiansContext.Metrics
    ///     .Track(context)
    ///     .ForModel("gpt-4")
    ///     .ReportAsync();
    /// </code>
    /// </example>
    public ContextAwareUsageReportBuilder Track(UserMessageContext? context = null)
    {
        return new ContextAwareUsageReportBuilder(_agent, context);
    }

    #region Fluent Builder Methods - Direct Access

    /// <summary>
    /// Sets the model name (e.g., "gpt-4", "claude-3-opus").
    /// Creates a new builder instance automatically populated from XiansContext.
    /// </summary>
    public ContextAwareUsageReportBuilder ForModel(string model)
    {
        return Track().ForModel(model);
    }

    /// <summary>
    /// Adds a single metric.
    /// Creates a new builder instance automatically populated from XiansContext.
    /// </summary>
    public ContextAwareUsageReportBuilder WithMetric(string category, string type, double value, string unit = "count")
    {
        return Track().WithMetric(category, type, value, unit);
    }

    /// <summary>
    /// Adds multiple metrics at once using tuple syntax.
    /// Creates a new builder instance automatically populated from XiansContext.
    /// </summary>
    public ContextAwareUsageReportBuilder WithMetrics(params (string category, string type, double value, string unit)[] metrics)
    {
        return Track().WithMetrics(metrics);
    }

    /// <summary>
    /// Sets the tenant ID.
    /// Creates a new builder instance automatically populated from XiansContext.
    /// </summary>
    public ContextAwareUsageReportBuilder WithTenantId(string tenantId)
    {
        return Track().WithTenantId(tenantId);
    }

    /// <summary>
    /// Sets the user/participant ID.
    /// Creates a new builder instance automatically populated from XiansContext.
    /// </summary>
    public ContextAwareUsageReportBuilder WithUserId(string userId)
    {
        return Track().WithUserId(userId);
    }

    /// <summary>
    /// Sets the workflow ID.
    /// Creates a new builder instance automatically populated from XiansContext.
    /// </summary>
    public ContextAwareUsageReportBuilder WithWorkflowId(string workflowId)
    {
        return Track().WithWorkflowId(workflowId);
    }

    /// <summary>
    /// Sets the request ID.
    /// Creates a new builder instance automatically populated from XiansContext.
    /// </summary>
    public ContextAwareUsageReportBuilder WithRequestId(string requestId)
    {
        return Track().WithRequestId(requestId);
    }

    /// <summary>
    /// Sets the source identifier.
    /// Creates a new builder instance automatically populated from XiansContext.
    /// </summary>
    public ContextAwareUsageReportBuilder FromSource(string source)
    {
        return Track().FromSource(source);
    }

    /// <summary>
    /// Sets a custom identifier to link this usage event to client/agent-specific data.
    /// Creates a new builder instance automatically populated from XiansContext.
    /// </summary>
    public ContextAwareUsageReportBuilder WithCustomIdentifier(string customIdentifier)
    {
        return Track().WithCustomIdentifier(customIdentifier);
    }

    /// <summary>
    /// Adds a metadata key-value pair.
    /// Creates a new builder instance automatically populated from XiansContext.
    /// </summary>
    public ContextAwareUsageReportBuilder WithMetadata(string key, string value)
    {
        return Track().WithMetadata(key, value);
    }

    /// <summary>
    /// Adds multiple metadata entries.
    /// Creates a new builder instance automatically populated from XiansContext.
    /// </summary>
    public ContextAwareUsageReportBuilder WithMetadata(Dictionary<string, string> metadata)
    {
        return Track().WithMetadata(metadata);
    }

    #endregion

    /// <summary>
    /// Gets the correct workflow ID for usage tracking.
    /// For A2A contexts, returns the target workflow ID instead of the source workflow ID.
    /// </summary>
    internal static string? GetWorkflowIdForTracking(UserMessageContext? context)
    {
        if (context is A2AMessageContext a2aContext)
        {
            // In A2A contexts, use the target workflow ID (where the handler is executing)
            return a2aContext.TargetWorkflowId;
        }
        // For regular contexts, use the current workflow ID from XiansContext
        return XiansContext.SafeWorkflowId;
    }

    /// <summary>
    /// Gets the correct workflow type for usage tracking.
    /// For A2A contexts, returns the target workflow type instead of the source workflow type.
    /// </summary>
    internal static string? GetWorkflowTypeForTracking(UserMessageContext? context)
    {
        if (context is A2AMessageContext a2aContext)
        {
            // In A2A contexts, use the target workflow type (where the handler is executing)
            var targetType = a2aContext.TargetWorkflowType;
            if (!string.IsNullOrWhiteSpace(targetType))
            {
                return targetType;
            }
        }
        // For regular contexts, use the current workflow type from XiansContext
        return XiansContext.SafeWorkflowType;
    }
}
