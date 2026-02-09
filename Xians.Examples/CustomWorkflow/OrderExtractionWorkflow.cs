using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;


namespace Xians.Examples.CustomWorkflow;


[Description("Extracts orders from a URL in a periodic manner")]
[Workflow("Order Manager Agent:Order Extraction Workflow")]
public class OrderExtractionWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(
        [Description("Frequency in seconds")]
        int frequency,
        [Description("Comma separated list of URLs to process")]
        string urls)
    {
        Workflow.Logger.LogInformation(
            "Processing schedule with frequency {Frequency} for URL {URL}",
            frequency,
            urls);

        // Create the schedule if not existing
        await XiansContext.CurrentAgent.Schedules
            .Create<OrderExtractionWorkflow>($"custom-schedule")
            .WithIntervalSchedule(TimeSpan.FromSeconds(frequency))
            .WithInput(new object[] { frequency, urls })
            .CreateIfNotExistsAsync();

        //for each url
        foreach (var url in urls.Split(','))
        {
            await ProcessUrl(url);
        }

        // return the result of the URL processing
        return "Order extraction completed";
    }

    private async Task<string> ProcessUrl(string url)
    {
        Workflow.Logger.LogInformation("Starting sub workflow UrlReaderWorkflow for URL: {URL}", url);
        // Synchronous call to the sub workflow
        // Pass url as idPostfix to ensure each URL gets a unique child workflow ID
        var result = await XiansContext.Workflows.ExecuteAsync<UrlReaderWorkflow, string>(new object[] { url }, url);
        Workflow.Logger.LogInformation("Sub workflow UrlReaderWorkflow completed with result: {Result}", result);
        return result;
    }

}