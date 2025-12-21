using Temporalio.Workflows;

[Workflow ("Default Workflow")]
public class DefaultWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        return "Hello, World!";
    }
}