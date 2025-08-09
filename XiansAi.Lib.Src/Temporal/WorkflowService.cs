using Temporalio.Client;
using XiansAi.Logging;

namespace Temporal;

public class WorkflowService
{
    private readonly Logger<WorkflowService> _logger;
    private readonly ITemporalClient _client;
    public WorkflowService()
    {
        _logger = Logger<WorkflowService>.For();
        _client = TemporalClientService.Instance.GetClientAsync().Result;
    }

    public async Task StartWorkflow(string workflowType, string? workflowId = null)
    {
        var options = new NewWorkflowOptions(workflowType, workflowId);
        await _client.StartWorkflowAsync(
            workflowType,
            Array.Empty<string>(),
            options
        );
    }
}