namespace Xians.Lib.Agents;

/// <summary>
/// Represents a registered agent in the Xians platform.
/// </summary>
public class XiansAgent
{
    /// <summary>
    /// Gets the workflows collection for managing agent workflows.
    /// </summary>
    public WorkflowCollection Workflows { get; private set; }

    /// <summary>
    /// Gets the name of the agent.
    /// </summary>
    public required string Name { get; private set; }

    public string Version { get; private set; }

    public string Description { get; private set; }

    internal XiansAgent(string name)
    {
        Name = name;
        Workflows = new WorkflowCollection(this);
    }

    /// <summary>
    /// Runs all registered workflows for this agent asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RunAllAsync()
    {
        // TODO: Implement running all workflows
        // Get all workflows from the Workflows collection and run them concurrently
        await Workflows.RunAllAsync();
    }
}

