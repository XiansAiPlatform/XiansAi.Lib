using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XiansAi.Models;
using XiansAi.Flow;
using Server;
using Server.Http;

namespace XiansAi.Capability
{
    public class CapabilityBase
    {
        protected readonly ILogger _logger;

        public CapabilityBase()
        {
            // Create a logger using the global logger factory.
            _logger = Globals.LogFactory.CreateLogger(this.GetType());
        }

        /// <summary>
        /// Starts a workflow asynchronously with the specified parameters.
        /// </summary>
        /// <param name="workflowType">The type of the workflow to start.</param>
        /// <param name="agentName">The name of the agent initiating the workflow.</param>
        /// <param name="parameters">The parameters for the workflow.</param>
        /// <param name="queueName">The queue name for the workflow (optional).</param>
        /// <param name="workflowId">The workflow ID (optional).</param>
        public async Task StartWorkflowAsync(string workflowType, string agentName, string[] parameters, string queueName = null, string workflowId = null)
        {
            try
            {
                // Create the workflow request with specified parameters.
                var workflowRequest = new WorkflowRequest
                {
                    WorkflowType = workflowType,
                    AgentName = agentName,
                    Parameters = parameters,
                    QueueName = queueName,
                    WorkflowId = workflowId
                };

                _logger.LogInformation("Preparing to start workflow with type '{WorkflowType}'", workflowRequest.WorkflowType);

                if (PlatformConfig.APP_SERVER_API_KEY != null && PlatformConfig.APP_SERVER_URL != null)
                {
                    // Initialize the secure API client.
                    SecureApi.InitializeClient(
                        PlatformConfig.APP_SERVER_API_KEY,
                        PlatformConfig.APP_SERVER_URL
                    );

                    // Instantiate the WorkflowStarter using the secure API instance and global logger factory.
                    var workflowStarter = new WorkflowStarter(Globals.LogFactory, SecureApi.Instance);

                    // Start the workflow asynchronously.
                    await workflowStarter.StartWorkflow(workflowRequest);

                    _logger.LogInformation("Workflow '{WorkflowType}' has been started successfully", workflowRequest.WorkflowType);
                }
                else
                {
                    _logger.LogError("App server connection failed because of missing configuration");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting workflow: {WorkflowType}", workflowType);
                throw;
            }
        }
    }
}
