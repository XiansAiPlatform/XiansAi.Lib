using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Workflows;

namespace Temporal;

/// <summary>
/// Unified service for starting and executing workflows.
/// Automatically handles both in-workflow (child workflows) and out-of-workflow (top-level workflows) contexts.
/// </summary>
public class WorkflowService
{
    private static readonly XiansAi.Logging.Logger<WorkflowService> _logger = XiansAi.Logging.Logger<WorkflowService>.For();

    /// <summary>
    /// Starts a workflow without waiting for completion.
    /// - In workflow context: starts as a child workflow
    /// - Outside workflow context: starts as a new top-level workflow
    /// </summary>
    /// <param name="namePostfix">Optional postfix for workflow ID uniqueness</param>
    /// <param name="args">Arguments to pass to the workflow</param>
    /// <param name="executionTimeout">Maximum time the workflow can run (optional)</param>
    /// <param name="runTimeout">Maximum time a single workflow run can take (optional)</param>
    /// <param name="taskTimeout">Maximum time a workflow task can take (optional)</param>
    public static async Task Start<TWorkflow>(
        string namePostfix, 
        object[] args,
        TimeSpan? executionTimeout = null,
        TimeSpan? runTimeout = null,
        TimeSpan? taskTimeout = null)
    {
        var workflowType = WorkflowIdentifier.GetWorkflowTypeFor(typeof(TWorkflow));
        
        if (Workflow.InWorkflow)
        {
            await StartChildWorkflow(workflowType, namePostfix, args, executionTimeout, runTimeout, taskTimeout);
        }
        else
        {
            await StartTopLevelWorkflow(workflowType, namePostfix, args, executionTimeout, runTimeout, taskTimeout);
        }
    }

    /// <summary>
    /// Executes a workflow and waits for its result.
    /// - In workflow context: executes as a child workflow
    /// - Outside workflow context: executes as a new top-level workflow
    /// </summary>
    /// <param name="namePostfix">Optional postfix for workflow ID uniqueness</param>
    /// <param name="args">Arguments to pass to the workflow</param>
    /// <param name="executionTimeout">Maximum time the workflow can run (optional)</param>
    /// <param name="runTimeout">Maximum time a single workflow run can take (optional)</param>
    /// <param name="taskTimeout">Maximum time a workflow task can take (optional)</param>
    public static async Task<TResult> Execute<TWorkflow, TResult>(
        string namePostfix, 
        object[] args,
        TimeSpan? executionTimeout = null,
        TimeSpan? runTimeout = null,
        TimeSpan? taskTimeout = null)
    {
        var workflowType = WorkflowIdentifier.GetWorkflowTypeFor(typeof(TWorkflow));
        
        if (Workflow.InWorkflow)
        {
            return await ExecuteChildWorkflow<TResult>(workflowType, namePostfix, args, executionTimeout, runTimeout, taskTimeout);
        }
        else
        {
            return await ExecuteTopLevelWorkflow<TResult>(workflowType, namePostfix, args, executionTimeout, runTimeout, taskTimeout);
        }
    }

    #region Child Workflow Operations (In-Workflow Context)

    private static async Task StartChildWorkflow(
        string workflowType, 
        string namePostfix, 
        object[] args,
        TimeSpan? executionTimeout,
        TimeSpan? runTimeout,
        TimeSpan? taskTimeout)
    {
        _logger.LogInformation($"Starting child workflow `{workflowType}` in parent workflow `{AgentContext.WorkflowId}`");
        var options = new SubWorkflowOptions(workflowType, namePostfix)
        {
            ExecutionTimeout = executionTimeout,
            RunTimeout = runTimeout,
            TaskTimeout = taskTimeout
        };
        await Workflow.StartChildWorkflowAsync(workflowType, args, options);
    }

    private static async Task<TResult> ExecuteChildWorkflow<TResult>(
        string workflowType, 
        string namePostfix, 
        object[] args,
        TimeSpan? executionTimeout,
        TimeSpan? runTimeout,
        TimeSpan? taskTimeout)
    {
        _logger.LogInformation($"Executing child workflow `{workflowType}` in parent workflow `{AgentContext.WorkflowId}`");
        var options = new SubWorkflowOptions(workflowType, namePostfix)
        {
            ExecutionTimeout = executionTimeout,
            RunTimeout = runTimeout,
            TaskTimeout = taskTimeout
        };
        return await Workflow.ExecuteChildWorkflowAsync<TResult>(workflowType, args, options);
    }

    #endregion

    #region Top-Level Workflow Operations (Out-of-Workflow Context)

    private static async Task StartTopLevelWorkflow(
        string workflowType, 
        string namePostfix, 
        object[] args,
        TimeSpan? executionTimeout,
        TimeSpan? runTimeout,
        TimeSpan? taskTimeout)
    {
        var client = new WorkflowClient(AgentContext.AgentName);
        await client.Start(workflowType, args, namePostfix, executionTimeout, runTimeout, taskTimeout);
    }

    private static async Task<TResult> ExecuteTopLevelWorkflow<TResult>(
        string workflowType, 
        string namePostfix, 
        object[] args,
        TimeSpan? executionTimeout,
        TimeSpan? runTimeout,
        TimeSpan? taskTimeout)
    {
        var client = new WorkflowClient(AgentContext.AgentName);
        return await client.Execute<TResult>(workflowType, args, namePostfix, executionTimeout, runTimeout, taskTimeout);
    }

    #endregion

    #region Workflow Client (Internal Helper)

    /// <summary>
    /// Internal helper class for interacting with Temporal client.
    /// Used only when starting/executing top-level workflows from outside a workflow context.
    /// </summary>
    private class WorkflowClient
    {
        private readonly ILogger<WorkflowClient> _clientLogger;
        private readonly ITemporalClient _client;
        private readonly string _agentName;

        public WorkflowClient(string agentName)
        {
            _clientLogger = Globals.LogFactory.CreateLogger<WorkflowClient>();
            _client = TemporalClientService.Instance.GetClientAsync().Result;
            _agentName = agentName;
        }

        public async Task<TResult> Execute<TResult>(
            string workflowType, 
            object[] args, 
            string? postfix = null,
            TimeSpan? executionTimeout = null,
            TimeSpan? runTimeout = null,
            TimeSpan? taskTimeout = null)
        {
            _clientLogger.LogInformation($"Executing top-level workflow `{workflowType}` with id postfix `{postfix}` for agent `{_agentName}`");
            var options = new NewWorkflowOptions(workflowType, postfix, _agentName)
            {
                ExecutionTimeout = executionTimeout,
                RunTimeout = runTimeout,
                TaskTimeout = taskTimeout
            };
            return await _client.ExecuteWorkflowAsync<TResult>(workflowType, args, options);
        }

        public async Task Start(
            string workflowType, 
            object[] args, 
            string? postfix = null,
            TimeSpan? executionTimeout = null,
            TimeSpan? runTimeout = null,
            TimeSpan? taskTimeout = null)
        {
            _clientLogger.LogInformation($"Starting top-level workflow `{workflowType}` with id postfix `{postfix}` for agent `{_agentName}`");
            var options = new NewWorkflowOptions(workflowType, postfix, _agentName)
            {
                ExecutionTimeout = executionTimeout,
                RunTimeout = runTimeout,
                TaskTimeout = taskTimeout
            };
            await _client.StartWorkflowAsync(workflowType, args, options);
        }
    }

    #endregion
}
