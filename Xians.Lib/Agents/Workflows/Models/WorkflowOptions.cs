namespace Xians.Lib.Agents.Workflows.Models;

/// <summary>
/// Configuration options for Temporal workflows.
/// </summary>
public class WorkflowOptions
{
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
        return new WorkflowOptions
        {
            MaxConcurrent = MaxConcurrent,
            MaxHistoryLength = MaxHistoryLength,
            Activable = Activable,
            InactivityTimeout = InactivityTimeout
        };
    }
}
