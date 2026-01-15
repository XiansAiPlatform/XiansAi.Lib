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
    /// Creates a copy of these options.
    /// </summary>
    internal WorkflowOptions Clone()
    {
        return new WorkflowOptions
        {
            MaxConcurrent = MaxConcurrent,
            MaxHistoryLength = MaxHistoryLength
        };
    }
}
