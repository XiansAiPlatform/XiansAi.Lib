using Temporalio.Workflows;

namespace Flowmaxer.Common
{
    public interface IWorkflow<TInput, TOutput>
    {
        [WorkflowRun]
        Task<TOutput> RunAsync(TInput input);
    }
}