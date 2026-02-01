using Microsoft.Extensions.Logging;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
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
            _logger.LogInformation(
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
            _logger.LogInformation(
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
            _logger.LogInformation(
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
            _logger.LogInformation(
                "Executing workflow '{WorkflowType}' via client (not in workflow context)",
                workflowType);

            return await ExecuteViaClientAsync<TResult>(workflowType, uniqueKeys, executionTimeout, args);
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
    /// Propagates parent workflow metadata when available from context.
    /// </summary>
    private static async Task StartViaClientAsync(string workflowType, string[] uniqueKeys, TimeSpan? executionTimeout, object[] args)
    {
        var (client, tenantId, systemScoped, agentName) = await GetClientAndContextAsync(workflowType);

        // Build workflow ID (includes parent idPostfix + optional uniqueKey)
        var workflowId = BuildSubWorkflowId(agentName, workflowType, tenantId, uniqueKeys);
        var taskQueue = TenantContext.GetTaskQueueName(workflowType, systemScoped, tenantId);

        var options = new WorkflowOptions
        {
            Id = workflowId,
            TaskQueue = taskQueue,
            IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
            // Inherit parent workflow metadata (works in both workflow and activity contexts)
            Memo = BuildInheritedMemo(tenantId, agentName, systemScoped),
            TypedSearchAttributes = BuildInheritedSearchAttributes(tenantId, agentName)
        };

        if (executionTimeout.HasValue)
        {
            options.ExecutionTimeout = executionTimeout.Value;
        }

        await client.StartWorkflowAsync(workflowType, args, options);

        _logger.LogInformation(
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

        var options = new WorkflowOptions
        {
            Id = workflowId,
            TaskQueue = taskQueue,
            IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
            // Inherit parent workflow metadata (works in both workflow and activity contexts)
            Memo = BuildInheritedMemo(tenantId, agentName, systemScoped),
            TypedSearchAttributes = BuildInheritedSearchAttributes(tenantId, agentName)
        };

        if (executionTimeout.HasValue)
        {
            options.ExecutionTimeout = executionTimeout.Value;
        }

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
    /// This is a shared method used by both SubWorkflowOptions and client-based workflow starting.
    /// </summary>
    internal static Dictionary<string, object> BuildInheritedMemo(string tenantId, string agentName, bool systemScoped)
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
        // These values are specific to the child and should not be inherited
        memo[Common.WorkflowConstants.Keys.TenantId] = tenantId;
        memo[Common.WorkflowConstants.Keys.Agent] = agentName;
        memo[Common.WorkflowConstants.Keys.SystemScoped] = systemScoped;
        
        var idPostfix = XiansContext.TryGetIdPostfix();
        if (idPostfix != null)
        {
            memo[Common.WorkflowConstants.Keys.idPostfix] = idPostfix;
        }

        return memo;
    }

    /// <summary>
    /// Builds search attributes for child/sub-workflow by inheriting parent attributes when in workflow context.
    /// Works both in workflow context (inherits all) and outside workflow context (builds minimal).
    /// This is a shared method used by both SubWorkflowOptions and client-based workflow starting.
    /// </summary>
    internal static Temporalio.Common.SearchAttributeCollection? BuildInheritedSearchAttributes(string tenantId, string agentName)
    {
        // If in workflow context, directly inherit parent's search attributes
        if (Workflow.InWorkflow)
        {
            // Directly inherit parent's search attributes (same approach as ScheduleBuilder)
            // This preserves all custom search attributes from parent to child
            return Workflow.TypedSearchAttributes;
        }

        // Not in workflow context - build minimal search attributes with available information
        var builder = new Temporalio.Common.SearchAttributeCollection.Builder()
            .Set(Temporalio.Common.SearchAttributeKey.CreateKeyword(Common.WorkflowConstants.Keys.TenantId), tenantId)
            .Set(Temporalio.Common.SearchAttributeKey.CreateKeyword(Common.WorkflowConstants.Keys.Agent), agentName);
        
        var idPostfix = XiansContext.TryGetIdPostfix();
        if (idPostfix != null)
        {
            builder.Set(Temporalio.Common.SearchAttributeKey.CreateKeyword(Common.WorkflowConstants.Keys.idPostfix), idPostfix);
        }
        
        return builder.ToSearchAttributeCollection();
    }
}

