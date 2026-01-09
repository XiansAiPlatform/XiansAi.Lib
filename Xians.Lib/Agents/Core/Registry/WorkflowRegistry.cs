using System.Collections.Concurrent;

namespace Xians.Lib.Agents.Core.Registry;

/// <summary>
/// Thread-safe registry for managing XiansWorkflow instances.
/// Extracted from XiansContext for better separation of concerns.
/// </summary>
internal class WorkflowRegistry : IWorkflowRegistry
{
    private readonly ConcurrentDictionary<string, XiansWorkflow> _workflows = new();

    /// <inheritdoc/>
    public void Register(string workflowType, XiansWorkflow workflow)
    {
        if (string.IsNullOrWhiteSpace(workflowType))
        {
            throw new ArgumentNullException(nameof(workflowType));
        }

        if (workflow == null)
        {
            throw new ArgumentNullException(nameof(workflow));
        }

        if (!_workflows.TryAdd(workflowType, workflow))
        {
            // Workflow already registered - this is OK (might be restarting)
            _workflows[workflowType] = workflow;
        }
    }

    /// <inheritdoc/>
    public XiansWorkflow Get(string workflowType)
    {
        if (string.IsNullOrWhiteSpace(workflowType))
        {
            throw new ArgumentNullException(nameof(workflowType), "Workflow type cannot be null or empty.");
        }

        if (_workflows.TryGetValue(workflowType, out var workflow))
        {
            return workflow;
        }

        throw new KeyNotFoundException(
            $"Workflow '{workflowType}' not found. Available workflows: {string.Join(", ", _workflows.Keys)}");
    }

    /// <inheritdoc/>
    public bool TryGet(string workflowType, out XiansWorkflow? workflow)
    {
        if (string.IsNullOrWhiteSpace(workflowType))
        {
            workflow = null;
            return false;
        }

        return _workflows.TryGetValue(workflowType, out workflow);
    }

    /// <inheritdoc/>
    public IEnumerable<XiansWorkflow> GetAll()
    {
        return _workflows.Values;
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _workflows.Clear();
    }
}
