using Microsoft.Extensions.Logging;
using Temporalio.Client;

namespace Temporal;

public class WorkflowClientService
{
    private readonly ILogger<WorkflowClientService> _logger;
    private readonly ITemporalClient _client;
    private readonly string _agentName;
    public WorkflowClientService(string agentName)
    {
        _logger = Globals.LogFactory.CreateLogger<WorkflowClientService>();
        _client = TemporalClientService.Instance.GetClientAsync().Result;
        _agentName = agentName;
    }


    public async Task<TResult> ExecuteWorkflow<TResult>(string workflowType, object[] args, string? postfix = null)
    {
        _logger.LogInformation($"Executing workflow `{workflowType}` with id postfix `{postfix}` for agent `{_agentName}`");
        var options = new NewWorkflowOptions(workflowType, postfix, _agentName);
        return await _client.ExecuteWorkflowAsync<TResult>(
            workflowType,
            args,
            options
        );
    }

    public async Task StartWorkflow(string workflowType, object[] args, string? postfix = null)
    {
        _logger.LogInformation($"Starting workflow `{workflowType}` with id postfix `{postfix}` for agent `{_agentName}`");
        var options = new NewWorkflowOptions(workflowType, postfix, _agentName);
        await _client.StartWorkflowAsync(
            workflowType,
            args,
            options
        );
    }
}