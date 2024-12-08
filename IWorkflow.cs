using Temporalio.Workflows;

namespace AgentFlow.Common
{
    public interface IWorkflow
    {
        [WorkflowRun]
        Task RunAsync();
    }
}