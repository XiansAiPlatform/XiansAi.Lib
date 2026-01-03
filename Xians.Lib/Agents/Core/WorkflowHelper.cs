using Xians.Lib.Agents.Workflows;

namespace Xians.Lib.Agents.Core;

/// <summary>
/// Helper for sub-workflow execution operations.
/// Provides methods to start and execute child workflows.
/// </summary>
public class WorkflowHelper
{
    /// <summary>
    /// Starts a child workflow without waiting for its completion.
    /// If called from within a workflow, starts a child workflow.
    /// If called outside a workflow, starts a new workflow using the Temporal client.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow class type.</typeparam>
    /// <param name="idPostfix">Optional postfix for workflow ID uniqueness.</param>
    /// <param name="args">Arguments to pass to the workflow.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task StartAsync<TWorkflow>(string? idPostfix = null, params object[] args)
    {
        await SubWorkflowService.StartAsync<TWorkflow>(idPostfix, args);
    }

    /// <summary>
    /// Starts a child workflow without waiting for its completion.
    /// If called from within a workflow, starts a child workflow.
    /// If called outside a workflow, starts a new workflow using the Temporal client.
    /// </summary>
    /// <param name="workflowType">The workflow type (format: "AgentName:WorkflowName").</param>
    /// <param name="idPostfix">Optional postfix for workflow ID uniqueness.</param>
    /// <param name="args">Arguments to pass to the workflow.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task StartAsync(string workflowType, string? idPostfix = null, params object[] args)
    {
        await SubWorkflowService.StartAsync(workflowType, idPostfix, args);
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
    public async Task<TResult> ExecuteAsync<TWorkflow, TResult>(string? idPostfix = null, params object[] args)
    {
        return await SubWorkflowService.ExecuteAsync<TWorkflow, TResult>(idPostfix, args);
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
    public async Task<TResult> ExecuteAsync<TResult>(string workflowType, string? idPostfix = null, params object[] args)
    {
        return await SubWorkflowService.ExecuteAsync<TResult>(workflowType, idPostfix, args);
    }
}



