using Temporalio.Workflows;
using XiansAi.Flow;

namespace XiansAi.Router;

public class SemanticRouter
{

    public async Task<string> RouteAsync(IMessageThread messageThread, string systemPrompt, string capabilitiesPluginName, RouterOptions? options = null)
    {

        var response = await Workflow.ExecuteActivityAsync(
            (SystemActivities a) => a.RouteAsync(messageThread, systemPrompt, capabilitiesPluginName, options),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(60) });

        return response;
    }

}