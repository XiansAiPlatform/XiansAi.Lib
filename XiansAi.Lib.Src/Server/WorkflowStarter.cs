using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using XiansAi.Models;
using Server.Http;
using System.Net.Http.Json;

namespace Server;

public class WorkflowStarter
{
    private readonly ILogger _logger;

    private readonly ISecureApiClient _secureApi;

    public WorkflowStarter(ILoggerFactory loggerFactory,
        ISecureApiClient secureApi)
    {
        _logger = loggerFactory.CreateLogger<FlowDefinitionUploader>() ?? 
            throw new ArgumentNullException(nameof(loggerFactory));
         _secureApi = secureApi ?? 
            throw new ArgumentNullException(nameof(secureApi));
    }

    public async Task StartWorkflow(WorkflowRequest workflowDetails)
    {
        _logger.LogInformation("Starting workflow: {workflow}", workflowDetails);
        if (SecureApi.Instance.IsReady)
        {
            var client = SecureApi.Instance.Client;

            var response = await client.PostAsync("api/agent/start-workflow", JsonContent.Create(workflowDetails));
            _logger.LogInformation("Workflow start response: {response}", response);
            response.EnsureSuccessStatusCode();
        }
        else
        {
            _logger.LogWarning("App server secure API is not ready, skipping workflow start");
        }
    }
}