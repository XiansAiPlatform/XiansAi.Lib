using Temporalio.Client;
using XiansAi.Logging;

namespace Temporal;

public class WorkflowClientService
{
    private readonly Logger<WorkflowClientService> _logger;
    private readonly ITemporalClient _client;
    public WorkflowClientService()
    {
        _logger = Logger<WorkflowClientService>.For();
        _client = TemporalClientService.Instance.GetClientAsync().Result;
    }


    public async Task<TResult> ExecuteWorkflow<TResult>(string workflowType, object[] args, string? postfix = null)
    {
        var options = new NewWorkflowOptions(workflowType, postfix);
        _logger.LogInformation($"Executing workflow using Temporal Client `{workflowType}` with id `{options.Id}`");
        return await _client.ExecuteWorkflowAsync<TResult>(
            workflowType,
            args,
            options
        );
    }

    public async Task StartWorkflow(string workflowType, object[] args, string? postfix = null)
    {
        var options = new NewWorkflowOptions(workflowType, postfix);
        _logger.LogInformation($"Starting workflow using Temporal Client `{workflowType}` with id postfix `{postfix}`");
        await _client.StartWorkflowAsync(
            workflowType,
            args,
            options
        );
    }
}