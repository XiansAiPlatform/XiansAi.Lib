using System.Diagnostics;
using Microsoft.Extensions.Logging;
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
    /// Reports token usage from a message context.
    /// Automatically captures context information like tenant ID, user ID, workflow ID, and request ID.
    /// </summary>
    /// <param name="context">The message context.</param>
    /// <param name="model">The LLM model used (e.g., "gpt-4", "claude-3-opus").</param>
    /// <param name="promptTokens">Number of tokens in the prompt/input.</param>
    /// <param name="completionTokens">Number of tokens in the completion/output.</param>
    /// <param name="totalTokens">Total tokens used (prompt + completion).</param>
    /// <param name="messageCount">Number of messages sent to the LLM (including history). Defaults to 1 if not specified.</param>
    /// <param name="source">Optional source identifier (defaults to current workflow type).</param>
    /// <param name="metadata">Optional metadata dictionary for additional context.</param>
    /// <param name="responseTimeMs">Optional response time in milliseconds.</param>
    public static async Task ReportUsageAsync(
        this UserMessageContext context,
        string model,
        long promptTokens,
        long completionTokens,
        long totalTokens,
        long messageCount = 1,
        string? source = null,
        Dictionary<string, string>? metadata = null,
        long? responseTimeMs = null)
    {
        var record = new UsageEventRecord(
            TenantId: context.Message.TenantId,
            UserId: context.Message.ParticipantId,
            Model: model,
            PromptTokens: promptTokens,
            CompletionTokens: completionTokens,
            TotalTokens: totalTokens,
            MessageCount: messageCount,
            WorkflowId: GetWorkflowIdForTracking(context),
            RequestId: context.Message.RequestId,
            Source: source ?? GetWorkflowTypeForTracking(context) ?? "Unknown",
            Metadata: metadata,
            ResponseTimeMs: responseTimeMs
        );

        await UsageEventsClient.Instance.ReportAsync(record);
    }

    /// <summary>
    /// Reports token usage with a custom usage event record.
    /// Provides full control over all fields in the usage record.
    /// </summary>
    /// <param name="context">The message context.</param>
    /// <param name="record">The complete usage event record to report.</param>
    public static async Task ReportUsageAsync(
        this UserMessageContext context,
        UsageEventRecord record)
    {
        await UsageEventsClient.Instance.ReportAsync(record);
    }
}

/// <summary>
/// Helper class for tracking LLM calls with automatic timing and usage reporting.
/// </summary>
/// <example>
/// // Track an LLM call with automatic timing:
/// using var tracker = new UsageTracker(context, "gpt-4");
/// var response = await CallOpenAIAsync(prompt);
/// tracker.ReportAsync(response.Usage.PromptTokens, response.Usage.CompletionTokens);
/// </example>
public class UsageTracker : IDisposable
{
    private readonly UserMessageContext _context;
    private readonly string _model;
    private readonly long _messageCount;
    private readonly string? _source;
    private readonly Dictionary<string, string>? _metadata;
    private readonly Stopwatch _stopwatch;
    private bool _reported;

    /// <summary>
    /// Creates a new usage tracker that automatically measures response time.
    /// </summary>
    /// <param name="context">The message context.</param>
    /// <param name="model">The LLM model being used.</param>
    /// <param name="messageCount">Number of messages sent to the LLM (including history). Defaults to 1.</param>
    /// <param name="source">Optional source identifier.</param>
    /// <param name="metadata">Optional metadata dictionary.</param>
    public UsageTracker(
        UserMessageContext context, 
        string model,
        long messageCount = 1,
        string? source = null,
        Dictionary<string, string>? metadata = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _messageCount = messageCount;
        _source = source;
        _metadata = metadata;
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Reports usage with the specified token counts.
    /// Automatically includes the elapsed time since tracker creation.
    /// </summary>
    /// <param name="promptTokens">Number of tokens in the prompt.</param>
    /// <param name="completionTokens">Number of tokens in the completion.</param>
    /// <param name="totalTokens">Optional total tokens (calculated if not provided).</param>
    public async Task ReportAsync(long promptTokens, long completionTokens, long? totalTokens = null)
    {
        _stopwatch.Stop();
        
        var record = new UsageEventRecord(
            TenantId: _context.Message.TenantId,
            UserId: _context.Message.ParticipantId,
            Model: _model,
            PromptTokens: promptTokens,
            CompletionTokens: completionTokens,
            TotalTokens: totalTokens ?? (promptTokens + completionTokens),
            MessageCount: _messageCount,
            WorkflowId: UsageTrackingExtensions.GetWorkflowIdForTracking(_context),
            RequestId: _context.Message.RequestId,
            Source: _source ?? UsageTrackingExtensions.GetWorkflowTypeForTracking(_context) ?? "Unknown",
            Metadata: _metadata,
            ResponseTimeMs: _stopwatch.ElapsedMilliseconds
        );

        await UsageEventsClient.Instance.ReportAsync(record);
        _reported = true;
    }

    /// <summary>
    /// Disposes the tracker. Logs a warning if usage was not reported.
    /// </summary>
    public void Dispose()
    {
        if (!_reported && _stopwatch.IsRunning)
        {
            // Log a warning that tracking was started but never reported
            // This helps developers catch missing usage reporting calls
            var logger = Common.Infrastructure.LoggerFactory.CreateLogger<UsageTracker>();
            logger.LogWarning(
                "UsageTracker disposed without reporting usage. Model={Model}, ElapsedMs={ElapsedMs}. " +
                "Call ReportAsync() to record usage, or this may indicate an error in the LLM call.",
                _model,
                _stopwatch.ElapsedMilliseconds);
        }
        
        _stopwatch.Stop();
    }
}


