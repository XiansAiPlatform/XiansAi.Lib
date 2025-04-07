using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Workflows;
using XiansAi.Flow;
using Temporal;

namespace XiansAi.Flow
{
    public interface IFlowSignalService
    {
        /// <summary>
        /// Sends a signal to a running workflow.
        /// </summary>
        /// <param name="workflowId">The workflow ID.</param>
        /// <param name="logicalSignalName">The logical signal name (for example, "ApprovalSignal").</param>
        /// <param name="signalValue">The signal value (payload).</param>
        Task TriggerEvent(string workflowId, string logicalSignalName, object signalValue);
    }

    public class FlowSignalService : IFlowSignalService
    {
        private readonly TemporalClientService _temporalClientService;
        private readonly ILogger<FlowSignalService> _logger;

        public FlowSignalService()
        {
            _temporalClientService = new TemporalClientService();
            _logger = Globals.LogFactory.CreateLogger<FlowSignalService>();
        }

        /// <summary>
        /// Sends a signal to a running workflow.
        /// </summary>
        /// <param name="workflowId">The workflow ID.</param>
        /// <param name="logicalSignalName">The logical signal name to route inside the workflow.</param>
        /// <param name="signalValue">The payload of the signal.</param>
        public async Task TriggerEvent(string workflowId, string logicalSignalName, object signalValue)
        {
            // Build the payload containing the logical signal name and its associated value.
            var payload = new SignalPayload(logicalSignalName, signalValue);

            // Get the Temporal client.
            var client = await _temporalClientService.GetClientAsync();

            // Obtain the workflow handle using the workflow ID.
            var workflowHandle = client.GetWorkflowHandle(workflowId);

            // Signal the workflow using the handle.
            await workflowHandle.SignalAsync("HandleSignal", new[] { (object?)payload });

            _logger.LogInformation("Sent signal '{LogicalSignalName}' to workflow '{WorkflowId}'",
                logicalSignalName, workflowId);
        }
    }
}
