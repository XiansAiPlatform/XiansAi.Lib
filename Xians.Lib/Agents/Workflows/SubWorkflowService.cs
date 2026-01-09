using Microsoft.Extensions.Logging;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common.MultiTenancy;
using System.Reflection;

namespace Xians.Lib.Agents.Workflows;

/// <summary>
/// Logger helper class for SubWorkflowService (needed because static classes can't be used as generic type arguments).
/// </summary>
internal class SubWorkflowServiceLogger { }

/// <summary>
/// Service for starting and executing sub-workflows (child workflows).
/// Automatically handles both in-workflow (child workflow) and out-of-workflow (client workflow) scenarios.
/// </summary>
public static class SubWorkflowService
{
    private static readonly ILogger _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<SubWorkflowServiceLogger>();


    /// <summary>
    /// Starts a child workflow without waiting for its completion.
    /// If called from within a workflow, starts a child workflow.
    /// If called outside a workflow, starts a new workflow using the Temporal client.
    /// </summary>
    /// <param name="workflowType">The workflow type (format: "AgentName:WorkflowName").</param>
    /// <param name="idPostfix">Optional postfix for workflow ID uniqueness.</param>
    /// <param name="args">Arguments to pass to the workflow.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task StartAsync(string workflowType, string? idPostfix = null, params object[] args)
    {
        if (Workflow.InWorkflow)
        {
            // Within a workflow - start as child workflow
            _logger.LogInformation(
                "Starting child workflow '{WorkflowType}' from parent '{ParentWorkflowId}'",
                workflowType,
                XiansContext.WorkflowId);

            var options = new SubWorkflowOptions(workflowType, idPostfix);
            await Workflow.StartChildWorkflowAsync(workflowType, args, options);
        }
        else
        {
            // Outside a workflow - start as new workflow via client
            _logger.LogInformation(
                "Starting workflow '{WorkflowType}' via client (not in workflow context)",
                workflowType);

            await StartViaClientAsync(workflowType, idPostfix, args);
        }
    }
    
    /// <summary>
    /// Starts a child workflow without waiting for its completion.
    /// If called from within a workflow, starts a child workflow.
    /// If called outside a workflow, starts a new workflow using the Temporal client.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow class type.</typeparam>
    /// <param name="idPostfix">Optional postfix for workflow ID uniqueness.</param>
    /// <param name="args">Arguments to pass to the workflow.</param>
    /// <param name="cancellationToken">Cancellation token (only applicable when called outside workflow context).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when workflow type cannot be determined or agent not found.</exception>
    public static async Task StartAsync<TWorkflow>(string? idPostfix = null, params object[] args)
    {
        var workflowType = GetWorkflowTypeFromClass<TWorkflow>();
        await StartAsync(workflowType, idPostfix, args);
    }

    /// <summary>
    /// Executes a child workflow and waits for its result.
    /// If called from within a workflow, executes a child workflow.
    /// If called outside a workflow, executes a workflow using the Temporal client.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow class type.</typeparam>
    /// <typeparam name="TResult">The expected result type.</typeparam>
    /// <param name="idPostfix">Optional postfix for workflow ID uniqueness.</param>
    /// <param name="args">Arguments to pass to the workflow.</param>
    /// <returns>The workflow result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when workflow type cannot be determined or agent not found.</exception>
    public static async Task<TResult> ExecuteAsync<TWorkflow, TResult>(string? idPostfix = null, params object[] args)
    {
        var workflowType = GetWorkflowTypeFromClass<TWorkflow>();
        return await ExecuteAsync<TResult>(workflowType, idPostfix, args);
    }

    /// <summary>
    /// Executes a child workflow and waits for its result.
    /// If called from within a workflow, executes a child workflow.
    /// If called outside a workflow, executes a workflow using the Temporal client.
    /// </summary>
    /// <typeparam name="TResult">The expected result type.</typeparam>
    /// <param name="workflowType">The workflow type (format: "AgentName:WorkflowName").</param>
    /// <param name="idPostfix">Optional postfix for workflow ID uniqueness.</param>
    /// <param name="args">Arguments to pass to the workflow.</param>
    /// <returns>The workflow result.</returns>
    public static async Task<TResult> ExecuteAsync<TResult>(string workflowType, string? idPostfix = null, params object[] args)
    {
        if (Workflow.InWorkflow)
        {
            // Within a workflow - execute as child workflow
            _logger.LogInformation(
                "Executing child workflow '{WorkflowType}' from parent '{ParentWorkflowId}'",
                workflowType,
                XiansContext.WorkflowId);

            var options = new SubWorkflowOptions(workflowType, idPostfix);
            return await Workflow.ExecuteChildWorkflowAsync<TResult>(workflowType, args, options);
        }
        else
        {
            // Outside a workflow - execute via client
            _logger.LogInformation(
                "Executing workflow '{WorkflowType}' via client (not in workflow context)",
                workflowType);

            return await ExecuteViaClientAsync<TResult>(workflowType, idPostfix, args);
        }
    }

    /// <summary>
    /// Extracts the workflow type from a workflow class using the WorkflowAttribute.
    /// </summary>
    private static string GetWorkflowTypeFromClass<TWorkflow>()
    {
        var workflowAttr = typeof(TWorkflow).GetCustomAttribute<WorkflowAttribute>();
        if (workflowAttr?.Name == null)
        {
            throw new InvalidOperationException(
                $"Workflow class '{typeof(TWorkflow).Name}' does not have a WorkflowAttribute with a Name property set.");
        }

        return workflowAttr.Name;
    }

    /// <summary>
    /// Starts a workflow via the Temporal client (out-of-workflow scenario).
    /// Requires access to the agent to get the Temporal client.
    /// </summary>
    private static async Task StartViaClientAsync(string workflowType, string? idPostfix, object[] args)
    {
        var (client, tenantId, systemScoped, agentName) = await GetClientAndContextAsync(workflowType);

        // Build workflow ID manually (can't use TenantContext.BuildWorkflowId because it requires CurrentAgent)
        var workflowId = BuildWorkflowIdManually(agentName, workflowType, tenantId, idPostfix);
        var taskQueue = TenantContext.GetTaskQueueName(workflowType, systemScoped, tenantId, agentName);

        var options = new WorkflowOptions
        {
            Id = workflowId,
            TaskQueue = taskQueue,
            IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting
        };

        await client.StartWorkflowAsync(workflowType, args, options);

        _logger.LogInformation(
            "Started workflow via client: WorkflowId='{WorkflowId}', TaskQueue='{TaskQueue}'",
            workflowId,
            taskQueue);
    }

    /// <summary>
    /// Executes a workflow via the Temporal client (out-of-workflow scenario).
    /// Requires access to the agent to get the Temporal client.
    /// </summary>
    private static async Task<TResult> ExecuteViaClientAsync<TResult>(string workflowType, string? idPostfix, object[] args)
    {
        var (client, tenantId, systemScoped, agentName) = await GetClientAndContextAsync(workflowType);

        // Build workflow ID manually (can't use TenantContext.BuildWorkflowId because it requires CurrentAgent)
        var workflowId = BuildWorkflowIdManually(agentName, workflowType, tenantId, idPostfix);
        var taskQueue = TenantContext.GetTaskQueueName(workflowType, systemScoped, tenantId, agentName);

        var options = new WorkflowOptions
        {
            Id = workflowId,
            TaskQueue = taskQueue,
            IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting
        };

        var result = await client.ExecuteWorkflowAsync<TResult>(workflowType, args, options);

        _logger.LogInformation(
            "Executed workflow via client: WorkflowId='{WorkflowId}', TaskQueue='{TaskQueue}'",
            workflowId,
            taskQueue);

        return result;
    }

    /// <summary>
    /// Gets the Temporal client and context information for a workflow.
    /// Extracts the agent name from the workflow type and retrieves the agent from XiansContext.
    /// </summary>
    private static async Task<(ITemporalClient client, string tenantId, bool systemScoped, string agentName)> GetClientAndContextAsync(string workflowType)
    {
        // Extract agent name from workflow type
        var agentName = workflowType.Contains(':')
            ? workflowType.Split(':')[0]
            : throw new InvalidOperationException(
                $"Invalid workflow type '{workflowType}'. Expected format: 'AgentName:WorkflowName'");

        // Get agent from registry
        var agent = XiansContext.GetAgent(agentName);

        if (agent.TemporalService == null)
        {
            throw new InvalidOperationException(
                $"Agent '{agentName}' does not have a Temporal service configured.");
        }

        // Get Temporal client
        var client = await agent.TemporalService.GetClientAsync();

        // Get tenant ID (null for system-scoped agents)
        if (!agent.SystemScoped)
        {
            if (agent.Options == null)
            {
                throw new InvalidOperationException(
                    $"Agent '{agentName}' is not system-scoped but Options is null. Ensure XiansOptions is configured.");
            }
            
            if (string.IsNullOrWhiteSpace(agent.Options.CertificateTenantId))
            {
                throw new InvalidOperationException(
                    $"Agent '{agentName}' is not system-scoped but CertificateTenantId is missing. Ensure API key is properly configured.");
            }
        }
        
        var tenantId = agent.SystemScoped ? "default" : agent.Options!.CertificateTenantId;

        return (client, tenantId, agent.SystemScoped, agentName);
    }

    /// <summary>
    /// Builds a workflow ID manually without relying on XiansContext.CurrentAgent.
    /// This is needed for out-of-workflow scenarios where there is no current agent context.
    /// </summary>
    private static string BuildWorkflowIdManually(string agentName, string workflowType, string tenantId, string? idPostfix)
    {
        // Extract workflow name from workflow type (format: "AgentName:WorkflowName")
        var workflowName = workflowType.Contains(':') 
            ? workflowType.Split(':')[1] 
            : workflowType;

        // Build workflow ID: {tenantId}:{agentName}:{workflowName}[:{idPostfix}]
        var workflowId = $"{tenantId}:{agentName}:{workflowName}";
        
        if (!string.IsNullOrWhiteSpace(idPostfix))
        {
            workflowId += $":{idPostfix}";
        }

        return workflowId;
    }
}

