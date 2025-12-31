using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Xians.Agent.Sample;

[Workflow(Constants.AgentName + ":Content Processing Workflow")]
public class ContentProcessingWorkflow
{
    private bool _approved = false;
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        Workflow.Logger.LogInformation("Awaiting approval");

        await Workflow.WaitConditionAsync(() => _approved);

        Workflow.Logger.LogInformation("Approved");

        return "Approved";
    }

    [WorkflowSignal]
    public Task UserApproved()
    {
        _approved = true;
        Workflow.Logger.LogInformation("Approved");
        return Task.CompletedTask;
    }
}

