using Temporalio.Client;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common;
using Xians.Lib.Common.MultiTenancy;
using Xians.Lib.Workflows.Messaging.Models;

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
    public static async Task<WorkflowHandle> StartOrGetWorkflowAsync(
        ITemporalClient client,
        string agentName,
        string workflowName,
        bool systemScoped = false,
        string? tenantId = null)
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
                IdConflictPolicy = Temporalio.Api.Enums.V1.WorkflowIdConflictPolicy.UseExisting
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
            await handle.TerminateAsync(reason);
        }
        catch
        {
            // Ignore - workflow may not exist or already terminated
        }
    }

    /// <summary>
    /// Terminates multiple workflows by their built-in names.
    /// </summary>
    public static async Task TerminateBuiltInWorkflowsAsync(
        ITemporalClient client,
        string agentName,
        IEnumerable<string> workflowNames,
        string? tenantId = null)
    {
        foreach (var name in workflowNames)
        {
            var workflowId = BuildWorkflowId(agentName, name, tenantId);
            await TerminateWorkflowIfRunningAsync(client, workflowId, "Test cleanup");
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

