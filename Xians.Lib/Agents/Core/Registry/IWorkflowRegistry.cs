namespace Xians.Lib.Agents.Core.Registry;

/// <summary>
/// Interface for managing workflow registration and retrieval.
/// Enables testability and dependency injection.
/// </summary>
public interface IWorkflowRegistry
{
    /// <summary>
    /// Registers a workflow in the registry.
    /// </summary>
    /// <param name="workflowType">The workflow type identifier.</param>
    /// <param name="workflow">The workflow instance to register.</param>
    void Register(string workflowType, XiansWorkflow workflow);

    /// <summary>
    /// Gets a registered workflow by workflow type.
    /// </summary>
    /// <param name="workflowType">The workflow type identifier.</param>
    /// <returns>The workflow instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when workflowType is null or empty.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the workflow is not found.</exception>
    XiansWorkflow Get(string workflowType);

    /// <summary>
    /// Tries to get a registered workflow by workflow type.
    /// </summary>
    /// <param name="workflowType">The workflow type identifier.</param>
    /// <param name="workflow">The workflow instance if found, null otherwise.</param>
    /// <returns>True if the workflow was found, false otherwise.</returns>
    bool TryGet(string workflowType, out XiansWorkflow? workflow);

    /// <summary>
    /// Gets all registered workflows.
    /// </summary>
    /// <returns>Enumerable of all registered workflow instances.</returns>
    IEnumerable<XiansWorkflow> GetAll();

    /// <summary>
    /// Clears all registered workflows.
    /// For testing purposes only.
    /// </summary>
    void Clear();
}
