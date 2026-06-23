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
        var urls = "https://www.google.com,https://www.yahoo.com";
        //for each url
        foreach (var url in urls.Split(','))
        {
            Workflow.Logger.LogInformation("Order extraction started for URL: {URL}", url);
        }

        // Create the schedule if not existing. CreateIfNotExistsAsync is idempotent.
        var frequency = 10;
        await XiansContext.CurrentAgent.Schedules
            .Create<OrderExtractionWorkflow>("order-schedule")
            .WithIntervalSchedule(TimeSpan.FromSeconds(frequency))
            .CreateIfNotExistsAsync();

        // return the result of the URL processing
        return "Order extraction completed";
    }

}