using System.Text.Json;
using Temporalio.Client;
using Xians.Lib.Agents.Core;

namespace Xians.Lib.Temporal.Workflows.A2A;

/// <summary>
/// Service for executing A2A signals, queries, and updates to custom workflows using Temporal client.
/// This service is called from activities (via ContextAwareActivityExecutor pattern) or external context.
/// 
/// Architecture (ContextAwareActivityExecutor pattern):
/// - From workflow: Executor → Activity → Service (this class) → TemporalClient
/// - From activity: Executor → Service (this class) → TemporalClient
/// </summary>
internal class A2ASignalQueryService
{
    /// <summary>
    /// Sends a signal to a workflow using the Temporal client.
    /// This method is called from activities (via the executor pattern) or external context.
    /// Always uses the Temporal client - when called from workflows, this is invoked via activities.
    /// </summary>
    public async Task SendSignalAsync(string workflowId, string signalName, object[] args)
    {
        var client = await GetTemporalClientAsync();
        var handle = client.GetWorkflowHandle(workflowId);
        await handle.SignalAsync(signalName, args);
    }

    /// <summary>
    /// Queries a workflow using Temporal SDK's string-based API.
    /// This method is called from activities (via the executor pattern) or external context.
    /// Always uses the Temporal client - queries go through activities when called from workflows.
    /// </summary>
    public async Task<TResult> QueryAsync<TResult>(string workflowId, string queryName, object[] args)
    {
        var client = await GetTemporalClientAsync();
        var handle = client.GetWorkflowHandle(workflowId);
        
        // Use the typed QueryAsync method directly
        return await handle.QueryAsync<TResult>(queryName, args);
    }

    /// <summary>
    /// Sends an update to a workflow using Temporal SDK's string-based API.
    /// This method is called from activities (via the executor pattern) or external context.
    /// Always uses the Temporal client - updates go through activities when called from workflows.
    /// </summary>
    public async Task<TResult> ExecuteUpdateAsync<TResult>(string workflowId, string updateName, object[] args)
    {
        var client = await GetTemporalClientAsync();
        var handle = client.GetWorkflowHandle(workflowId);
        
        // Use the typed ExecuteUpdateAsync method directly
        return await handle.ExecuteUpdateAsync<TResult>(updateName, args);
    }

    /// <summary>
    /// Gets the Temporal client from the current agent context.
    /// </summary>
    private static async Task<ITemporalClient> GetTemporalClientAsync()
    {
        var agent = XiansContext.CurrentAgent;
        
        if (agent.TemporalService == null)
        {
            throw new InvalidOperationException(
                $"Agent '{agent.Name}' does not have a Temporal service configured.");
        }

        return await agent.TemporalService.GetClientAsync();
    }
}

