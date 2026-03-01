using Microsoft.Extensions.Logging;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common;
using Xians.Lib.Common.MultiTenancy;
using System.Reflection;
using System.Text.Json;

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
    private static readonly ILogger _logger = Common.Infrastructure.LoggerFactory.CreateLogger<SubWorkflowServiceLogger>();


    /// <summary>
    /// Starts a child workflow without waiting for its completion.
    /// If called from within a workflow, starts a child workflow.
    /// If called outside a workflow, starts a new workflow using the Temporal client.
    /// </summary>
    /// <param name="workflowType">The workflow type (format: "AgentName:WorkflowName").</param>
    /// <param name="uniqueKeys">Optional unique keys for workflow ID uniqueness.</param>
    /// <param name="executionTimeout">Optional workflow execution timeout.</param>
    /// <throws>WorkflowAlreadyStartedException if there is a running workflow with given unique keys</throws>
    /// <param name="args">Arguments to pass to the workflow.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task StartAsync(string workflowType, string[] uniqueKeys, TimeSpan? executionTimeout = null, params object[] args)
    {
        if (Workflow.InWorkflow)
        {
            // Within a workflow - start as child workflow
            _logger.LogDebug(
                "Starting child workflow '{WorkflowType}' from parent '{ParentWorkflowId}'",
                workflowType,
                XiansContext.WorkflowId);

            var options = new SubWorkflowOptions(workflowType, uniqueKeys);
            if (executionTimeout.HasValue)
            {
                options.ExecutionTimeout = executionTimeout.Value;
            }
            await Workflow.StartChildWorkflowAsync(workflowType, args, options);
        }
        else
        {
            // Outside a workflow - start as new workflow via client
            _logger.LogDebug(
                "Starting workflow '{WorkflowType}' via client (not in workflow context)",
                workflowType);

            await StartViaClientAsync(workflowType, uniqueKeys, executionTimeout, args);
        }
    }
    
    /// <summary>
    /// Starts a child workflow without waiting for its completion.
    /// If called from within a workflow, starts a child workflow.
    /// If called outside a workflow, starts a new workflow using the Temporal client.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow class type.</typeparam>
    /// <param name="uniqueKeys">Optional unique keys for workflow ID uniqueness.</param>
    /// <param name="executionTimeout">Optional workflow execution timeout.</param>
    /// <param name="args">Arguments to pass to the workflow.</param>
    /// <throws>WorkflowAlreadyStartedException if there is a running workflow with given unique keys</throws>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when workflow type cannot be determined or agent not found.</exception>
    public static async Task StartAsync<TWorkflow>(string[] uniqueKeys, TimeSpan? executionTimeout = null, params object[] args)
    {
        var workflowType = GetWorkflowTypeFromClass<TWorkflow>();
        await StartAsync(workflowType, uniqueKeys, executionTimeout, args);
    }

    /// <summary>
    /// Executes a child workflow and waits for its result.
    /// If called from within a workflow, executes a child workflow.
    /// If called outside a workflow, executes a workflow using the Temporal client.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow class type.</typeparam>
    /// <typeparam name="TResult">The expected result type.</typeparam>
    /// <param name="uniqueKeys">Optional postfix for workflow ID uniqueness.</param>
    /// <param name="executionTimeout">Optional workflow execution timeout.</param>
    /// <param name="args">Arguments to pass to the workflow.</param>
    /// <throws>WorkflowAlreadyStartedException if there is a running workflow with given unique keys</throws>
    /// <returns>The workflow result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when workflow type cannot be determined or agent not found.</exception>
    public static async Task<TResult> ExecuteAsync<TWorkflow, TResult>(string[] uniqueKeys, TimeSpan? executionTimeout = null, params object[] args)
    {
        var workflowType = GetWorkflowTypeFromClass<TWorkflow>();
        return await ExecuteAsync<TResult>(workflowType, uniqueKeys, executionTimeout, args);
    }

    /// <summary>
    /// Executes a child workflow and waits for its result.
    /// If called from within a workflow, executes a child workflow.
    /// If called outside a workflow, executes a workflow using the Temporal client.
    /// </summary>
    /// <typeparam name="TResult">The expected result type.</typeparam>
    /// <param name="workflowType">The workflow type (format: "AgentName:WorkflowName").</param>
    /// <param name="uniqueKeys">Optional uniqueKeys for workflow ID uniqueness.</param>
    /// <param name="executionTimeout">Optional workflow execution timeout.</param>
    /// <param name="args">Arguments to pass to the workflow.</param>
    /// <throws>WorkflowAlreadyStartedException if there is a running workflow with given unique keys</throws>
    /// <returns>The workflow result.</returns>
    public static async Task<TResult> ExecuteAsync<TResult>(string workflowType, string[] uniqueKeys, TimeSpan? executionTimeout = null, params object[] args)
    {
        if (Workflow.InWorkflow)
        {
            // Within a workflow - execute as child workflow
            _logger.LogDebug(
                "Executing child workflow '{WorkflowType}' from parent '{ParentWorkflowId}'",
                workflowType,
                XiansContext.WorkflowId);

            var options = new SubWorkflowOptions(workflowType, uniqueKeys);
            if (executionTimeout.HasValue)
            {
                options.ExecutionTimeout = executionTimeout.Value;
            }
            return await Workflow.ExecuteChildWorkflowAsync<TResult>(workflowType, args, options);
        }
        else
        {
            // Outside a workflow - execute via client
            _logger.LogDebug(
                "Executing workflow '{WorkflowType}' via client (not in workflow context)",
                workflowType);

            return await ExecuteViaClientAsync<TResult>(workflowType, uniqueKeys, executionTimeout, args);
        }
    }

    /// <summary>
    /// Sends a signal to a workflow execution.
    /// If called from within a workflow, uses the external workflow handle to signal another workflow.
    /// If called outside a workflow, uses the Temporal client to signal the workflow.
    /// The workflow must already be running; signals cannot be sent to closed workflows.
    /// Workflow ID is built from context only (idPostfix when in workflow/activity); users cannot pass unique keys externally.
    /// See <see href="https://docs.temporal.io/develop/dotnet/message-passing#send-signal-from-client"/>.
    /// </summary>
    /// <param name="workflowType">The workflow type (format: "AgentName:WorkflowName").</param>
    /// <param name="signalName">The name of the signal to send (must match a handler with <see cref="WorkflowSignalAttribute"/>).</param>
    /// <param name="signalArgs">Arguments to pass to the signal handler.</param>
    /// <returns>A task representing the asynchronous operation. Returns when the server accepts the signal; does not wait for delivery to the workflow.</returns>
    public static async Task SignalAsync(string workflowType, string signalName, params object[] signalArgs)
    {
        var uniqueKeys = GetUniqueKeysFromContext();
        if (Workflow.InWorkflow)
        {
            _logger.LogDebug(
                "Sending signal '{SignalName}' to workflow '{WorkflowType}' from parent '{ParentWorkflowId}'",
                signalName,
                workflowType,
                XiansContext.WorkflowId);

            var workflowId = GetWorkflowIdForSignal(workflowType, uniqueKeys);
            var handle = Workflow.GetExternalWorkflowHandle(workflowId);
            await handle.SignalAsync(signalName, signalArgs);
        }
        else
        {
            _logger.LogDebug(
                "Sending signal '{SignalName}' to workflow '{WorkflowType}' via client",
                signalName,
                workflowType);

            await SignalViaClientAsync(workflowType, uniqueKeys, signalName, signalArgs);
        }
    }

    /// <summary>
    /// Sends a signal to a workflow execution.
    /// If called from within a workflow, uses the external workflow handle to signal another workflow.
    /// If called outside a workflow, uses the Temporal client to signal the workflow.
    /// The workflow must already be running; signals cannot be sent to closed workflows.
    /// Workflow ID is built from context only (idPostfix when in workflow/activity); users cannot pass unique keys externally.
    /// See <see href="https://docs.temporal.io/develop/dotnet/message-passing#send-signal-from-client"/>.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow class type.</typeparam>
    /// <param name="signalName">The name of the signal to send (must match a handler with <see cref="WorkflowSignalAttribute"/>).</param>
    /// <param name="signalArgs">Arguments to pass to the signal handler.</param>
    /// <returns>A task representing the asynchronous operation. Returns when the server accepts the signal; does not wait for delivery to the workflow.</returns>
    /// <exception cref="InvalidOperationException">Thrown when workflow type cannot be determined or agent not found.</exception>
    public static async Task SignalAsync<TWorkflow>(string signalName, params object[] signalArgs)
    {
        var workflowType = GetWorkflowTypeFromClass<TWorkflow>();
        await SignalAsync(workflowType, signalName, signalArgs);
    }

    /// <summary>
    /// Sends a signal to a workflow, starting it if it does not already exist (signal-with-start).
    /// Client-only operation; not supported when called from within a workflow.
    /// If a workflow with the given ID exists, it will be signaled. If not, a new workflow is started and immediately signaled.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow class type.</typeparam>
    /// <param name="uniqueKeys">Unique keys for workflow ID (e.g. idPostfix, session ID).</param>
    /// <param name="workflowArgs">Arguments to pass when starting the workflow (used only if workflow does not exist).</param>
    /// <param name="signalName">The name of the signal to send.</param>
    /// <param name="signalArgs">Arguments to pass to the signal handler.</param>
    /// <param name="executionTimeout">Optional workflow execution timeout.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when called from within a workflow (client-only) or workflow type cannot be determined.</exception>
    public static async Task SignalWithStartAsync<TWorkflow>(
        string[] uniqueKeys,
        object[] workflowArgs,
        string signalName,
        object[] signalArgs,
        TimeSpan? executionTimeout = null)
    {
        if (Workflow.InWorkflow)
        {
            throw new InvalidOperationException(
                "SignalWithStart is a client-only operation and cannot be called from within a workflow. " +
                "Use StartAsync to start a child workflow, or call SignalWithStart from an activity or outside workflow context.");
        }

        var workflowType = GetWorkflowTypeFromClass<TWorkflow>();
        await SignalWithStartViaClientAsync(workflowType, uniqueKeys, workflowArgs, signalName, signalArgs, executionTimeout);
    }

    /// <summary>
    /// Sends a signal to a workflow, starting it if it does not already exist (signal-with-start).
    /// Client-only operation; not supported when called from within a workflow.
    /// </summary>
    /// <param name="workflowType">The workflow type (format: "AgentName:WorkflowName").</param>
    /// <param name="uniqueKeys">Unique keys for workflow ID.</param>
    /// <param name="workflowArgs">Arguments to pass when starting the workflow (used only if workflow does not exist).</param>
    /// <param name="signalName">The name of the signal to send.</param>
    /// <param name="signalArgs">Arguments to pass to the signal handler.</param>
    /// <param name="executionTimeout">Optional workflow execution timeout.</param>
    public static async Task SignalWithStartAsync(
        string workflowType,
        string[] uniqueKeys,
        object[] workflowArgs,
        string signalName,
        object[] signalArgs,
        TimeSpan? executionTimeout = null)
    {
        if (Workflow.InWorkflow)
        {
            throw new InvalidOperationException(
                "SignalWithStart is a client-only operation and cannot be called from within a workflow. " +
                "Use StartAsync to start a child workflow, or call SignalWithStart from an activity or outside workflow context.");
        }

        await SignalWithStartViaClientAsync(workflowType, uniqueKeys, workflowArgs, signalName, signalArgs, executionTimeout);
    }

    /// <summary>
    /// Gets unique keys from context only (idPostfix). Callers cannot pass unique keys externally for signaling.
    /// </summary>
    private static string[] GetUniqueKeysFromContext()
    {
        var idPostfix = XiansContext.TryGetIdPostfix();
        return string.IsNullOrWhiteSpace(idPostfix) ? [] : [idPostfix];
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
    /// Propagates parent workflow metadata when available from context.
    /// </summary>
    private static async Task StartViaClientAsync(string workflowType, string[] uniqueKeys, TimeSpan? executionTimeout, object[] args)
    {
        var (client, tenantId, systemScoped, agentName) = await GetClientAndContextAsync(workflowType);

        // Build workflow ID (includes parent idPostfix + optional uniqueKey)
        var workflowId = BuildSubWorkflowId(agentName, workflowType, tenantId, uniqueKeys);
        var taskQueue = TenantContext.GetTaskQueueName(workflowType, systemScoped, tenantId);

        var searchAttributes = await BuildInheritedSearchAttributesAsync(tenantId, agentName, client);
        var options = new WorkflowOptions
        {
            Id = workflowId,
            TaskQueue = taskQueue,
            IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
            // Inherit parent workflow metadata (works in both workflow and activity contexts)
            Memo = BuildInheritedMemo(tenantId, agentName, systemScoped, searchAttributes),
            TypedSearchAttributes = searchAttributes
        };

        if (executionTimeout.HasValue)
        {
            options.ExecutionTimeout = executionTimeout.Value;
        }

        await client.StartWorkflowAsync(workflowType, args, options);

        _logger.LogDebug(
            "Started workflow via client: WorkflowId='{WorkflowId}', TaskQueue='{TaskQueue}'",
            workflowId,
            taskQueue);
    }

    /// <summary>
    /// Executes a workflow via the Temporal client (out-of-workflow scenario).
    /// Requires access to the agent to get the Temporal client.
    /// If called from an activity, propagates parent workflow metadata.
    /// </summary>
    private static async Task<TResult> ExecuteViaClientAsync<TResult>(string workflowType, string[] uniqueKeys, TimeSpan? executionTimeout, object[] args)
    {
        var (client, tenantId, systemScoped, agentName) = await GetClientAndContextAsync(workflowType);

        // Build workflow ID (includes parent idPostfix + optional uniqueKey)
        var workflowId = BuildSubWorkflowId(agentName, workflowType, tenantId, uniqueKeys);
        var taskQueue = TenantContext.GetTaskQueueName(workflowType, systemScoped, tenantId);

        var searchAttributes = await BuildInheritedSearchAttributesAsync(tenantId, agentName, client);
        var options = new WorkflowOptions
        {
            Id = workflowId,
            TaskQueue = taskQueue,
            IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
            // Inherit parent workflow metadata (works in both workflow and activity contexts)
            Memo = BuildInheritedMemo(tenantId, agentName, systemScoped, searchAttributes),
            TypedSearchAttributes = searchAttributes
        };

        if (executionTimeout.HasValue)
        {
            options.ExecutionTimeout = executionTimeout.Value;
        }

        var result = await client.ExecuteWorkflowAsync<TResult>(workflowType, args, options);

        _logger.LogDebug(
            "Executed workflow via client: WorkflowId='{WorkflowId}', TaskQueue='{TaskQueue}'",
            workflowId,
            taskQueue);

        return result;
    }

    /// <summary>
    /// Sends a signal to a workflow via the Temporal client (out-of-workflow scenario).
    /// </summary>
    private static async Task SignalViaClientAsync(string workflowType, string[] uniqueKeys, string signalName, object[] signalArgs)
    {
        var (client, tenantId, _, agentName) = await GetClientAndContextAsync(workflowType);
        var workflowId = BuildSubWorkflowId(agentName, workflowType, tenantId, uniqueKeys);
        var handle = client.GetWorkflowHandle(workflowId);
        await handle.SignalAsync(signalName, signalArgs);

        _logger.LogDebug(
            "Sent signal via client: WorkflowId='{WorkflowId}', SignalName='{SignalName}'",
            workflowId,
            signalName);
    }

    /// <summary>
    /// Signal-with-start via the Temporal client. Starts workflow if not exists, then signals.
    /// </summary>
    private static async Task SignalWithStartViaClientAsync(
        string workflowType,
        string[] uniqueKeys,
        object[] workflowArgs,
        string signalName,
        object[] signalArgs,
        TimeSpan? executionTimeout)
    {
        var (client, tenantId, systemScoped, agentName) = await GetClientAndContextAsync(workflowType);
        var workflowId = BuildSubWorkflowId(agentName, workflowType, tenantId, uniqueKeys);
        var taskQueue = TenantContext.GetTaskQueueName(workflowType, systemScoped, tenantId);

        var searchAttributes = await BuildInheritedSearchAttributesAsync(tenantId, agentName, client);
        var options = new WorkflowOptions
        {
            Id = workflowId,
            TaskQueue = taskQueue,
            IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
            Memo = BuildInheritedMemo(tenantId, agentName, systemScoped, searchAttributes),
            TypedSearchAttributes = searchAttributes
        };

        if (executionTimeout.HasValue)
        {
            options.ExecutionTimeout = executionTimeout.Value;
        }

        options.SignalWithStart(signalName, signalArgs);
        await client.StartWorkflowAsync(workflowType, workflowArgs, options);

        _logger.LogDebug(
            "SignalWithStart via client: WorkflowId='{WorkflowId}', SignalName='{SignalName}'",
            workflowId,
            signalName);
    }

    /// <summary>
    /// Builds the workflow ID for signalling when called from within a workflow.
    /// Uses only sync in-memory lookups - workflows must not perform I/O.
    /// </summary>
    private static string GetWorkflowIdForSignal(string workflowType, string[] uniqueKeys)
    {
        var agentName = workflowType.Contains(':')
            ? workflowType.Split(':')[0]
            : throw new InvalidOperationException(
                $"Invalid workflow type '{workflowType}'. Expected format: 'AgentName:WorkflowName'");

        var agent = XiansContext.GetAgent(agentName);

        string tenantId;
        if (agent.SystemScoped)
        {
            tenantId = XiansContext.SafeTenantId ?? agent.Options?.CertificateTenantId
                ?? throw new InvalidOperationException(
                    $"System-scoped agent '{agentName}' requires workflow context or CertificateTenantId for signalling.");
        }
        else
        {
            if (agent.Options == null || string.IsNullOrWhiteSpace(agent.Options.CertificateTenantId))
            {
                throw new InvalidOperationException(
                    $"Agent '{agentName}' is not system-scoped but CertificateTenantId is missing.");
            }
            tenantId = agent.Options.CertificateTenantId;
        }

        return BuildSubWorkflowId(agentName, workflowType, tenantId, uniqueKeys);
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

        // Get tenant ID from agent options (non-system-scoped) or from workflow context (system-scoped)
        string tenantId;
        if (agent.SystemScoped)
        {
            // Try context first; when outside workflow/activity, fall back to certificate tenant if available
            tenantId = XiansContext.SafeTenantId ?? agent.Options?.CertificateTenantId
                ?? throw new InvalidOperationException(
                    $"System-scoped agent '{agentName}' requires workflow/activity context or CertificateTenantId when starting workflows from outside Temporal context.");
        }
        else
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
            tenantId = agent.Options.CertificateTenantId;
        }

        return (client, tenantId, agent.SystemScoped, agentName);
    }

    /// <summary>
    /// Builds a workflow ID with support for multiple suffix parts, without relying on XiansContext.CurrentAgent.
    /// Mirrors TenantContext.BuildWorkflowId() but takes agentName as parameter for use outside workflow context.
    /// Includes parent's idPostfix when available (from workflow/activity context) plus optional uniqueKey.
    /// Format: {tenantId}:{agentName}:{workflowName}[:{idPostfix}][:{uniqueKey}]
    /// </summary>
    internal static string BuildSubWorkflowId(string agentName, string workflowType, string tenantId, string[] uniqueKeys)
    {
        // Extract workflow name from workflow type (format: "AgentName:WorkflowName")
        var workflowName = workflowType.Contains(':') 
            ? workflowType.Split(':')[1] 
            : workflowType;

        // Build base workflow ID
        var workflowId = $"{tenantId}:{agentName}:{workflowName}";
        
        if (uniqueKeys.Length > 0)
        {
            workflowId += $":{string.Join(":", uniqueKeys)}";
        }

        return workflowId;
    }

    /// <summary>
    /// Builds memo for child/sub-workflow by inheriting all parent memo entries when in workflow context.
    /// Works both in workflow context (inherits all) and outside workflow context (builds minimal).
    /// When searchAttributes is provided (e.g. from client-based start in activity), userId is extracted for memo consistency.
    /// </summary>
    /// <param name="tenantId">Tenant ID for the child workflow.</param>
    /// <param name="agentName">Agent name for the child workflow.</param>
    /// <param name="systemScoped">Whether the workflow is system-scoped.</param>
    /// <param name="searchAttributes">Optional search attributes; when provided, userId is extracted for the memo.</param>
    internal static Dictionary<string, object> BuildInheritedMemo(
        string tenantId,
        string agentName,
        bool systemScoped,
        Temporalio.Common.SearchAttributeCollection? searchAttributes = null)
    {
        var memo = new Dictionary<string, object>();

        // If in workflow context, inherit all parent memo entries
        if (Workflow.InWorkflow)
        {
            foreach (var kvp in Workflow.Memo)
            {
                try
                {
                    // Get the JSON-encoded value from the payload
                    var jsonStr = kvp.Value.Payload.Data.ToStringUtf8();
                    
                    // Deserialize the JSON to get the actual value (removes extra quotes)
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonStr);
                    
                    // Store the appropriate type based on JSON value kind
                    memo[kvp.Key] = jsonElement.ValueKind switch
                    {
                        JsonValueKind.String => jsonElement.GetString()!,
                        JsonValueKind.Number => jsonElement.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => null!,
                        _ => jsonStr // For complex types, use raw JSON string
                    };
                }
                catch
                {
                    // Skip entries that cannot be deserialized
                }
            }
        }

        // Override/set required system metadata for the child workflow
        memo[WorkflowConstants.Keys.TenantId] = tenantId;
        memo[WorkflowConstants.Keys.Agent] = agentName;
        memo[WorkflowConstants.Keys.SystemScoped] = systemScoped;
        
        var idPostfix = XiansContext.TryGetIdPostfix();
        if (idPostfix != null)
        {
            memo[WorkflowConstants.Keys.idPostfix] = idPostfix;
        }

        // Include userId for consistency with search attributes (Temporal memo cannot have null values)
        var userId = WorkflowMetadataResolver.GetValueFromSearchAttributes(searchAttributes, WorkflowConstants.Keys.UserId)
            ?? XiansContext.TryGetParticipantId()
            ?? string.Empty;
        memo[WorkflowConstants.Keys.UserId] = userId;

        return memo;
    }

    /// <summary>
    /// Builds search attributes for child/sub-workflow by inheriting parent attributes when in workflow context.
    /// Works both in workflow context (inherits all) and outside workflow context (builds minimal).
    /// This is a shared method used by both SubWorkflowOptions and client-based workflow starting.
    /// </summary>
    internal static Temporalio.Common.SearchAttributeCollection? BuildInheritedSearchAttributes(string tenantId, string agentName)
    {
        if (Workflow.InWorkflow)
            return Workflow.TypedSearchAttributes;

        var idPostfix = XiansContext.TryGetIdPostfix() ?? string.Empty;
        var participantId = XiansContext.TryGetParticipantId() ?? string.Empty;
        return WorkflowMetadataResolver.BuildSearchAttributes(tenantId, agentName, participantId, idPostfix);
    }

    /// <summary>
    /// Async version that fetches parent workflow's search attributes when in activity context.
    /// Delegates to <see cref="WorkflowMetadataResolver.ResolveSearchAttributesForChildAsync"/> for activity-context
    /// resolution; falls back to sync <see cref="BuildInheritedSearchAttributes"/> when outside workflow/activity.
    /// </summary>
    internal static async Task<Temporalio.Common.SearchAttributeCollection?> BuildInheritedSearchAttributesAsync(
        string tenantId,
        string agentName,
        ITemporalClient client)
    {
        var fromResolver = await WorkflowMetadataResolver.ResolveSearchAttributesForChildAsync(tenantId, agentName, client);
        if (fromResolver != null)
            return fromResolver;

        return BuildInheritedSearchAttributes(tenantId, agentName);
    }
}

