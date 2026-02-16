using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;


namespace Xians.Examples.CustomWorkflow;


[Description("Extracts orders from a URL in a periodic manner")]
[Workflow("Scheduled Workflow Agent:Order Extraction Workflow")]
public class OrderExtractionWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        var frequency = 10;
        var urls = "https://www.google.com,https://www.yahoo.com";
        Workflow.Logger.LogInformation(
            "Processing schedule with frequency {Frequency} for URL {URL}",
            frequency,
            urls);

        // Create the schedule if not existing. CreateIfNotExistsAsync is idempotent.
        await XiansContext.CurrentAgent.Schedules
            .Create<OrderExtractionWorkflow>("custom-schedule")
            .WithIntervalSchedule(TimeSpan.FromSeconds(frequency))
            .WithInput(new object[] { frequency, urls })
            .CreateIfNotExistsAsync();

        //for each url
        foreach (var url in urls.Split(','))
        {
            Workflow.Logger.LogInformation("Order extraction started for URL: {URL}", url);
        }

        // return the result of the URL processing
        return "Order extraction completed";
    }

}