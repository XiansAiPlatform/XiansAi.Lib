using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

[Workflow("Custom Approval Workflow")]
public class CustomWorkflow
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
    public void UserApproved()
    {
        _approved = true;
        Workflow.Logger.LogInformation("Approved");
    }
}