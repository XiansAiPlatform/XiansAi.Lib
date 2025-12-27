using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Scheduling;
using Xians.Lib.Agents.Scheduling.Models;

[Workflow("Scheduled Wash Workflow")]
public class ScheduledWashWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name = "default")
    {
        // At the start of the workflow, ensure a recurring schedule exists
        // With the new workflow-aware SDK, we can call directly - no manual activity wiring!
        await EnsureScheduleExists(name);

        Workflow.Logger.LogInformation("Processing {Name}", name);

        return "Done";
    }

    /// <summary>
    /// Ensures that a recurring schedule exists for this workflow.
    /// Uses the workflow-aware Schedule SDK - automatically uses activities when in workflow context!
    /// </summary>
    private async Task EnsureScheduleExists(string name)
    {
        try
        {
            Workflow.Logger.LogInformation("Creating schedule for ScheduledWashWorkflow");

            // Get the current workflow instance using XiansContext
            var workflow = XiansContext.CurrentWorkflow;

            // Call the Schedule SDK directly - it automatically detects workflow context
            // and uses ScheduleActivities under the hood!
            var schedule = await workflow.Schedules!
                .Create("scheduled-wash-every-10sec")
                .WithIntervalSchedule(TimeSpan.FromSeconds(10))
                .WithInput($"wash-{DateTime.UtcNow:yyyyMMddHHmmss}")
                .StartAsync();

            Workflow.Logger.LogInformation(
                "✅ Schedule '{ScheduleId}' ensured - will run every 10 seconds",
                schedule.Id);
        }
        catch (ScheduleAlreadyExistsException ex)
        {
            Workflow.Logger.LogInformation(
                "ℹ️ Schedule '{ScheduleId}' already exists, no action needed",
                ex.ScheduleId);
        }
        catch (Exception ex)
        {
            // Log the error but don't fail the workflow
            Workflow.Logger.LogWarning(
                ex,
                "⚠️ Failed to create schedule, but continuing workflow execution");
        }
    }

    // Removed UserApproved signal since approval workflow is commented out
    // [WorkflowSignal]
    // public void UserApproved()
    // {
    //     Workflow.Logger.LogInformation("Received approval signal");
    // }
}