using Microsoft.Extensions.Logging;
using Temporalio.Workflows;


namespace Xians.Examples.CustomWorkflow;


[Workflow("Order Manager Agent:Url Reader Workflow")]
public class UrlReaderWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(
        string url)
    {
        // Process the URL
        var result = await ProcessUrl(url);

        // return the result of the URL processing
        return result;
    }

    private async Task<string> ProcessUrl(string url)
    {
        Workflow.Logger.LogInformation("Processing URL: {URL}", url);

        // Simulate URL processing
        await Workflow.DelayAsync(TimeSpan.FromSeconds(1));

        return "URL processed successfully. 20 new orders added to the database.";
    }

}