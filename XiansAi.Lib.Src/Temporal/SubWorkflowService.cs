using Temporalio.Workflows;
using XiansAi.Logging;

namespace Temporal;

public class SubWorkflowService
{

    private static readonly Logger<SubWorkflowService> _logger = Logger<SubWorkflowService>.For();
    public static async Task Start<TWorkflow>(string namePostfix, object[] args) {
        var workflowType = AgentContext.GetWorkflowTypeFor(typeof(TWorkflow));
        if (Workflow.InWorkflow) {
            _logger.LogInformation($"Starting sub workflow `{workflowType}` in workflow `{AgentContext.WorkflowId}`");
            var options = new SubWorkflowOptions(namePostfix, workflowType);
            await Workflow.StartChildWorkflowAsync(workflowType, args, options);
        } else {
            await new WorkflowClientService().StartWorkflow(workflowType, args, namePostfix);
        }
    }

    public static async Task<TResult> Execute<TWorkflow, TResult>(string namePostfix, object[] args) {
        var workflowType = AgentContext.GetWorkflowTypeFor(typeof(TWorkflow));
        
        if (Workflow.InWorkflow) {
            _logger.LogInformation($"Executing sub workflow `{workflowType}` in workflow `{AgentContext.WorkflowId}`");
            var options = new SubWorkflowOptions(namePostfix, workflowType);
            return await Workflow.ExecuteChildWorkflowAsync<TResult>(workflowType, args, options);
        } else {
            return await new WorkflowClientService().ExecuteWorkflow<TResult>(workflowType, args, namePostfix);
        }
    }
}