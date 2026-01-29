namespace Xians.Lib.Agents.Workflows;

/// <summary>
/// Options for defining workflows.
/// </summary>
public class WorkflowDefinitionOptions
{
    /// <summary>
    /// Maximum concurrent workflow task executions.
    /// Default is 100 (Temporal's default).
    /// </summary>
    public int MaxConcurrent { get; set; } = 100;

    /// <summary>
    /// Whether the workflow can be activated.
    /// Default is true.
    /// Only applicable to custom workflows (built-in workflows are always activable).
    /// </summary>
    public bool Activable { get; set; } = true;
}
