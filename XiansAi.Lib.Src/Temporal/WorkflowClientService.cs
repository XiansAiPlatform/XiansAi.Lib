using Microsoft.Extensions.Logging;
using Temporalio.Client;
using System.Diagnostics;
using XiansAi.Telemetry;

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
        using var activity = OpenTelemetryExtensions.StartTemporalOperation(
            "Temporal.ExecuteWorkflow",
            new Dictionary<string, object>
            {
                ["temporal.operation_type"] = "execute_workflow",
                ["temporal.workflow_type"] = workflowType,
                ["temporal.agent_name"] = _agentName
            });
        
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
        using var activity = OpenTelemetryExtensions.StartTemporalOperation(
            "Temporal.StartWorkflow",
            new Dictionary<string, object>
            {
                ["temporal.operation_type"] = "start_workflow",
                ["temporal.workflow_type"] = workflowType,
                ["temporal.agent_name"] = _agentName
            });
        
        _logger.LogInformation($"Starting workflow `{workflowType}` with id postfix `{postfix}` for agent `{_agentName}`");
        var options = new NewWorkflowOptions(workflowType, postfix, _agentName);
        await _client.StartWorkflowAsync(
            workflowType,
            args,
            options
        );
    }
}