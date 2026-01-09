using System.Text.Json;
using Microsoft.Extensions.Logging;
using Temporalio.Common;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Temporal.Workflows.Messaging;
using Xians.Lib.Temporal.Workflows.Messaging.Models;
using Xians.Lib.Temporal.Workflows.A2A;

namespace Xians.Lib.Agents.A2A;

/// <summary>
/// Activity executor for A2A operations.
/// Handles context-aware execution of A2A message processing.
/// Eliminates manual branching between workflow and activity contexts.
/// </summary>
internal class A2AActivityExecutor : ContextAwareActivityExecutor<MessageActivities, A2AService>
{
    private readonly XiansWorkflow _targetWorkflow;

    public A2AActivityExecutor(XiansWorkflow targetWorkflow, ILogger logger)
        : base(logger)
    {
        _targetWorkflow = targetWorkflow ?? throw new ArgumentNullException(nameof(targetWorkflow));
    }

    protected override A2AService CreateService()
    {
        return new A2AService(_targetWorkflow);
    }

    protected override ActivityOptions GetDefaultActivityOptions()
    {
        // Use the standard message activity options for A2A
        return MessageActivityOptions.GetStandardOptions();
    }

    /// <summary>
    /// Processes an A2A message using context-aware execution.
    /// In workflow context: executes as activity.
    /// In activity context: calls service directly.
    /// </summary>
    public async Task<A2AActivityResponse> ProcessA2AMessageAsync(ProcessMessageActivityRequest request)
    {
        return await ExecuteAsync(
            act => act.ProcessA2AMessageAsync(request),
            svc => svc.ProcessDirectAsync(request),
            operationName: "ProcessA2AMessage");
    }
}

/// <summary>
/// Activity executor for A2A signal/query/update operations to custom workflows.
/// Handles context-aware execution following the same pattern as A2AActivityExecutor.
/// </summary>
internal class A2ASignalQueryExecutor : ContextAwareActivityExecutor<A2ASignalQueryActivities, A2ASignalQueryService>
{
    public A2ASignalQueryExecutor(ILogger logger)
        : base(logger)
    {
    }

    protected override A2ASignalQueryService CreateService()
    {
        return new A2ASignalQueryService();
    }

    protected override ActivityOptions GetDefaultActivityOptions()
    {
        return new ActivityOptions
        {
            StartToCloseTimeout = TimeSpan.FromMinutes(5),
            RetryPolicy = new RetryPolicy
            {
                InitialInterval = TimeSpan.FromSeconds(1),
                MaximumInterval = TimeSpan.FromSeconds(10),
                BackoffCoefficient = 2.0f,
                MaximumAttempts = 3
            }
        };
    }

    /// <summary>
    /// Sends a signal to a workflow.
    /// In workflow context: executes as activity.
    /// In activity context: calls service directly.
    /// </summary>
    public async Task ExecuteSignalAsync(string workflowId, string signalName, object[] args)
    {
        await ExecuteAsync(
            act => act.SendSignalAsync(new A2ASignalRequest
            {
                WorkflowId = workflowId,
                SignalName = signalName,
                Args = args
            }),
            svc => svc.SendSignalAsync(workflowId, signalName, args),
            operationName: "SendSignalAsync");
    }

    /// <summary>
    /// Queries a workflow and returns the result.
    /// In workflow context: executes as activity (returns object, needs deserialization).
    /// In activity context: calls service directly (returns TResult).
    /// Uses Temporal SDK's string-based API.
    /// </summary>
    public async Task<TResult> ExecuteQueryAsync<TResult>(string workflowId, string queryName, object[] args)
    {
        var request = new A2AQueryRequest
        {
            WorkflowId = workflowId,
            QueryName = queryName,
            Args = args
        };
        
        // Note: activity returns object?, service returns TResult
        // We need to handle both cases
        if (Workflow.InWorkflow)
        {
            var result = await ExecuteAsync(
                act => act.QueryWorkflowAsync(request),
                svc => Task.FromResult<object?>(null), // Never called in workflow context
                operationName: "QueryAsync");
            return DeserializeResult<TResult>(result);
        }
        
        var service = CreateService();
        return await service.QueryAsync<TResult>(workflowId, queryName, args);
    }

    /// <summary>
    /// Executes an update to a workflow.
    /// In workflow context: executes as activity (returns object, needs deserialization).
    /// In activity context: calls service directly (returns TResult).
    /// Uses Temporal SDK's string-based API.
    /// </summary>
    public async Task<TResult> ExecuteUpdateAsync<TResult>(string workflowId, string updateName, object[] args)
    {
        var request = new A2AUpdateRequest
        {
            WorkflowId = workflowId,
            UpdateName = updateName,
            Args = args
        };
        
        // Note: activity returns object?, service returns TResult
        // We need to handle both cases
        if (Workflow.InWorkflow)
        {
            var result = await ExecuteAsync(
                act => act.ExecuteUpdateWorkflowAsync(request),
                svc => Task.FromResult<object?>(null), // Never called in workflow context
                operationName: "ExecuteUpdateAsync");
            return DeserializeResult<TResult>(result);
        }
        
        var service = CreateService();
        return await service.ExecuteUpdateAsync<TResult>(workflowId, updateName, args);
    }

    /// <summary>
    /// Deserializes a result that may be a JsonElement into the target type.
    /// </summary>
    private static TResult DeserializeResult<TResult>(object? result)
    {
        if (result == null)
        {
            return default!;
        }
        
        // If result is already the target type, return it
        if (result is TResult typedResult)
        {
            return typedResult;
        }
        
        // If result is JsonElement, deserialize it
        if (result is JsonElement jsonElement)
        {
            return JsonSerializer.Deserialize<TResult>(jsonElement.GetRawText())!;
        }
        
        // Try to cast or throw
        return (TResult)result;
    }
}

