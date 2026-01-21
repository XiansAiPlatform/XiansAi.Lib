using Temporalio.Workflows;
using Xians.Lib.Common.Usage;
using Xians.Lib.Temporal.Workflows.Usage;

namespace Xians.Lib.Agents.Core;

/// <summary>
/// Helper for usage metrics reporting operations.
/// Automatically handles workflow vs non-workflow contexts.
/// </summary>
public class MetricsHelper
{
    /// <summary>
    /// Reports usage metrics with automatic context detection.
    /// - In workflows: Uses UsageActivities (deterministic, no direct HTTP calls)
    /// - Outside workflows: Directly calls UsageEventsClient (HTTP)
    /// </summary>
    /// <param name="request">The usage report request containing metrics and metadata.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ReportAsync(UsageReportRequest request)
    {
        if (XiansContext.InWorkflow)
        {
            // In workflow context - use activity to avoid non-deterministic HTTP calls
            await Workflow.ExecuteActivityAsync(
                (UsageActivities act) => act.ReportUsageAsync(request),
                new() { StartToCloseTimeout = TimeSpan.FromSeconds(30) });
        }
        else
        {
            // Outside workflow context (agents, activities, etc.) - direct HTTP call is fine
            await UsageEventsClient.Instance.ReportAsync(request);
        }
    }

    /// <summary>
    /// Starts a fluent builder for tracking metrics with automatic context population.
    /// Auto-populates tenant ID, workflow ID, user ID, etc. from XiansContext when available.
    /// </summary>
    /// <returns>A fluent builder for constructing and reporting usage metrics.</returns>
    /// <example>
    /// <code>
    /// // In a workflow
    /// await XiansContext.Metrics
    ///     .Track()
    ///     .WithMetric("workflow_approval", "submitted", 1, "count")
    ///     .ForModel("gpt-4")
    ///     .ReportAsync();
    /// 
    /// // In an agent message handler
    /// await XiansContext.Metrics
    ///     .Track()
    ///     .WithMetric("tokens", "total", 150, "tokens")
    ///     .WithMetric("tokens", "prompt", 100, "tokens")
    ///     .ForModel("gpt-4")
    ///     .ReportAsync();
    /// </code>
    /// </example>
    public ContextAwareUsageReportBuilder Track()
    {
        return new ContextAwareUsageReportBuilder();
    }
}

/// <summary>
/// Context-aware fluent builder for constructing and reporting usage metrics.
/// Automatically populates context information from XiansContext when available.
/// </summary>
public class ContextAwareUsageReportBuilder
{
    private readonly List<MetricValue> _metrics = new();
    private string? _tenantId;
    private string? _userId;
    private string? _workflowId;
    private string? _requestId;
    private string? _model;
    private string? _source;
    private string? _customIdentifier;
    private Dictionary<string, string>? _metadata;

    internal ContextAwareUsageReportBuilder()
    {
    }

    /// <summary>
    /// Sets the tenant ID (defaults to XiansContext.TenantId if available).
    /// </summary>
    public ContextAwareUsageReportBuilder WithTenantId(string tenantId)
    {
        _tenantId = tenantId;
        return this;
    }

    /// <summary>
    /// Sets the user ID (defaults to current workflow's participant ID if available).
    /// </summary>
    public ContextAwareUsageReportBuilder WithUserId(string userId)
    {
        _userId = userId;
        return this;
    }

    /// <summary>
    /// Sets the workflow ID (defaults to XiansContext.WorkflowId if available).
    /// </summary>
    public ContextAwareUsageReportBuilder WithWorkflowId(string workflowId)
    {
        _workflowId = workflowId;
        return this;
    }

    /// <summary>
    /// Sets the request ID.
    /// </summary>
    public ContextAwareUsageReportBuilder WithRequestId(string requestId)
    {
        _requestId = requestId;
        return this;
    }

    /// <summary>
    /// Sets the model name (e.g., "gpt-4", "claude-3-opus").
    /// </summary>
    public ContextAwareUsageReportBuilder ForModel(string model)
    {
        _model = model;
        return this;
    }

    /// <summary>
    /// Sets the source identifier (defaults to current workflow type if available).
    /// </summary>
    public ContextAwareUsageReportBuilder FromSource(string source)
    {
        _source = source;
        return this;
    }

    /// <summary>
    /// Sets a custom identifier to link this usage event to client/agent-specific data.
    /// For example, a message ID to correlate token usage with a specific message.
    /// </summary>
    public ContextAwareUsageReportBuilder WithCustomIdentifier(string customIdentifier)
    {
        _customIdentifier = customIdentifier;
        return this;
    }

    /// <summary>
    /// Adds a single metric.
    /// </summary>
    /// <param name="category">The metric category (e.g., "tokens", "workflow_approval").</param>
    /// <param name="type">The metric type (e.g., "total", "submitted", "prompt").</param>
    /// <param name="value">The numeric value of the metric.</param>
    /// <param name="unit">The unit of measurement (e.g., "count", "tokens", "ms").</param>
    public ContextAwareUsageReportBuilder WithMetric(
        string category,
        string type,
        double value,
        string unit = "count")
    {
        _metrics.Add(new MetricValue
        {
            Category = category,
            Type = type,
            Value = value,
            Unit = unit
        });
        return this;
    }

    /// <summary>
    /// Adds multiple metrics at once using tuple syntax.
    /// </summary>
    /// <param name="metrics">Array of metric tuples (category, type, value, unit).</param>
    public ContextAwareUsageReportBuilder WithMetrics(params (string category, string type, double value, string unit)[] metrics)
    {
        foreach (var (category, type, value, unit) in metrics)
        {
            _metrics.Add(new MetricValue
            {
                Category = category,
                Type = type,
                Value = value,
                Unit = unit
            });
        }
        return this;
    }

    /// <summary>
    /// Adds a metadata key-value pair.
    /// </summary>
    public ContextAwareUsageReportBuilder WithMetadata(string key, string value)
    {
        _metadata ??= new Dictionary<string, string>();
        _metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Adds multiple metadata entries.
    /// </summary>
    public ContextAwareUsageReportBuilder WithMetadata(Dictionary<string, string> metadata)
    {
        _metadata = metadata;
        return this;
    }

    /// <summary>
    /// Reports the usage metrics with automatic context detection.
    /// Auto-populates tenant ID, workflow ID, source, etc. from XiansContext if not explicitly set.
    /// Throws if TenantId cannot be determined from context.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when TenantId is not set and cannot be extracted from context.</exception>
    public async Task ReportAsync()
    {
        // Auto-populate from XiansContext if available and not explicitly set
        var request = new UsageReportRequest
        {
            TenantId = _tenantId ?? XiansContext.TenantId,
            UserId = _userId,
            WorkflowId = _workflowId ?? XiansContext.SafeWorkflowId,
            RequestId = _requestId,
            Source = _source ?? XiansContext.SafeWorkflowType ?? "Unknown",
            Model = _model,
            CustomIdentifier = _customIdentifier,
            Metrics = _metrics,
            Metadata = _metadata
        };

        // Use the MetricsHelper to report (handles workflow vs non-workflow context)
        if (XiansContext.InWorkflow)
        {
            // In workflow context - use activity
            await Workflow.ExecuteActivityAsync(
                (UsageActivities act) => act.ReportUsageAsync(request),
                new() { StartToCloseTimeout = TimeSpan.FromSeconds(30) });
        }
        else
        {
            // Outside workflow context - direct HTTP call
            await UsageEventsClient.Instance.ReportAsync(request);
        }
    }
}
