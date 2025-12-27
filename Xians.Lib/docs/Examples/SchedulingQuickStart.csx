// Scheduling Quick Start Example
// This example demonstrates basic usage of the Xians.Lib Scheduling SDK

using Xians.Lib.Agents;
using Xians.Lib.Agents.Scheduling;

// Assuming you have already initialized XiansPlatform and registered an agent
// var xiansPlatform = await XiansPlatform.InitializeAsync(...);
// var agent = xiansPlatform.Agents.Register(...);

// Define your custom workflow
var workflow = await agent.Workflows.DefineCustom<CompanyResearchWorkflow>(workers: 1);

// ===== Example 1: Daily Schedule =====
// Run company research every day at 9 AM
var dailySchedule = await workflow.Schedules!
    .Create("daily-company-research")
    .Daily(hour: 9, minute: 0)
    .WithInput("ACME Corp")
    .StartAsync();

Console.WriteLine($"✅ Created daily schedule: {dailySchedule.Id}");

// ===== Example 2: Interval Schedule =====
// Run status check every 30 minutes
var intervalSchedule = await workflow.Schedules!
    .Create("status-check")
    .EveryMinutes(30)
    .WithInput("health-check")
    .SkipIfRunning() // Skip if previous execution is still running
    .StartAsync();

Console.WriteLine($"✅ Created interval schedule: {intervalSchedule.Id}");

// ===== Example 3: Weekday Schedule =====
// Run reports on weekdays at 5 PM
var weekdaySchedule = await workflow.Schedules!
    .Create("weekday-report")
    .Weekdays(hour: 17, minute: 0, timezone: "America/New_York")
    .WithInput("daily-summary")
    .StartAsync();

Console.WriteLine($"✅ Created weekday schedule: {weekdaySchedule.Id}");

// ===== Example 4: One-Time Schedule =====
// Run once at a specific time
var oneTimeSchedule = await workflow.Schedules!
    .Create("year-end-report")
    .WithCalendarSchedule(new DateTime(2025, 12, 31, 23, 59, 0))
    .WithInput("annual-report")
    .StartAsync();

Console.WriteLine($"✅ Created one-time schedule: {oneTimeSchedule.Id}");

// ===== Managing Schedules =====

// Pause a schedule
await dailySchedule.PauseAsync("Maintenance period");
Console.WriteLine("⏸️ Paused daily schedule");

// Resume a schedule  
await dailySchedule.UnpauseAsync("Maintenance complete");
Console.WriteLine("▶️ Resumed daily schedule");

// Trigger immediate execution (outside normal schedule)
await dailySchedule.TriggerAsync();
Console.WriteLine("🚀 Triggered immediate execution");

// Get schedule information
var description = await dailySchedule.DescribeAsync();
Console.WriteLine($"📊 Next run: {description.Info.NextActionTimes.FirstOrDefault()}");
Console.WriteLine($"📊 Recent runs: {description.Info.RecentActions.Count()}");

// List all schedules
var allSchedules = await workflow.Schedules.ListAsync();
Console.WriteLine("\n📋 All Schedules:");
await foreach (var schedule in allSchedules)
{
    Console.WriteLine($"  - {schedule.Id}");
}

// Delete a schedule
await oneTimeSchedule.DeleteAsync();
Console.WriteLine($"🗑️ Deleted schedule: {oneTimeSchedule.Id}");

// ===== Advanced: Using Cron Expressions =====
var cronSchedule = await workflow.Schedules!
    .Create("complex-schedule")
    .WithCronSchedule("0 */6 * * *") // Every 6 hours
    .WithInput("periodic-sync")
    .WithRetryPolicy(new Temporalio.Common.RetryPolicy
    {
        MaximumAttempts = 3,
        InitialInterval = TimeSpan.FromSeconds(10)
    })
    .WithTimeout(TimeSpan.FromMinutes(30))
    .StartAsync();

Console.WriteLine($"✅ Created cron schedule: {cronSchedule.Id}");

Console.WriteLine("\n✨ Scheduling Quick Start Complete!");

