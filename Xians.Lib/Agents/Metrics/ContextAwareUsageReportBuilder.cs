using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Agents.Metrics.Models;

namespace Xians.Lib.Agents.Metrics;

/// <summary>
/// Context-aware fluent builder for constructing and reporting usage metrics.
/// Automatically populates context information from XiansContext when available.
/// </summary>
public class ContextAwareUsageReportBuilder
{
    private readonly List<MetricValue> _metrics = new();
    private readonly UserMessageContext? _context;
    private readonly XiansAgent _agent;
    private string? _tenantId;
    private string? _userId;
    private string? _workflowId;
    private string? _requestId;
    private string? _model;
    private string? _source;
    private string? _customIdentifier;
    private Dictionary<string, string>? _metadata;

    internal ContextAwareUsageReportBuilder(XiansAgent agent, UserMessageContext? context = null)
    {
        _agent = agent;
        _context = context;
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
    /// Automatically populates tenantId, agentName, activationName, participantId, workflowId, and requestId 
    /// from XiansContext without requiring explicit method calls. These values can still be overridden if needed.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ReportAsync()
    {
        // Auto-populate TenantId - try explicit value, then context message, then XiansContext
        var tenantId = _tenantId 
            ?? _context?.Message.TenantId 
            ?? XiansContext.SafeTenantId;
        
        // Auto-populate ParticipantId - try explicit value, then context message, then XiansContext
        var participantId = _userId 
            ?? _context?.Message.ParticipantId 
            ?? XiansContext.SafeParticipantId;
        
        // Auto-populate WorkflowId - try explicit value, then A2A-aware detection from context/XiansContext
        var workflowId = _workflowId 
            ?? MetricsCollection.GetWorkflowIdForTracking(_context);
        
        // Auto-populate RequestId - try explicit value, then context message (no XiansContext equivalent)
        var requestId = _requestId 
            ?? _context?.Message.RequestId
            ?? XiansContext.GetRequestId();
        
        // Auto-populate AgentName - try XiansContext, fallback to current agent name
        var agentName = XiansContext.SafeAgentName ?? _agent.Name;
        
        // Auto-populate ActivationName (workflow type) from XiansContext
        var activationName = XiansContext.SafeIdPostfix;
        
        // Auto-populate Source (workflow type) - try explicit value, then A2A-aware detection, fallback to "Unknown"
        var workflowType = _source 
            ?? MetricsCollection.GetWorkflowTypeForTracking(_context) 
            ?? "Unknown";
        
        var request = new UsageReportRequest
        {
            TenantId = tenantId,
            ParticipantId = participantId,
            WorkflowId = workflowId,
            RequestId = requestId,
            WorkflowType = workflowType,
            Model = _model,
            CustomIdentifier = _customIdentifier,
            AgentName = agentName,
            ActivationName = activationName,
            Metrics = _metrics,
            Metadata = _metadata
        };

        // Use the MetricsCollection to report (via activity executor pattern)
        var metricsCollection = new MetricsCollection(_agent);
        await metricsCollection.ReportAsync(request);
    }
}
