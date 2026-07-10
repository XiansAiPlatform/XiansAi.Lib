using System.Net.Http.Json;
using System.Reflection;
using Temporalio.Client;
using Temporalio.Workflows;
using Xians.Lib.Agents.Workflows;
using Xians.Lib.Common;
using Xians.Lib.Temporal;

namespace Xians.Lib.Agents.Core;

/// <summary>
/// Helper for workflow operations including execution, signaling, querying, and Temporal client access.
/// Provides methods to start and execute child workflows, get workflow handles, and access the Temporal client.
/// </summary>
public class WorkflowHelper
{
    /// <summary>
    /// Starts a child workflow without waiting for its completion.
    /// If called from within a workflow, starts a child workflow.
    /// If called outside a workflow, starts a new workflow using the Temporal client.
    /// The caller's idPostfix (activation name) is inherited only for same-agent children;
    /// for cross-agent children, pass <paramref name="activationName"/> to target a specific activation.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow class type.</typeparam>
    /// <param name="args">Arguments to pass to the workflow.</param>
    /// <param name="uniqueKey">Optional unique key for workflow ID uniqueness (appended after the child's idPostfix).</param>
    /// <param name="executionTimeout">Optional workflow execution timeout.</param>
    /// <param name="activationName">Optional target activation name (idPostfix) for the child workflow.
    /// Use this when starting a workflow that belongs to a different agent and should run under
    /// one of that agent's activations.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task StartAsync<TWorkflow>(object[] args, string? uniqueKey = null, TimeSpan? executionTimeout = null, string? activationName = null)
    {
        var workflowType = GetWorkflowTypeFromClass<TWorkflow>();
        await StartAsync(workflowType, args, uniqueKey, executionTimeout, activationName);
    }

    /// <summary>
    /// Starts a child workflow without waiting for its completion.
    /// If called from within a workflow, starts a child workflow.
    /// If called outside a workflow, starts a new workflow using the Temporal client.
    /// The caller's idPostfix (activation name) is inherited only for same-agent children;
    /// for cross-agent children, pass <paramref name="activationName"/> to target a specific activation.
    /// </summary>
    /// <param name="workflowType">The workflow type (format: "AgentName:WorkflowName").</param>
    /// <param name="args">Arguments to pass to the workflow.</param>
    /// <param name="uniqueKey">Optional unique key for workflow ID uniqueness (appended after the child's idPostfix).</param>
    /// <param name="executionTimeout">Optional workflow execution timeout.</param>
    /// <param name="activationName">Optional target activation name (idPostfix) for the child workflow.
    /// Use this when starting a workflow that belongs to a different agent and should run under
    /// one of that agent's activations.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task StartAsync(string workflowType, object[] args, string? uniqueKey = null, TimeSpan? executionTimeout = null, string? activationName = null)
    {
        // Validate workflowType is not null
        _ = workflowType.Length;

        var uniqueKeys = BuildChildUniqueKeys(workflowType, uniqueKey, activationName);
        await SubWorkflowService.StartCoreAsync(workflowType, uniqueKeys, activationName, executionTimeout, args);
    }

    /// <summary>
    /// Executes a child workflow and waits for its result.
    /// If called from within a workflow, executes a child workflow.
    /// If called outside a workflow, executes a workflow using the Temporal client.
    /// The caller's idPostfix (activation name) is inherited only for same-agent children;
    /// for cross-agent children, pass <paramref name="activationName"/> to target a specific activation.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow class type.</typeparam>
    /// <typeparam name="TResult">The expected result type.</typeparam>
    /// <param name="args">Arguments to pass to the workflow.</param>
    /// <param name="uniqueKey">Optional unique key for workflow ID uniqueness (appended after the child's idPostfix).</param>
    /// <param name="executionTimeout">Optional workflow execution timeout.</param>
    /// <param name="activationName">Optional target activation name (idPostfix) for the child workflow.
    /// Use this when starting a workflow that belongs to a different agent and should run under
    /// one of that agent's activations.</param>
    /// <returns>The workflow result.</returns>
    public async Task<TResult> ExecuteAsync<TWorkflow, TResult>(object[] args, string? uniqueKey = null, TimeSpan? executionTimeout = null, string? activationName = null)
    {
        var workflowType = GetWorkflowTypeFromClass<TWorkflow>();
        return await ExecuteAsync<TResult>(workflowType, args, uniqueKey, executionTimeout, activationName);
    }

    /// <summary>
    /// Executes a child workflow and waits for its result.
    /// If called from within a workflow, executes a child workflow.
    /// If called outside a workflow, executes a workflow using the Temporal client.
    /// The caller's idPostfix (activation name) is inherited only for same-agent children;
    /// for cross-agent children, pass <paramref name="activationName"/> to target a specific activation.
    /// </summary>
    /// <typeparam name="TResult">The expected result type.</typeparam>
    /// <param name="workflowType">The workflow type (format: "AgentName:WorkflowName").</param>
    /// <param name="args">Arguments to pass to the workflow.</param>
    /// <param name="uniqueKey">Optional unique key for workflow ID uniqueness (appended after the child's idPostfix).</param>
    /// <param name="executionTimeout">Optional workflow execution timeout.</param>
    /// <param name="activationName">Optional target activation name (idPostfix) for the child workflow.
    /// Use this when starting a workflow that belongs to a different agent and should run under
    /// one of that agent's activations.</param>
    /// <returns>The workflow result.</returns>
    public async Task<TResult> ExecuteAsync<TResult>(string workflowType, object[] args, string? uniqueKey = null, TimeSpan? executionTimeout = null, string? activationName = null)
    {
        // Validate workflowType is not null
        _ = workflowType.Length;

        var uniqueKeys = BuildChildUniqueKeys(workflowType, uniqueKey, activationName);
        return await SubWorkflowService.ExecuteCoreAsync<TResult>(workflowType, uniqueKeys, activationName, executionTimeout, args);
    }

    /// <summary>
    /// Builds the workflow ID unique keys for a child workflow: the resolved child idPostfix
    /// (explicit activation name, or the caller's idPostfix for same-agent children) followed
    /// by the optional caller-provided unique key.
    /// </summary>
    private static string[] BuildChildUniqueKeys(string workflowType, string? uniqueKey, string? activationName)
    {
        var childIdPostfix = SubWorkflowService.ResolveChildIdPostfix(workflowType, activationName);
        var uniqueKeys = new List<string>();

        if (!string.IsNullOrWhiteSpace(childIdPostfix))
            uniqueKeys.Add(childIdPostfix);

        if (!string.IsNullOrWhiteSpace(uniqueKey))
            uniqueKeys.Add(uniqueKey);

        return uniqueKeys.ToArray();
    }

    /// <summary>
    /// Sends a signal to a workflow execution.
    /// If called from within a workflow, uses the external workflow handle to signal another workflow.
    /// If called outside a workflow, uses the Temporal client to signal the workflow.
    /// Workflow ID is built from context only (idPostfix when in workflow/activity); unique keys cannot be passed externally.
    /// The workflow must already be running; signals cannot be sent to closed workflows.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow class type.</typeparam>
    /// <param name="signalName">The name of the signal to send (must match a handler with <see cref="WorkflowSignalAttribute"/>).</param>
    /// <param name="signalArgs">Arguments to pass to the signal handler.</param>
    /// <returns>A task representing the asynchronous operation. Returns when the server accepts the signal.</returns>
    public async Task SignalAsync<TWorkflow>(string signalName, params object[] signalArgs)
    {
        await SubWorkflowService.SignalAsync<TWorkflow>(signalName, signalArgs);
    }

    /// <summary>
    /// Sends a signal to a workflow execution.
    /// If called from within a workflow, uses the external workflow handle to signal another workflow.
    /// If called outside a workflow, uses the Temporal client to signal the workflow.
    /// Workflow ID is built from context only (idPostfix when in workflow/activity); unique keys cannot be passed externally.
    /// The workflow must already be running; signals cannot be sent to closed workflows.
    /// </summary>
    /// <param name="workflowType">The workflow type (format: "AgentName:WorkflowName").</param>
    /// <param name="signalName">The name of the signal to send (must match a handler with <see cref="WorkflowSignalAttribute"/>).</param>
    /// <param name="signalArgs">Arguments to pass to the signal handler.</param>
    /// <returns>A task representing the asynchronous operation. Returns when the server accepts the signal.</returns>
    public async Task SignalAsync(string workflowType, string signalName, params object[] signalArgs)
    {
        _ = workflowType.Length;
        await SubWorkflowService.SignalAsync(workflowType, signalName, signalArgs);
    }

    /// <summary>
    /// Sends a signal to a workflow execution running under a specific activation.
    /// Use this to signal a workflow that was started with an explicit <c>activationName</c>
    /// (typically a cross-agent workflow), since the target workflow ID includes the activation's idPostfix.
    /// The workflow must already be running; signals cannot be sent to closed workflows.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow class type.</typeparam>
    /// <param name="signalName">The name of the signal to send (must match a handler with <see cref="WorkflowSignalAttribute"/>).</param>
    /// <param name="signalArgs">Arguments to pass to the signal handler.</param>
    /// <param name="activationName">The target activation name (idPostfix) the workflow runs under.</param>
    /// <returns>A task representing the asynchronous operation. Returns when the server accepts the signal.</returns>
    public async Task SignalAsync<TWorkflow>(string signalName, object[] signalArgs, string activationName)
    {
        var workflowType = GetWorkflowTypeFromClass<TWorkflow>();
        await SubWorkflowService.SignalCoreAsync(workflowType, signalName, activationName, signalArgs);
    }

    /// <summary>
    /// Sends a signal to a workflow execution running under a specific activation.
    /// Use this to signal a workflow that was started with an explicit <c>activationName</c>
    /// (typically a cross-agent workflow), since the target workflow ID includes the activation's idPostfix.
    /// The workflow must already be running; signals cannot be sent to closed workflows.
    /// </summary>
    /// <param name="workflowType">The workflow type (format: "AgentName:WorkflowName").</param>
    /// <param name="signalName">The name of the signal to send (must match a handler with <see cref="WorkflowSignalAttribute"/>).</param>
    /// <param name="signalArgs">Arguments to pass to the signal handler.</param>
    /// <param name="activationName">The target activation name (idPostfix) the workflow runs under.</param>
    /// <returns>A task representing the asynchronous operation. Returns when the server accepts the signal.</returns>
    public async Task SignalAsync(string workflowType, string signalName, object[] signalArgs, string activationName)
    {
        _ = workflowType.Length;
        await SubWorkflowService.SignalCoreAsync(workflowType, signalName, activationName, signalArgs);
    }

    /// <summary>
    /// Sends a signal to a workflow, starting it if it does not already exist (signal-with-start).
    /// Fetches the workflow input arguments from the server activation configuration before starting.
    /// Client-only operation; throws when called from within a workflow.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow class type.</typeparam>
    /// <param name="signalName">The name of the signal to send.</param>
    /// <param name="signalArgs">Arguments to pass to the signal handler.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// The activation name is resolved implicitly from the current execution context via
    /// <see cref="XiansContext.TryGetIdPostfix"/>. An <see cref="InvalidOperationException"/>
    /// is thrown when no activation name is available in context.
    /// </remarks>
    public async Task SignalWithActivationStartAsync<TWorkflow>(
        string signalName,
        params object[] signalArgs)
    {
        var activationName = XiansContext.TryGetIdPostfix();
        if (string.IsNullOrWhiteSpace(activationName))
        {
            throw new InvalidOperationException("Activation name is required to signal with activation start.");
        }

        var workflowType = GetWorkflowTypeFromClass<TWorkflow>();
        var agent = XiansContext.CurrentAgent;
        string tenantId = XiansContext.GetTenantId();

        var workflowId = BuildWorkflowId(agent.Name, workflowType, tenantId, activationName);
        var workflowArgs = await FetchWorkflowArgsFromServerAsync(agent, activationName, workflowType, workflowId, tenantId);

        await SubWorkflowService.SignalWithStartAsync<TWorkflow>(
            [activationName],
            workflowArgs,
            signalName,
            signalArgs);
    }


    /// <summary>
    /// Sends a signal to a workflow, starting it if it does not already exist (signal-with-start).
    /// Client-only operation; throws when called from within a workflow.
    /// If a workflow with the given ID exists, it will be signaled. If not, a new workflow is started and immediately signaled.
    /// The caller's idPostfix (activation name) is included only for same-agent targets,
    /// mirroring how StartAsync/ExecuteAsync build child workflow IDs;
    /// for cross-agent targets, pass <paramref name="activationName"/> to target a specific activation.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow class type.</typeparam>
    /// <param name="workflowArgs">Arguments to pass when starting the workflow (used only if workflow does not exist).</param>
    /// <param name="signalName">The name of the signal to send.</param>
    /// <param name="uniqueKey">Optional unique key for workflow ID (appended after the resolved idPostfix).</param>
    /// <param name="executionTimeout">Optional workflow execution timeout.</param>
    /// <param name="activationName">Optional target activation name (idPostfix) for the workflow.
    /// When provided, the SDK validates that the activation exists and is active before starting.
    /// When null, the caller's idPostfix is used only for same-agent targets.</param>
    /// <param name="signalArgs">Arguments to pass to the signal handler.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when called from within a workflow.</exception>
    /// <exception cref="Workflows.ActivationNotFoundException">Thrown when the explicit activation does not exist.</exception>
    /// <exception cref="Workflows.ActivationDeactivatedException">Thrown when the explicit activation is deactivated.</exception>
    public async Task SignalWithStartAsync<TWorkflow>(
        object[] workflowArgs,
        string signalName,
        string? uniqueKey = null,
        TimeSpan? executionTimeout = null,
        string? activationName = null,
        params object[] signalArgs)
    {
        var workflowType = GetWorkflowTypeFromClass<TWorkflow>();
        var uniqueKeys = BuildChildUniqueKeys(workflowType, uniqueKey, activationName);

        await SubWorkflowService.SignalWithStartCoreAsync(
            workflowType,
            uniqueKeys,
            workflowArgs,
            signalName,
            signalArgs,
            activationName,
            executionTimeout);
    }

    /// <summary>
    /// Sends a signal to a workflow, starting it if it does not already exist (signal-with-start).
    /// Client-only operation; throws when called from within a workflow.
    /// If a workflow with the given ID exists, it will be signaled. If not, a new workflow is started and immediately signaled.
    /// The caller's idPostfix (activation name) is included only for same-agent targets,
    /// mirroring how StartAsync/ExecuteAsync build child workflow IDs;
    /// for cross-agent targets, pass <paramref name="activationName"/> to target a specific activation.
    /// </summary>
    /// <param name="workflowType">The workflow type (format: "AgentName:WorkflowName").</param>
    /// <param name="workflowArgs">Arguments to pass when starting the workflow (used only if workflow does not exist).</param>
    /// <param name="signalName">The name of the signal to send.</param>
    /// <param name="uniqueKey">Optional unique key for workflow ID (appended after the resolved idPostfix).</param>
    /// <param name="executionTimeout">Optional workflow execution timeout.</param>
    /// <param name="activationName">Optional target activation name (idPostfix) for the workflow.
    /// When provided, the SDK validates that the activation exists and is active before starting.
    /// When null, the caller's idPostfix is used only for same-agent targets.</param>
    /// <param name="signalArgs">Arguments to pass to the signal handler.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when called from within a workflow.</exception>
    /// <exception cref="Workflows.ActivationNotFoundException">Thrown when the explicit activation does not exist.</exception>
    /// <exception cref="Workflows.ActivationDeactivatedException">Thrown when the explicit activation is deactivated.</exception>
    public async Task SignalWithStartAsync(
        string workflowType,
        object[] workflowArgs,
        string signalName,
        string? uniqueKey = null,
        TimeSpan? executionTimeout = null,
        string? activationName = null,
        params object[] signalArgs)
    {
        _ = workflowType.Length;

        var uniqueKeys = BuildChildUniqueKeys(workflowType, uniqueKey, activationName);

        await SubWorkflowService.SignalWithStartCoreAsync(
            workflowType,
            uniqueKeys,
            workflowArgs,
            signalName,
            signalArgs,
            activationName,
            executionTimeout);
    }

    #region Temporal Client Access

    /// <summary>
    /// Gets the Temporal client from the current agent's context.
    /// Since all agents share the same Temporal connection, this returns the shared client.
    /// Works both inside and outside workflow/activity contexts.
    /// </summary>
    /// <returns>The Temporal client instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Temporal service is not configured.</exception>
    public async Task<ITemporalClient> GetClientAsync()
    {
        var agent = GetAgentForTemporalAccess();
        return await GetClientFromAgentAsync(agent);
    }

    /// <summary>
    /// Gets the Temporal client service from the current agent's context.
    /// This provides access to additional service methods like health checking and reconnection.
    /// Since all agents share the same Temporal connection, this returns the shared service.
    /// Works both inside and outside workflow/activity contexts.
    /// </summary>
    /// <returns>The Temporal client service instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Temporal service is not configured.</exception>
    public ITemporalClientService GetService()
    {
        var agent = GetAgentForTemporalAccess();
        return GetServiceFromAgent(agent);
    }

    #endregion

    #region Workflow Handle Access

    /// <summary>
    /// Gets a workflow handle for a specific workflow using its class type and ID postfix.
    /// Automatically constructs the workflow ID using the same format as when workflows are created.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow class type.</typeparam>
    /// <param name="idPostfix">Optional postfix for workflow ID. If null, only base workflow ID is used.</param>
    /// <returns>A workflow handle that can be used to signal, query, or get results from the workflow.</returns>
    /// <exception cref="InvalidOperationException">Thrown when workflow type cannot be determined or agent not configured.</exception>
    public async Task<WorkflowHandle<TWorkflow>> GetWorkflowHandleAsync<TWorkflow>(string? idPostfix = null)
    {
        // Extract workflow type from the class
        var workflowType = GetWorkflowTypeFromClass<TWorkflow>();
        
        // Extract agent name from workflow type
        var agentName = workflowType.Contains(':')
            ? workflowType.Split(':')[0]
            : throw new InvalidOperationException(
                $"Invalid workflow type '{workflowType}'. Expected format: 'AgentName:WorkflowName'");

        // Get agent
        var agent = XiansContext.GetAgent(agentName);
        
        // Determine tenant ID from agent options (non-system-scoped) or from workflow context (system-scoped)
        string tenantId;
        if (agent.SystemScoped)
        {
            tenantId = XiansContext.GetTenantId();
        }
        else
        {
            if (agent.Options == null || string.IsNullOrWhiteSpace(agent.Options.CertificateTenantId))
            {
                throw new InvalidOperationException(
                    $"Agent '{agentName}' is not system-scoped but tenant ID is missing. Ensure API key is properly configured.");
            }
            tenantId = agent.Options.CertificateTenantId;
        }

        // Build workflow ID using the same format as SubWorkflowService
        var workflowId = BuildWorkflowId(agentName, workflowType, tenantId, idPostfix);

        // Get Temporal client
        var client = await GetClientFromAgentAsync(agent);

        // Return workflow handle
        return client.GetWorkflowHandle<TWorkflow>(workflowId);
    }

    /// <summary>
    /// Gets a workflow handle for a specific workflow using its class type and ID postfix, without specifying a generic type for the handle.
    /// Automatically constructs the workflow ID using the same format as when workflows are created.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow class type.</typeparam>
    /// <param name="idPostfix">Optional postfix for workflow ID. If null, only base workflow ID is used.</param>
    /// <returns>An untyped workflow handle that can be used to signal the workflow.</returns>
    /// <exception cref="InvalidOperationException">Thrown when workflow type cannot be determined or agent not configured.</exception>
    public async Task<WorkflowHandle> GetWorkflowHandleUntypedAsync<TWorkflow>(string? idPostfix = null)
    {
        // Extract workflow type from the class
        var workflowType = GetWorkflowTypeFromClass<TWorkflow>();
        
        // Extract agent name from workflow type
        var agentName = workflowType.Contains(':')
            ? workflowType.Split(':')[0]
            : throw new InvalidOperationException(
                $"Invalid workflow type '{workflowType}'. Expected format: 'AgentName:WorkflowName'");

        // Get agent
        var agent = XiansContext.GetAgent(agentName);
        
        // Determine tenant ID from agent options (non-system-scoped) or from workflow context (system-scoped)
        string tenantId;
        if (agent.SystemScoped)
        {
            tenantId = XiansContext.GetTenantId();
        }
        else
        {
            if (agent.Options == null || string.IsNullOrWhiteSpace(agent.Options.CertificateTenantId))
            {
                throw new InvalidOperationException(
                    $"Agent '{agentName}' is not system-scoped but tenant ID is missing. Ensure API key is properly configured.");
            }
            tenantId = agent.Options.CertificateTenantId;
        }

        // Build workflow ID using the same format as SubWorkflowService
        var workflowId = BuildWorkflowId(agentName, workflowType, tenantId, idPostfix);

        // Get Temporal client
        var client = await GetClientFromAgentAsync(agent);

        // Return workflow handle
        return client.GetWorkflowHandle(workflowId);
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Fetches ordered workflow input values from the server for a given activation.
    /// Returns an empty array when the activation has no inputs configured for the workflow type.
    /// For system-scoped agents, the <paramref name="tenantId"/> is sent as the
    /// <c>X-Tenant-Id</c> request header so the server can resolve the correct tenant context.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the HTTP service is unavailable or the server returns an error.</exception>
    private static async Task<object[]> FetchWorkflowArgsFromServerAsync(
        XiansAgent agent,
        string activationName,
        string workflowType,
        string workflowId,
        string? tenantId = null)
    {
        var agentName = agent.Name;

        if (agent.HttpService == null)
        {
            throw new InvalidOperationException(
                $"Agent '{agentName}' does not have an HTTP service configured. Cannot fetch workflow inputs for activation '{activationName}'.");
        }

        var client = await agent.HttpService.GetHealthyClientAsync();
        var url = $"{WorkflowConstants.ApiEndpoints.ActivationWorkflowInputs}" +
                  $"?activationName={Uri.EscapeDataString(activationName)}" +
                  $"&agentName={Uri.EscapeDataString(agentName)}" +
                  $"&workflowType={Uri.EscapeDataString(workflowType)}" +
                  $"&workflowId={Uri.EscapeDataString(workflowId)}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (agent.SystemScoped && !string.IsNullOrWhiteSpace(tenantId))
        {
            request.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, tenantId);
        }

        var response = await client.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Failed to fetch workflow inputs for activation '{activationName}'. " +
                $"Status: {response.StatusCode}, Error: {errorContent}");
        }

        return await response.Content.ReadFromJsonAsync<object[]>() ?? [];
    }

    /// <summary>
    /// Gets the appropriate agent for Temporal client access.
    /// Tries to get current agent if in workflow context, otherwise gets first registered agent.
    /// </summary>
    private XiansAgent GetAgentForTemporalAccess()
    {
        // Try to get current agent if in workflow/activity context
        if (XiansContext.InWorkflow || XiansContext.InActivity)
        {
            try
            {
                return XiansContext.CurrentAgent;
            }
            catch
            {
                // Fall through to get first agent
            }
        }

        // Not in workflow context or CurrentAgent failed - get first registered agent
        // Since all agents share the same Temporal connection, it doesn't matter which one we use
        var agents = XiansContext.GetAllAgents().ToList();
        if (agents.Count == 0)
        {
            throw new InvalidOperationException(
                "No agents registered. Cannot obtain Temporal client. " +
                "Ensure at least one agent is registered with XiansPlatform before calling GetClientAsync().");
        }

        return agents[0]; // All agents share the same Temporal connection
    }

    /// <summary>
    /// Internal helper to get Temporal client from an agent.
    /// </summary>
    private async Task<ITemporalClient> GetClientFromAgentAsync(XiansAgent agent)
    {
        if (agent.TemporalService == null)
        {
            throw new InvalidOperationException(
                $"Agent '{agent.Name}' does not have a Temporal service configured. " +
                "Ensure the agent was registered with a properly configured XiansPlatform.");
        }

        return await agent.TemporalService.GetClientAsync();
    }

    /// <summary>
    /// Internal helper to get Temporal service from an agent.
    /// </summary>
    private ITemporalClientService GetServiceFromAgent(XiansAgent agent)
    {
        if (agent.TemporalService == null)
        {
            throw new InvalidOperationException(
                $"Agent '{agent.Name}' does not have a Temporal service configured. " +
                "Ensure the agent was registered with a properly configured XiansPlatform.");
        }

        return agent.TemporalService;
    }

    /// <summary>
    /// Resolves the runtime workflow type for a workflow class.
    /// Delegates to <see cref="XiansContext.GetWorkflowTypeFor(Type)"/> so any runtime
    /// <c>typeName</c> override supplied to <c>DefineCustom&lt;T&gt;</c> is honoured —
    /// the <c>[Workflow]</c> attribute on the class is only consulted as a fallback
    /// when the class hasn't been registered.
    /// </summary>
    public static string GetWorkflowTypeFromClass<TWorkflow>()
    {
        return XiansContext.GetWorkflowTypeFor(typeof(TWorkflow));
    }

    /// <summary>
    /// Builds a workflow ID using the same format as SubWorkflowService.
    /// Format: {tenantId}:{agentName}:{workflowName}[:{idPostfix}]
    /// </summary>
    private static string BuildWorkflowId(string agentName, string workflowType, string tenantId, string? idPostfix)
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

    #endregion
}



