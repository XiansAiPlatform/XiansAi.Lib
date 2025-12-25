using Xians.Lib.Agents.Models;

namespace Xians.Lib.Agents;

/// <summary>
/// Manages the collection of workflows for an agent.
/// </summary>
public class WorkflowCollection
{
    private readonly XiansAgent _agent;
    private readonly WorkflowDefinitionUploader? _uploader;
    private readonly List<XiansWorkflow> _workflows = new();

    internal WorkflowCollection(XiansAgent agent, WorkflowDefinitionUploader? uploader)
    {
        _agent = agent;
        _uploader = uploader;
    }

    /// <summary>
    /// Defines a built-in workflow for the agent using the platform-provided workflow implementation.
    /// </summary>
    /// <param name="name">Optional name for the workflow.</param>
    /// <param name="workers">Number of workers for the workflow. Default is 1.</param>
    /// <returns>A new built-in XiansWorkflow instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a workflow with the same name already exists or when attempting to register multiple unnamed workflows.</exception>
    public async Task<XiansWorkflow> DefineBuiltIn(string? name = null, int workers = 1)
    {
        // Check if workflow with same name already exists
        if (name != null && _workflows.Any(w => w.Name == name))
        {
            throw new InvalidOperationException($"A workflow with the name '{name}' has already been registered.");
        }
        
        // Check if an unnamed workflow already exists
        if (name == null && _workflows.Any(w => w.Name == null))
        {
            throw new InvalidOperationException("An unnamed workflow has already been registered. Only one unnamed workflow is allowed.");
        }
        
        var workflowType = _agent.Name + ":Default Workflow" + (name != null ? $" - {name}" : "");
        var workflow = new XiansWorkflow(_agent, workflowType, name, workers, isBuiltIn: true);
        _workflows.Add(workflow);
        
        // Upload workflow definition to server if uploader is available
        if (_uploader != null)
        {
            await UploadWorkflowDefinitionAsync(workflow);
        }
        
        return workflow;
    }

    /// <summary>
    /// Defines a custom workflow for the agent.
    /// </summary>
    /// <typeparam name="T">The custom workflow type.</typeparam>
    /// <param name="workers">Number of workers for the workflow. Default is 1.</param>
    /// <returns>A new custom XiansWorkflow instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a workflow of the same type already exists.</exception>
    public async Task<XiansWorkflow> DefineCustom<T>(int workers = 1) where T : class
    {
        var workflowType = typeof(T).Name;
        
        // Check if workflow with same type already exists
        if (_workflows.Any(w => w.WorkflowType == workflowType))
        {
            throw new InvalidOperationException($"A workflow of type '{workflowType}' has already been registered.");
        }
        
        var workflow = new XiansWorkflow(_agent, workflowType, null, workers, isBuiltIn: false);
        _workflows.Add(workflow);
        
        // Upload workflow definition to server if uploader is available
        if (_uploader != null)
        {
            await UploadWorkflowDefinitionAsync(workflow);
        }
        
        return workflow;
    }

    /// <summary>
    /// Uploads a workflow definition to the server.
    /// </summary>
    private async Task UploadWorkflowDefinitionAsync(XiansWorkflow workflow)
    {
        try
        {
            var definition = new WorkflowDefinition
            {
                Agent = _agent.Name,
                WorkflowType = workflow.WorkflowType,
                Name = workflow.Name,
                SystemScoped = _agent.SystemScoped,
                Workers = workflow.Workers,
                ActivityDefinitions = [],
                ParameterDefinitions = []
            };

            await _uploader!.UploadWorkflowDefinitionAsync(definition);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to upload workflow definition for workflow type {workflow.WorkflowType}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets a built-in workflow by name.
    /// If name is null, returns the unnamed built-in workflow.
    /// </summary>
    /// <param name="name">The name of the built-in workflow, or null for the unnamed workflow.</param>
    /// <returns>The built-in workflow or null if not found.</returns>
    public XiansWorkflow? GetBuiltIn(string? name = null)
    {
        // Built-in workflows are identified by their WorkflowType containing "Default Workflow"
        return _workflows.FirstOrDefault(w => 
            w.WorkflowType.Contains("Default Workflow") && w.Name == name);
    }

    /// <summary>
    /// Gets a custom workflow by its type.
    /// </summary>
    /// <typeparam name="T">The custom workflow type.</typeparam>
    /// <returns>The custom workflow or null if not found.</returns>
    public XiansWorkflow? GetCustom<T>() where T : class
    {
        var workflowType = typeof(T).Name;
        return _workflows.FirstOrDefault(w => w.WorkflowType == workflowType);
    }

    /// <summary>
    /// Gets all workflows for this agent.
    /// </summary>
    /// <returns>A read-only list of workflows.</returns>
    public IReadOnlyList<XiansWorkflow> GetAll()
    {
        return _workflows.AsReadOnly();
    }

    /// <summary>
    /// Runs all registered workflows asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for stopping workflows.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal async Task RunAllAsync(CancellationToken cancellationToken)
    {
        // Run all workflows concurrently
        var tasks = _workflows.Select(w => w.RunAsync(cancellationToken));
        await Task.WhenAll(tasks);
    }
}
