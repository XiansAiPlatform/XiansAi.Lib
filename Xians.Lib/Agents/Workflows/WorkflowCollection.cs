using Xians.Lib.Agents.Knowledge.Models;
using Xians.Lib.Agents.Messaging.Models;
using Xians.Lib.Agents.Workflows.Models;
using System.Linq;
using System.Reflection;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common;

namespace Xians.Lib.Agents.Workflows;

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
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _uploader = uploader;
        
        // Automatically register TaskWorkflow for human-in-the-loop tasks
        DefineTaskWorkflow();
    }

    /// <summary>
    /// Defines a built-in workflow for the agent using the platform-provided workflow implementation.
    /// </summary>
    /// <param name="name">Optional name for the workflow.</param>
    /// <param name="workers">Number of workers for the workflow. Default is 1.</param>
    /// <returns>A new built-in XiansWorkflow instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a workflow with the same name already exists or when attempting to register multiple unnamed workflows.</exception>
    public XiansWorkflow DefineBuiltIn(string? name = null, int workers = 1)
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
        
        var workflowType = WorkflowIdentity.BuildBuiltInWorkflowType(_agent.Name, name);
        var workflow = new XiansWorkflow(_agent, workflowType, name, workers, isBuiltIn: true);
        _workflows.Add(workflow);
        
        // Note: Workflow definition will be uploaded when RunAllAsync() is called
        
        return workflow;
    }

    /// <summary>
    /// Defines a custom workflow for the agent.
    /// </summary>
    /// <typeparam name="T">The custom workflow type.</typeparam>
    /// <param name="workers">Number of workers for the workflow. Default is 1.</param>
    /// <returns>A new custom XiansWorkflow instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a workflow of the same type already exists.</exception>
    public XiansWorkflow DefineCustom<T>(int workers = 1) where T : class
    {
        return DefineCustomInternal<T>(workers, validateAgentPrefix: true);
    }

    /// <summary>
    /// Internal method to define a custom workflow with optional agent prefix validation.
    /// </summary>
    private XiansWorkflow DefineCustomInternal<T>(int workers, bool validateAgentPrefix, bool isPlatformWorkflow = false) where T : class
    {
        // Get workflow type from [Workflow] attribute if present, otherwise use class name
        var workflowType = GetWorkflowTypeFromAttribute<T>() ?? typeof(T).Name;
        
        // Validate that workflow type follows the naming convention (unless it's a platform workflow)
        if (validateAgentPrefix)
        {
            var expectedPrefix = _agent.Name + ":";
            if (!workflowType.StartsWith(expectedPrefix))
            {
                throw new InvalidOperationException(
                    $"Custom workflow type '{workflowType}' must start with agent name prefix '{expectedPrefix}'. " +
                    $"Add [Workflow(\"{expectedPrefix}{workflowType}\")] attribute to your workflow class, " +
                    $"or rename the class to start with the agent name.");
            }
        }
        
        // Check if workflow with same type already exists
        if (_workflows.Any(w => w.WorkflowType == workflowType))
        {
            throw new InvalidOperationException($"A workflow of type '{workflowType}' has already been registered.");
        }
        
        var workflow = new XiansWorkflow(_agent, workflowType, null, workers, isBuiltIn: false, workflowClassType: typeof(T), isPlatformWorkflow: isPlatformWorkflow);
        _workflows.Add(workflow);
        
        // Note: Workflow definition will be uploaded when RunAllAsync() is called
        
        return workflow;
    }

    /// <summary>
    /// Automatically defines the TaskWorkflow for human-in-the-loop tasks.
    /// This is a platform workflow and doesn't follow agent naming conventions.
    /// </summary>
    private void DefineTaskWorkflow()
    {
        // Register TaskWorkflow without agent prefix validation
        // TaskWorkflow is a platform-level workflow with type "Platform:Task Workflow"
        DefineCustomInternal<TaskWorkflow>(workers: 1, validateAgentPrefix: false, isPlatformWorkflow: true);
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
                ParameterDefinitions = ExtractWorkflowParameters(workflow)
            };

            await _uploader!.UploadWorkflowDefinitionAsync(definition);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to upload workflow definition for workflow type {workflow.WorkflowType}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Extracts workflow input parameters from the workflow class using reflection.
    /// </summary>
    private static List<ParameterDefinition> ExtractWorkflowParameters(XiansWorkflow workflow)
    {
        var workflowType = workflow.GetWorkflowClassType();
        if (workflowType == null)
        {
            // Built-in workflows don't have parameters
            return [];
        }

        var workflowRunMethod = workflowType.GetMethods()
            .FirstOrDefault(m => m.GetCustomAttributes(typeof(Temporalio.Workflows.WorkflowRunAttribute), false).Any());

        if (workflowRunMethod == null)
        {
            throw new InvalidOperationException($"Workflow run method not found for workflow type {workflowType}");
        }

        return workflowRunMethod.GetParameters()
            .Select(p => new ParameterDefinition
            {
                Name = p.Name,
                Type = p.ParameterType.Name
            })
            .ToList();
    }

    /// <summary>
    /// Gets a built-in workflow by name.
    /// If name is null, returns the unnamed built-in workflow.
    /// </summary>
    /// <param name="name">The name of the built-in workflow, or null for the unnamed workflow.</param>
    /// <returns>The built-in workflow or null if not found.</returns>
    public XiansWorkflow? GetBuiltIn(string? name = null)
    {
        // Built-in workflows are identified by their WorkflowType containing "BuiltIn Workflow"
        return _workflows.FirstOrDefault(w => 
            WorkflowIdentity.IsBuiltInWorkflow(w.WorkflowType) && w.Name == name);
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
    /// Gets the workflow type name from the [Workflow] attribute if present.
    /// </summary>
    private static string? GetWorkflowTypeFromAttribute<T>() where T : class
    {
        var workflowAttribute = typeof(T).GetCustomAttributes(typeof(Temporalio.Workflows.WorkflowAttribute), false)
            .FirstOrDefault() as Temporalio.Workflows.WorkflowAttribute;
        
        return workflowAttribute?.Name;
    }

    /// <summary>
    /// Uploads all workflow definitions to the server.
    /// This should be called before running workflows to ensure the server has the latest definitions.
    /// Can also be called explicitly to register the agent without running workflows.
    /// </summary>
    public async Task UploadAllDefinitionsAsync()
    {
        if (_uploader == null)
        {
            return; // No uploader configured, skip
        }

        // Upload all workflow definitions
        foreach (var workflow in _workflows)
        {
            await UploadWorkflowDefinitionAsync(workflow);
        }
    }

    /// <summary>
    /// Runs all registered workflows asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for stopping workflows.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal async Task RunAllAsync(CancellationToken cancellationToken)
    {
        // Upload all workflow definitions to server before starting workers
        // This ensures atomicity - either all workflows are uploaded or none are
        await UploadAllDefinitionsAsync();

        // Run all workflows concurrently
        var tasks = _workflows.Select(w => w.RunAsync(cancellationToken));
        await Task.WhenAll(tasks);
    }
}
