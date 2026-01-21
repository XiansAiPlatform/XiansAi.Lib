using Xians.Lib.Agents.Messaging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.A2A;

namespace Xians.Lib.Common.Usage;

/// <summary>
/// Extension methods for easy usage tracking in message handlers.
/// </summary>
public static class UsageTrackingExtensions
{
    /// <summary>
    /// Gets the correct workflow ID for usage tracking.
    /// For A2A contexts, returns the target workflow ID instead of the source workflow ID.
    /// </summary>
    internal static string GetWorkflowIdForTracking(UserMessageContext context)
    {
        // Check if this is an A2A context
        if (context is A2AMessageContext a2aContext)
        {
            Console.WriteLine($"A2A Context: Target Workflow ID: {a2aContext.TargetWorkflowId}");
            // In A2A contexts, use the target workflow ID (where the handler is executing)
            return a2aContext.TargetWorkflowId;
        }
        Console.WriteLine($"Regular Context: Workflow ID: {XiansContext.WorkflowId}");
        // For regular contexts, use the current workflow ID
        return XiansContext.WorkflowId;
    }

    /// <summary>
    /// Gets the correct workflow type for usage tracking.
    /// For A2A contexts, returns the target workflow type instead of the source workflow type.
    /// </summary>
    internal static string GetWorkflowTypeForTracking(UserMessageContext context)
    {
        // Check if this is an A2A context
        if (context is A2AMessageContext a2aContext)
        {
            // In A2A contexts, use the target workflow type (where the handler is executing)
            var targetType = a2aContext.TargetWorkflowType;
            if (!string.IsNullOrWhiteSpace(targetType))
            {
                return targetType;
            }
            
            // Fallback: try to get from XiansContext if TargetWorkflowType is empty
            try
            {
                var workflowType = XiansContext.CurrentWorkflow.WorkflowType;
                if (!string.IsNullOrWhiteSpace(workflowType))
                {
                    return workflowType;
                }
            }
            catch
            {
                // Ignore and fall through to "Unknown"
            }
        }
        else
        {
            // For regular contexts, use the current workflow type
            try
            {
                var workflowType = XiansContext.CurrentWorkflow.WorkflowType;
                if (!string.IsNullOrWhiteSpace(workflowType))
                {
                    return workflowType;
                }
            }
            catch
            {
                // Ignore and fall through to "Unknown"
            }
        }
        
        // Last resort: return "Unknown"
        return "Unknown";
    }

    /// <summary>
    /// Internal method for reporting usage metrics. Used by the fluent builder.
    /// Developers should use context.TrackUsage() instead of calling this directly.
    /// </summary>
    internal static async Task ReportUsageAsync(
        this UserMessageContext context,
        List<MetricValue> metrics,
        string? tenantId = null,
        string? userId = null,
        string? workflowId = null,
        string? requestId = null,
        string? model = null,
        string? source = null,
        string? customIdentifier = null,
        Dictionary<string, string>? metadata = null)
    {
        var request = new UsageReportRequest
        {
            TenantId = tenantId ?? context.Message.TenantId,
            UserId = userId ?? context.Message.ParticipantId,
            WorkflowId = workflowId ?? GetWorkflowIdForTracking(context),
            RequestId = requestId ?? context.Message.RequestId,
            Source = source ?? GetWorkflowTypeForTracking(context) ?? "Unknown",
            Model = model,
            CustomIdentifier = customIdentifier,
            Metrics = metrics,
            Metadata = metadata
        };

        await UsageEventsClient.Instance.ReportAsync(request);
    }

    /// <summary>
    /// Starts building a usage report with fluent API.
    /// </summary>
    public static UsageReportBuilder TrackUsage(this UserMessageContext context)
    {
        return new UsageReportBuilder(context);
    }
}

/// <summary>
/// Fluent builder for constructing and reporting usage metrics.
/// </summary>
public class UsageReportBuilder
{
    private readonly UserMessageContext _context;
    private readonly List<MetricValue> _metrics = new();
    private string? _tenantId;
    private string? _userId;
    private string? _workflowId;
    private string? _requestId;
    private string? _model;
    private string? _source;
    private string? _customIdentifier;
    private Dictionary<string, string>? _metadata;

    internal UsageReportBuilder(UserMessageContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Overrides the tenant ID (defaults to context.Message.TenantId if not set).
    /// </summary>
    public UsageReportBuilder WithTenantId(string tenantId)
    {
        _tenantId = tenantId;
        return this;
    }

    /// <summary>
    /// Overrides the user ID (defaults to context.Message.ParticipantId if not set).
    /// </summary>
    public UsageReportBuilder WithUserId(string userId)
    {
        _userId = userId;
        return this;
    }

    /// <summary>
    /// Overrides the workflow ID (defaults to auto-detected workflow ID if not set).
    /// </summary>
    public UsageReportBuilder WithWorkflowId(string workflowId)
    {
        _workflowId = workflowId;
        return this;
    }

    /// <summary>
    /// Overrides the request ID (defaults to context.Message.RequestId if not set).
    /// </summary>
    public UsageReportBuilder WithRequestId(string requestId)
    {
        _requestId = requestId;
        return this;
    }

    /// <summary>
    /// Sets the model name.
    /// </summary>
    public UsageReportBuilder ForModel(string model)
    {
        _model = model;
        return this;
    }

    /// <summary>
    /// Sets the source identifier.
    /// </summary>
    public UsageReportBuilder FromSource(string source)
    {
        _source = source;
        return this;
    }

    /// <summary>
    /// Sets a custom identifier to link this usage event to client/agent-specific data.
    /// For example, a message ID to correlate token usage with a specific message.
    /// </summary>
    public UsageReportBuilder WithCustomIdentifier(string customIdentifier)
    {
        _customIdentifier = customIdentifier;
        return this;
    }

    /// <summary>
    /// Adds a single metric.
    /// </summary>
    public UsageReportBuilder WithMetric(
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
    public UsageReportBuilder WithMetrics(params (string category, string type, double value, string unit)[] metrics)
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
    /// Adds metadata key-value pair.
    /// </summary>
    public UsageReportBuilder WithMetadata(string key, string value)
    {
        _metadata ??= new Dictionary<string, string>();
        _metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Adds multiple metadata entries.
    /// </summary>
    public UsageReportBuilder WithMetadata(Dictionary<string, string> metadata)
    {
        _metadata = metadata;
        return this;
    }

    /// <summary>
    /// Reports the usage metrics.
    /// </summary>
    public async Task ReportAsync()
    {
        await _context.ReportUsageAsync(
            _metrics, 
            _tenantId,
            _userId,
            _workflowId,
            _requestId,
            _model, 
            _source, 
            _customIdentifier, 
            _metadata);
    }
}
