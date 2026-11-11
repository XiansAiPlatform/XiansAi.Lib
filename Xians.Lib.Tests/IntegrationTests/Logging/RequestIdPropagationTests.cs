using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Testing;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common;
using Xians.Lib.Temporal;

namespace Xians.Lib.Tests.IntegrationTests.Logging;

/// <summary>
/// Runs a real workflow and activity on Temporal's local test server: a workflow started with a
/// request ID in its memo sees it, and so does the activity it schedules (through the header the
/// interceptor adds) — so both write it on their logs.
/// </summary>
[Trait("Category", "Integration")]
public class RequestIdPropagationTests
{
    [Workflow]
    public class EchoRequestIdWorkflow
    {
        [WorkflowRun]
        public async Task<string[]> RunAsync()
        {
            var inWorkflow = XiansContext.SafeRequestId ?? "<none>";
            var inActivity = await Workflow.ExecuteActivityAsync(
                () => EchoActivities.RequestIdInActivity(),
                new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(30) });
            return [inWorkflow, inActivity];
        }
    }

    public static class EchoActivities
    {
        [Activity]
        public static string RequestIdInActivity() => XiansContext.SafeRequestId ?? "<none>";
    }

    [Fact]
    public async Task TheRequestIdReachesTheWorkflowAndItsActivities()
    {
        var result = await RunAsync(memo: new Dictionary<string, object> { [WorkflowConstants.Keys.RequestId] = "req-e2e" });

        Assert.Equal(["req-e2e", "req-e2e"], result);
    }

    [Fact]
    public async Task AWorkflowWithoutARequestIdTagsNothing()
    {
        var result = await RunAsync(memo: null);

        Assert.Equal(["<none>", "<none>"], result);
    }

    private static async Task<string[]> RunAsync(Dictionary<string, object>? memo)
    {
        await using var env = await WorkflowEnvironment.StartLocalAsync();
        var taskQueue = $"request-id-{Guid.NewGuid():N}";

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions(taskQueue)
            {
                Interceptors = [new RequestIdInterceptor()],
            }
            .AddWorkflow<EchoRequestIdWorkflow>()
            .AddActivity(EchoActivities.RequestIdInActivity));

        return await worker.ExecuteAsync(() => env.Client.ExecuteWorkflowAsync(
            (EchoRequestIdWorkflow wf) => wf.RunAsync(),
            new WorkflowOptions($"wf-{Guid.NewGuid():N}", taskQueue) { Memo = memo }));
    }
}
