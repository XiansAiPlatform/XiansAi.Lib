using Microsoft.Extensions.Logging;
using Temporalio.Worker;
using Xians.Lib.Workflows;

namespace Xians.Lib.Agents.Core;

/// <summary>
/// Handles registration of workflows for Temporal workers.
/// Simplifies workflow registration and removes reflection complexity.
/// </summary>
internal class WorkflowRegistrar
{
    private readonly ILogger _logger;

    public WorkflowRegistrar(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Registers a built-in workflow.
    /// </summary>
    public void RegisterBuiltInWorkflow(TemporalWorkerOptions workerOptions, string workflowType)
    {
        workerOptions.AddWorkflow<BuiltinWorkflow>();
        _logger.LogDebug("Registered built-in workflow '{WorkflowType}'", workflowType);
    }

    /// <summary>
    /// Registers a custom workflow using its type.
    /// </summary>
    public void RegisterCustomWorkflow(
        TemporalWorkerOptions workerOptions,
        string workflowType,
        Type workflowClassType)
    {
        if (workflowClassType == null)
        {
            throw new InvalidOperationException(
                $"Workflow class type not provided for custom workflow '{workflowType}'");
        }

        try
        {
            // Use AddWorkflow via reflection (Temporal SDK requires generic method)
            var addWorkflowMethod = typeof(TemporalWorkerOptions)
                .GetMethod("AddWorkflow", Type.EmptyTypes);
            
            if (addWorkflowMethod == null)
            {
                throw new InvalidOperationException(
                    "Could not find AddWorkflow method on TemporalWorkerOptions");
            }

            var genericMethod = addWorkflowMethod.MakeGenericMethod(workflowClassType);
            genericMethod.Invoke(workerOptions, null);

            _logger.LogDebug(
                "Registered custom workflow '{WorkflowType}' with type '{WorkflowClass}'",
                workflowType, workflowClassType.Name);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to register custom workflow '{workflowType}' with type '{workflowClassType.Name}'",
                ex);
        }
    }
}

