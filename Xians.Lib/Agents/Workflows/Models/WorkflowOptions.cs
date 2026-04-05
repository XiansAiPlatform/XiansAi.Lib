namespace Xians.Lib.Agents.Workflows.Models;

/// <summary>
/// Configuration options for Temporal workflows.
/// </summary>
public class WorkflowOptions
{
    /// <summary>
    /// Message activity execution options used by built-in messaging flow.
    /// Agents can override these values per workflow to customize retries/timeouts.
    /// </summary>
    private MessageActivityExecutionOptions _messageActivityExecution = new();

    // Only set to true when agent code explicitly assigns MessageActivityExecution.
    private bool _isMessageActivityExecutionExplicitlySet;

    public MessageActivityExecutionOptions MessageActivityExecution
    {
        get => _messageActivityExecution;
        set
        {
            _messageActivityExecution = value ?? new MessageActivityExecutionOptions();
            _isMessageActivityExecutionExplicitlySet = true;
        }
    }

    /// <summary>
    /// Indicates whether MessageActivityExecution was explicitly assigned by agent code.
    /// Used to preserve legacy behavior when agents do not provide any message activity options.
    /// </summary>
    public bool IsMessageActivityExecutionExplicitlySet => _isMessageActivityExecutionExplicitlySet;

    /// <summary>
    /// Maximum number of concurrent workflow task executions.
    /// Default is 100 (Temporal's default).
    /// </summary>
    public int MaxConcurrent { get; set; } = 100;

    /// <summary>
    /// Maximum history length before ContinueAsNew is triggered.
    /// Default is 1000 events.
    /// This is a safety fallback - the workflow will primarily rely on Workflow.ContinueAsNewSuggested.
    /// </summary>
    public int MaxHistoryLength { get; set; } = 1000;

    /// <summary>
    /// Whether this workflow can be activated/triggered.
    /// Default is true.
    /// </summary>
    public bool Activable { get; set; } = true;

    /// <summary>
    /// Maximum duration of inactivity (no messages) before the workflow completes.
    /// When set, the timer resets each time a message is processed.
    /// Null means never timeout (workflow runs indefinitely until cancelled or continued-as-new).
    /// Default is 12 hours.
    /// </summary>
    public TimeSpan? InactivityTimeout { get; set; } = TimeSpan.FromHours(12);

    /// <summary>
    /// Creates a copy of these options.
    /// </summary>
    internal WorkflowOptions Clone()
    {
        var clone = new WorkflowOptions
        {
            MaxConcurrent = MaxConcurrent,
            MaxHistoryLength = MaxHistoryLength,
            Activable = Activable,
            InactivityTimeout = InactivityTimeout
        };

        // Copy backing fields without invoking the setter (so the explicit assignment flag is preserved).
        clone._messageActivityExecution = MessageActivityExecution.Clone();
        clone._isMessageActivityExecutionExplicitlySet = _isMessageActivityExecutionExplicitlySet;

        return clone;
    }
}

/// <summary>
/// Configures Temporal activity timeout/retry behavior for built-in message processing.
/// </summary>
public class MessageActivityExecutionOptions
{
    /// <summary>
    /// Activity start-to-close timeout.
    /// Default is 10 minutes.
    /// </summary>
    public TimeSpan StartToCloseTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Retry policy for message activities.
    /// </summary>
    public MessageActivityRetryOptions Retry { get; set; } = new();

    /// <summary>
    /// Optional exception type names that should be treated as transient.
    /// Accepts short names (e.g. "TimeoutException") or fully qualified names
    /// (e.g. "Xians.Lib.Common.Exceptions.RateLimitException").
    /// If empty, built-in transient type detection is used.
    /// </summary>
    public List<string> TransientExceptionTypeNames { get; set; } = [];

    /// <summary>
    /// Optional message substrings that should be treated as transient when
    /// found in an exception message (case-insensitive).
    /// If empty, built-in message pattern detection is used.
    /// </summary>
    public List<string> TransientExceptionMessagePatterns { get; set; } = [];

    internal MessageActivityExecutionOptions Clone()
    {
        return new MessageActivityExecutionOptions
        {
            StartToCloseTimeout = StartToCloseTimeout,
            Retry = Retry.Clone(),
            TransientExceptionTypeNames = [.. TransientExceptionTypeNames],
            TransientExceptionMessagePatterns = [.. TransientExceptionMessagePatterns]
        };
    }
}

/// <summary>
/// Retry policy configuration for built-in message activities.
/// </summary>
public class MessageActivityRetryOptions
{
    /// <summary>
    /// Maximum retry attempts for the activity.
    /// Default is 5.
    /// </summary>
    public int MaximumAttempts { get; set; } = 5;

    /// <summary>
    /// Initial backoff interval.
    /// Default is 5 seconds.
    /// </summary>
    public TimeSpan InitialInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum backoff interval.
    /// Default is 3 minutes.
    /// </summary>
    public TimeSpan MaximumInterval { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Exponential backoff coefficient.
    /// Default is 2.
    /// </summary>
    public float BackoffCoefficient { get; set; } = 2.0f;

    internal MessageActivityRetryOptions Clone()
    {
        return new MessageActivityRetryOptions
        {
            MaximumAttempts = MaximumAttempts,
            InitialInterval = InitialInterval,
            MaximumInterval = MaximumInterval,
            BackoffCoefficient = BackoffCoefficient
        };
    }
}
