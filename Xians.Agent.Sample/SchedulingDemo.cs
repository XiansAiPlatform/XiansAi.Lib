using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Scheduling;

namespace Xians.Agent.Sample;

/// <summary>
/// Demonstrates how to use the Xians.Lib Schedule SDK with custom workflows.
/// </summary>
public static class SchedulingDemo
{
    /// <summary>
    /// Example: Schedule the CompanyResearchWorkflow to run daily
    /// </summary>
    public static async Task ScheduleDailyResearch(XiansWorkflow workflow)
    {
        Console.WriteLine("📅 Creating daily company research schedule...");

        // Schedule research to run every day at 9 AM Eastern Time
        var schedule = await workflow.Schedules!
            .Create("daily-company-research")
            .Daily(hour: 9, minute: 0, timezone: "America/New_York")
            .WithInput("ACME Corp")
            .StartAsync();

        Console.WriteLine($"✅ Schedule created: {schedule.Id}");
        
        // Get schedule details
        var description = await schedule.DescribeAsync();
        var nextRun = description.Info.NextActionTimes.FirstOrDefault();
        Console.WriteLine($"📊 Next run: {nextRun}");
    }

    /// <summary>
    /// Example: Schedule multiple companies for research
    /// </summary>
    public static async Task ScheduleMultipleCompanies(XiansWorkflow workflow)
    {
        var companies = new[] { "ACME Corp", "TechCo", "GlobalInc" };

        foreach (var company in companies)
        {
            var scheduleName = $"research-{company.ToLower().Replace(" ", "-")}";
            
            var schedule = await workflow.Schedules!
                .Create(scheduleName)
                .Weekdays(hour: 8, minute: 0)
                .WithInput(company)
                .StartAsync();

            Console.WriteLine($"✅ Created schedule for {company}: {schedule.Id}");
        }
    }

    /// <summary>
    /// Example: Create a schedule with retry policy
    /// </summary>
    public static async Task ScheduleWithRetry(XiansWorkflow workflow)
    {
        var schedule = await workflow.Schedules!
            .Create("resilient-research")
            .Daily(hour: 6, minute: 0)
            .WithInput("Important Corp")
            .WithRetryPolicy(new Temporalio.Common.RetryPolicy
            {
                MaximumAttempts = 5,
                InitialInterval = TimeSpan.FromSeconds(10),
                BackoffCoefficient = 2.0f,
                MaximumInterval = TimeSpan.FromMinutes(10)
            })
            .WithTimeout(TimeSpan.FromHours(1))
            .StartAsync();

        Console.WriteLine($"✅ Created resilient schedule: {schedule.Id}");
    }

    /// <summary>
    /// Example: List all schedules
    /// </summary>
    public static async Task ListAllSchedules(XiansWorkflow workflow)
    {
        Console.WriteLine("\n📋 All Schedules:");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        var schedules = await workflow.Schedules!.ListAsync();
        
        await foreach (var scheduleInfo in schedules)
        {
            try
            {
                var schedule = await workflow.Schedules.GetAsync(scheduleInfo.Id);
                var description = await schedule.DescribeAsync();
                
                var status = description.Schedule.State.Paused ? "⏸️ PAUSED" : "▶️ ACTIVE";
                var nextRun = description.Info.NextActionTimes?.FirstOrDefault();
                
                Console.WriteLine($"\n  {status} {scheduleInfo.Id}");
                Console.WriteLine($"    Next run: {(nextRun.HasValue ? nextRun.Value.ToString() : "N/A")}");
                Console.WriteLine($"    Recent runs: {description.Info.RecentActions?.Count() ?? 0}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ {scheduleInfo.Id} - Error: {ex.Message}");
            }
        }
        
        Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    }

    /// <summary>
    /// Example: Pause and resume a schedule
    /// </summary>
    public static async Task PauseResumeSchedule(XiansWorkflow workflow, string scheduleId)
    {
        var schedule = await workflow.Schedules!.GetAsync(scheduleId);

        // Pause
        await schedule.PauseAsync("Temporarily paused for system maintenance");
        Console.WriteLine($"⏸️ Paused: {scheduleId}");

        // Wait a bit (in real scenario, this would be during maintenance)
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Resume
        await schedule.UnpauseAsync("Maintenance complete, resuming normal operations");
        Console.WriteLine($"▶️ Resumed: {scheduleId}");
    }

    /// <summary>
    /// Example: Trigger immediate execution
    /// </summary>
    public static async Task TriggerScheduleNow(XiansWorkflow workflow, string scheduleId)
    {
        var schedule = await workflow.Schedules!.GetAsync(scheduleId);
        
        Console.WriteLine($"🚀 Triggering immediate execution of: {scheduleId}");
        await schedule.TriggerAsync();
        Console.WriteLine("✅ Triggered successfully");
    }

    /// <summary>
    /// Example: Delete a schedule
    /// </summary>
    public static async Task DeleteSchedule(XiansWorkflow workflow, string scheduleId)
    {
        await workflow.Schedules!.DeleteAsync(scheduleId);
        Console.WriteLine($"🗑️ Deleted schedule: {scheduleId}");
    }

    /// <summary>
    /// Comprehensive demo - creates, manages, and cleans up schedules
    /// </summary>
    public static async Task RunFullDemo(XiansWorkflow workflow)
    {
        Console.WriteLine("\n╔════════════════════════════════════════════╗");
        Console.WriteLine("║  Xians.Lib Schedule SDK - Full Demo       ║");
        Console.WriteLine("╚════════════════════════════════════════════╝\n");

        try
        {
            // 1. Create daily schedule
            Console.WriteLine("1️⃣ Creating daily schedule...");
            await ScheduleDailyResearch(workflow);
            await Task.Delay(1000);

            // 2. Create weekly schedules for multiple companies
            Console.WriteLine("\n2️⃣ Creating multiple schedules...");
            await ScheduleMultipleCompanies(workflow);
            await Task.Delay(1000);

            // 3. Create resilient schedule with retry
            Console.WriteLine("\n3️⃣ Creating schedule with retry policy...");
            await ScheduleWithRetry(workflow);
            await Task.Delay(1000);

            // 4. List all schedules
            Console.WriteLine("\n4️⃣ Listing all schedules...");
            await ListAllSchedules(workflow);
            await Task.Delay(1000);

            // 5. Pause and resume
            Console.WriteLine("\n5️⃣ Demonstrating pause/resume...");
            await PauseResumeSchedule(workflow, "daily-company-research");
            await Task.Delay(1000);

            // 6. Trigger immediate execution
            Console.WriteLine("\n6️⃣ Triggering immediate execution...");
            await TriggerScheduleNow(workflow, "daily-company-research");
            await Task.Delay(1000);

            Console.WriteLine("\n✨ Demo complete! Schedules are now active.");
            Console.WriteLine("\n💡 Tip: Use workflow.Schedules to manage these schedules.");
            Console.WriteLine("   - Pause: await schedule.PauseAsync()");
            Console.WriteLine("   - Resume: await schedule.UnpauseAsync()");
            Console.WriteLine("   - Delete: await schedule.DeleteAsync()");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Demo error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Cleanup - deletes all demo schedules
    /// </summary>
    public static async Task Cleanup(XiansWorkflow workflow)
    {
        Console.WriteLine("\n🧹 Cleaning up demo schedules...");

        var scheduleIds = new[]
        {
            "daily-company-research",
            "research-acme-corp",
            "research-techco",
            "research-globalinc",
            "resilient-research"
        };

        foreach (var scheduleId in scheduleIds)
        {
            try
            {
                await workflow.Schedules!.DeleteAsync(scheduleId);
                Console.WriteLine($"  ✅ Deleted: {scheduleId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ Could not delete {scheduleId}: {ex.Message}");
            }
        }

        Console.WriteLine("🧹 Cleanup complete!");
    }
}

