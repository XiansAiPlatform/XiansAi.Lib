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
    /// Maximum number of workflow executions this workflow's worker keeps in its sticky cache.
    /// Default is 500.
    /// <para>
    /// This is the single largest consumer of a worker's memory. A cached execution holds its workflow
    /// instance and the state needed to continue it without replaying history - mostly native memory owned
    /// by the SDK core, which a managed heap dump does not show. Eviction is LRU by count only: there is no
    /// TTL and no memory-pressure eviction, and a workflow terminated server-side keeps its entry until
    /// enough newer workflows push it out. So the cache does not shrink when load goes away; this number is
    /// what its steady-state footprint is bounded by.
    /// </para>
    /// <para>
    /// Temporal's own default is 10,000 per worker. An agent process runs one worker per defined workflow,
    /// so that default lets a handful of workflow definitions retain tens of thousands of executions - far
    /// more than a container-sized memory limit allows. 500 keeps the sticky hit rate high for the set of
    /// executions actually being worked on (five times the default <see cref="MaxConcurrent"/>) while
    /// bounding what is retained after a burst.
    /// </para>
    /// <para>
    /// The trade-off when lowering this is CPU and server load, not correctness: an evicted execution is
    /// replayed from history the next time a task arrives for it. Raise it for a worker with few, long-lived
    /// workflows; lower it for one facing many short-lived executions under a tight memory limit. Values
    /// between 1 and <see cref="MaxConcurrent"/> are raised to <see cref="MaxConcurrent"/>, since a non-zero
    /// cache smaller than the number of tasks in flight would thrash on every one of them.
    /// </para>
    /// <para>
    /// Set to <c>0</c> to disable sticky execution entirely. Nothing is cached, so nothing is retained and a
    /// terminated execution can strand nothing — at the cost of replaying history on every workflow task.
    /// That trade is worth making for a worker whose executions are numerous and mostly idle, since each one
    /// produces few workflow tasks and there is little cache benefit to give up; it is a poor trade for a
    /// worker running bursts of many activities, where every activity result becomes another full replay.
    /// </para>
    /// </summary>
    public int MaxCachedWorkflows { get; set; } = 500;

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
            MaxCachedWorkflows = MaxCachedWorkflows,
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
