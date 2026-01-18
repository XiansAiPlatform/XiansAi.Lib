using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Temporalio.Exceptions;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Scheduling;
using Xians.Lib.Agents.Scheduling.Models;

namespace Xians.Examples.CustomWorkflow;


[Description("Processes schedule with frequency in seconds")]
[Workflow("Order Manager Agent:Schedule Workflow")]
public class ScheduleWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(
        [Description("Frequency in seconds")]
        int frequency,
        [Description("URL to process")]
        string url)
    {
        Workflow.Logger.LogInformation(
            "Processing schedule with frequency {Frequency} for URL {URL}",
            frequency,
            url);

        // Create the schedule if not existing
        var schedule = await XiansContext.CurrentWorkflow.Schedules
            .Create($"custom-schedule")
            .WithIntervalSchedule(TimeSpan.FromSeconds(frequency))
            .WithInput(new object[] { frequency, url })
            .CreateIfNotExistsAsync();

        // Process the URL
        var result = await ProcessUrl(url);

        // return the result of the URL processing
        return "Schedule processing completed";
    }

    private async Task<string> ProcessUrl(string url)
    {
        Workflow.Logger.LogInformation("Processing URL: {URL}", url);
        // Add your URL processing logic here
        await Task.CompletedTask;
        return "URL processed successfully : " + url;
    }

}