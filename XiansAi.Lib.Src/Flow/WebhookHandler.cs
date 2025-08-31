using Temporalio.Workflows;
using XiansAi.Logging;

namespace XiansAi.Flow;

/// <summary>
/// Handles chat message processing and conversation management for flows
/// </summary>
public class WebhookHandler : SafeHandler
{
    private readonly Logger<WebhookHandler> _logger = Logger<WebhookHandler>.For();



    /// <summary>
    /// Starts listening for user messages and processing them
    /// </summary>
    public async Task InitWebhookProcessing()
    {
        _logger.LogDebug("Initializing webhook handler");
        
        while (true)
        {
            try 
            {
                _logger.LogDebug("Waiting for continue as new...");
                await Workflow.WaitConditionAsync(() => ShouldContinueAsNew);

                if (ShouldContinueAsNew)
                {
                    // Check if we should continue as new
                    ContinueAsNew();
                }

            }
            catch (ContinueAsNewException)
            {
                _logger.LogDebug("WebhookHandler is continuing as new");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in WebhookHandler", ex);
            }
        }
    }

}
