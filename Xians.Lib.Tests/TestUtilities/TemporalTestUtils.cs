using Temporalio.Client;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common;
using Xians.Lib.Common.MultiTenancy;
using Xians.Lib.Temporal.Workflows.Messaging.Models;

namespace Xians.Lib.Tests.TestUtilities;

/// <summary>
/// Utility class for common Temporal operations in tests.
/// </summary>
public static class TemporalTestUtils
{
    /// <summary>
    /// Default tenant ID for tests.
    /// </summary>
    public const string DefaultTestTenantId = "test";

    /// <summary>
    /// Default workflow execution timeout for tests (5 minutes).
    /// This ensures test workflows don't run forever if cleanup fails.
    /// </summary>
    public static readonly TimeSpan DefaultWorkflowExecutionTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Builds a workflow ID for a built-in workflow.
    /// </summary>
    public static string BuildWorkflowId(string agentName, string workflowName, string? tenantId = null)
    {
        var workflowType = WorkflowIdentity.BuildBuiltInWorkflowType(agentName, workflowName);
        return $"{tenantId ?? DefaultTestTenantId}:{workflowType}";
    }

    /// <summary>
    /// Gets the task queue name for a built-in workflow.
    /// </summary>
    public static string GetTaskQueue(string agentName, string workflowName, bool systemScoped = false, string? tenantId = null)
    {
        var workflowType = WorkflowIdentity.BuildBuiltInWorkflowType(agentName, workflowName);
        return TenantContext.GetTaskQueueName(workflowType, systemScoped, tenantId ?? DefaultTestTenantId);
    }

    /// <summary>
    /// Starts or gets a handle to a built-in workflow.
    /// </summary>
    /// <param name="client">The Temporal client.</param>
    /// <param name="agentName">The agent name.</param>
    /// <param name="workflowName">The workflow name.</param>
    /// <param name="systemScoped">Whether the workflow is system-scoped.</param>
    /// <param name="tenantId">Optional tenant ID (defaults to test tenant).</param>
    /// <param name="executionTimeout">Optional execution timeout (defaults to 5 minutes to prevent runaway workflows).</param>
    public static async Task<WorkflowHandle> StartOrGetWorkflowAsync(
        ITemporalClient client,
        string agentName,
        string workflowName,
        bool systemScoped = false,
        string? tenantId = null,
        TimeSpan? executionTimeout = null)
    {
        var workflowType = WorkflowIdentity.BuildBuiltInWorkflowType(agentName, workflowName);
        var workflowId = BuildWorkflowId(agentName, workflowName, tenantId);
        var taskQueue = GetTaskQueue(agentName, workflowName, systemScoped, tenantId);

        return await client.StartWorkflowAsync(
            workflowType,
            Array.Empty<object>(),
            new WorkflowOptions
            {
                Id = workflowId,
                TaskQueue = taskQueue,
                IdConflictPolicy = Temporalio.Api.Enums.V1.WorkflowIdConflictPolicy.UseExisting,
                ExecutionTimeout = executionTimeout ?? DefaultWorkflowExecutionTimeout
            });
    }

    /// <summary>
    /// Creates an inbound chat message for testing.
    /// </summary>
    public static InboundMessage CreateChatMessage(
        string agentName,
        string text,
        string? threadId = null,
        string? participantId = null)
    {
        var testId = threadId ?? $"test-{Guid.NewGuid():N}";
        return new InboundMessage
        {
            Payload = new InboundMessagePayload
            {
                Agent = agentName,
                ThreadId = testId,
                ParticipantId = participantId ?? "test-user",
                Text = text,
                RequestId = $"req-{Guid.NewGuid():N}",
                Hint = "",
                Scope = "",
                Data = new { },
                Type = "chat"
            },
            SourceAgent = "TestRunner",
            SourceWorkflowId = "test-workflow",
            SourceWorkflowType = "test:runner"
        };
    }

    /// <summary>
    /// Creates an inbound data message for testing.
    /// </summary>
    public static InboundMessage CreateDataMessage(
        string agentName,
        object data,
        string? text = null,
        string? threadId = null,
        string? participantId = null)
    {
        var testId = threadId ?? $"test-{Guid.NewGuid():N}";
        return new InboundMessage
        {
            Payload = new InboundMessagePayload
            {
                Agent = agentName,
                ThreadId = testId,
                ParticipantId = participantId ?? "test-user",
                Text = text ?? "",
                RequestId = $"req-{Guid.NewGuid():N}",
                Hint = "",
                Scope = "",
                Data = data,
                Type = "data"
            },
            SourceAgent = "TestRunner",
            SourceWorkflowId = "test-workflow",
            SourceWorkflowType = "test:runner"
        };
    }

    /// <summary>
    /// Sends a signal to a workflow and waits for processing.
    /// </summary>
    public static async Task SendSignalAsync(
        WorkflowHandle handle,
        InboundMessage message,
        string signalName = "HandleInboundChatOrData")
    {
        await handle.SignalAsync(signalName, new object[] { message });
    }

    /// <summary>
    /// Terminates a workflow if it's running.
    /// </summary>
    public static async Task TerminateWorkflowIfRunningAsync(
        ITemporalClient client,
        string workflowId,
        string reason = "Test cleanup")
    {
        try
        {
            var handle = client.GetWorkflowHandle(workflowId);
            var description = await handle.DescribeAsync();
            
            // Only terminate if workflow is actually running
            if (description.Status == Temporalio.Api.Enums.V1.WorkflowExecutionStatus.Running)
            {
                await handle.TerminateAsync(reason);
                Console.WriteLine($"  ✓ Terminated workflow: {workflowId}");
            }
            else
            {
                Console.WriteLine($"  ℹ Workflow already completed: {workflowId} (status: {description.Status})");
            }
        }
        catch (Temporalio.Exceptions.RpcException ex) when (ex.Code == Temporalio.Exceptions.RpcException.StatusCode.NotFound)
        {
            // Workflow doesn't exist - that's fine
            Console.WriteLine($"  ℹ Workflow not found: {workflowId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Error terminating workflow {workflowId}: {ex.Message}");
        }
    }

    /// <summary>
    /// Terminates workflows matching a pattern (useful for cleaning up test-created workflows).
    /// </summary>
    /// <param name="client">The Temporal client.</param>
    /// <param name="workflowIdPattern">Pattern to match workflow IDs (e.g., "a2a-custom-target-*").</param>
    /// <param name="reason">Termination reason.</param>
    public static void TerminateWorkflowsByPatternAsync(
        ITemporalClient client,
        string workflowIdPattern,
        string reason = "Test cleanup")
    {
        try
        {
            // Note: This is a basic implementation. For better cleanup, consider using
            // Temporal's list workflows API with a query filter.
            Console.WriteLine($"  ℹ Pattern-based termination not fully implemented. Use explicit workflow IDs.");
            Console.WriteLine($"  ℹ Pattern: {workflowIdPattern}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Error in pattern termination: {ex.Message}");
        }
    }

    /// <summary>
    /// Terminates multiple workflows by their built-in names.
    /// This method terminates the main workflow and attempts to find and terminate
    /// any schedule-triggered workflows using workflow ID patterns.
    /// </summary>
    public static async Task TerminateBuiltInWorkflowsAsync(
        ITemporalClient client,
        string agentName,
        IEnumerable<string> workflowNames,
        string? tenantId = null)
    {
        foreach (var name in workflowNames)
        {
            try
            {
                var workflowType = WorkflowIdentity.BuildBuiltInWorkflowType(agentName, name);
                var tenant = tenantId ?? DefaultTestTenantId;
                
                // Terminate the main workflow
                var workflowId = BuildWorkflowId(agentName, name, tenantId);
                await TerminateWorkflowIfRunningAsync(client, workflowId, "Test cleanup");
                
                // For workflows with schedules, also try to terminate schedule-triggered workflows
                // These have IDs like: test:AgentName:WorkflowType:scheduleId-timestamp
                // We can't list them easily, so we rely on schedule deletion and a delay
                
                Console.WriteLine($"  ℹ Waiting for any schedule-triggered workflows to complete...");
                await Task.Delay(2000); // Give time for schedule-triggered workflows to finish
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ Failed to terminate workflows for '{name}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Terminates multiple custom workflows by their explicit workflow IDs.
    /// Use this to clean up custom workflows created in tests.
    /// </summary>
    /// <param name="client">The Temporal client.</param>
    /// <param name="workflowIds">The workflow IDs to terminate.</param>
    /// <param name="reason">Termination reason.</param>
    public static async Task TerminateCustomWorkflowsAsync(
        ITemporalClient client,
        IEnumerable<string> workflowIds,
        string reason = "Test cleanup")
    {
        foreach (var workflowId in workflowIds)
        {
            await TerminateWorkflowIfRunningAsync(client, workflowId, reason);
        }
    }

    /// <summary>
    /// Polls for a result in a dictionary with timeout.
    /// </summary>
    public static async Task<T?> WaitForResultAsync<T>(
        Func<T?> getResult,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null) where T : class
    {
        var timeoutValue = timeout ?? TimeSpan.FromSeconds(30);
        var intervalValue = pollInterval ?? TimeSpan.FromSeconds(1);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeoutValue)
        {
            var result = getResult();
            if (result != null)
            {
                return result;
            }
            await Task.Delay(intervalValue);
        }

        return null;
    }
}

