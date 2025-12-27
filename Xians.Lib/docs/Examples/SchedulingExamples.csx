using Xians.Lib.Agents;
using Xians.Lib.Agents.Scheduling;
using Temporalio.Client.Schedules;

/// <summary>
/// Examples demonstrating the Xians.Lib Scheduling SDK.
/// </summary>
public class SchedulingExamples
{
    // Example workflow for demonstration
    public class DailyReportWorkflow
    {
        // Workflow implementation
    }

    /// <summary>
    /// Example 1: Simple daily schedule using cron expression
    /// </summary>
    public static async Task SimpleDailyScheduleExample(XiansWorkflow workflow)
    {
        // Schedule to run every day at 9:00 AM UTC
        var schedule = await workflow.Schedules!
            .Create("daily-report")
            .WithCronSchedule("0 9 * * *")
            .WithInput("Generate daily report")
            .StartAsync();

        Console.WriteLine($"Created schedule: {schedule.Id}");
    }

    /// <summary>
    /// Example 2: Using helper extension methods for common patterns
    /// </summary>
    public static async Task ExtensionMethodsExample(XiansWorkflow workflow)
    {
        // Daily at 9:00 AM in New York timezone
        var dailySchedule = await workflow.Schedules!
            .Create("daily-ny-report")
            .Daily(hour: 9, minute: 0, timezone: "America/New_York")
            .WithInput("NY Daily Report")
            .StartAsync();

        // Weekly on Mondays at 10:00 AM
        var weeklySchedule = await workflow.Schedules!
            .Create("weekly-monday")
            .Weekly(DayOfWeek.Monday, hour: 10, minute: 0)
            .WithInput("Weekly Report")
            .StartAsync();

        // Every 30 minutes
        var frequentSchedule = await workflow.Schedules!
            .Create("frequent-check")
            .EveryMinutes(30)
            .WithInput("Status Check")
            .StartAsync();

        // Weekdays only at 8:30 AM
        var weekdaySchedule = await workflow.Schedules!
            .Create("weekday-morning")
            .Weekdays(hour: 8, minute: 30, timezone: "America/Los_Angeles")
            .WithInput("Morning Briefing")
            .StartAsync();
    }

    /// <summary>
    /// Example 3: Interval-based schedule
    /// </summary>
    public static async Task IntervalScheduleExample(XiansWorkflow workflow)
    {
        // Run every 2 hours
        var schedule = await workflow.Schedules!
            .Create("bi-hourly-sync")
            .WithIntervalSchedule(TimeSpan.FromHours(2))
            .WithInput("Sync data")
            .StartAsync();
    }

    /// <summary>
    /// Example 4: One-time scheduled execution
    /// </summary>
    public static async Task OneTimeScheduleExample(XiansWorkflow workflow)
    {
        // Run once at a specific future time
        var futureTime = new DateTime(2025, 12, 31, 23, 59, 0);
        
        var schedule = await workflow.Schedules!
            .Create("end-of-year-report")
            .WithCalendarSchedule(futureTime, timezone: "UTC")
            .WithInput("End of Year Report")
            .StartAsync();
    }

    /// <summary>
    /// Example 5: Schedule with complex input object
    /// </summary>
    public static async Task ComplexInputExample(XiansWorkflow workflow)
    {
        var reportConfig = new
        {
            CompanyName = "ACME Corp",
            ReportType = "Financial",
            IncludeSections = new[] { "Revenue", "Expenses", "Projections" },
            Format = "PDF"
        };

        var schedule = await workflow.Schedules!
            .Create("monthly-financial")
            .Monthly(dayOfMonth: 1, hour: 6, minute: 0)
            .WithInput(reportConfig)
            .StartAsync();
    }

    /// <summary>
    /// Example 6: Schedule with retry policy and timeout
    /// </summary>
    public static async Task ScheduleWithRetryExample(XiansWorkflow workflow)
    {
        var schedule = await workflow.Schedules!
            .Create("resilient-task")
            .Daily(hour: 3, minute: 0)
            .WithInput("Critical Task")
            .WithRetryPolicy(new Temporalio.Common.RetryPolicy
            {
                MaximumAttempts = 5,
                InitialInterval = TimeSpan.FromSeconds(10),
                BackoffCoefficient = 2.0,
                MaximumInterval = TimeSpan.FromMinutes(10)
            })
            .WithTimeout(TimeSpan.FromMinutes(30))
            .StartAsync();
    }

    /// <summary>
    /// Example 7: Schedule with overlap policy
    /// </summary>
    public static async Task OverlapPolicyExample(XiansWorkflow workflow)
    {
        // Skip execution if previous one is still running
        var schedule = await workflow.Schedules!
            .Create("long-running-task")
            .EveryHours(1)
            .WithInput("Long Task")
            .SkipIfRunning()
            .StartAsync();

        // Or allow concurrent executions
        var concurrentSchedule = await workflow.Schedules!
            .Create("concurrent-task")
            .EveryMinutes(5)
            .WithInput("Quick Task")
            .AllowOverlap()
            .StartAsync();
    }

    /// <summary>
    /// Example 8: Start schedule in paused state
    /// </summary>
    public static async Task PausedScheduleExample(XiansWorkflow workflow)
    {
        var schedule = await workflow.Schedules!
            .Create("future-activation")
            .Daily(hour: 10)
            .WithInput("Task")
            .StartPaused(paused: true, note: "Will activate after review")
            .StartAsync();

        // Later, unpause it
        await schedule.UnpauseAsync("Reviewed and approved");
    }

    /// <summary>
    /// Example 9: Managing existing schedules
    /// </summary>
    public static async Task ManageSchedulesExample(XiansWorkflow workflow)
    {
        // Get an existing schedule
        var schedule = await workflow.Schedules!.GetAsync("daily-report");

        // Describe the schedule
        var description = await schedule.DescribeAsync();
        Console.WriteLine($"Next run: {description.Info.NextActionTimes.FirstOrDefault()}");
        Console.WriteLine($"Recent runs: {description.Info.RecentActions.Count()}");

        // Pause the schedule
        await schedule.PauseAsync("Maintenance period");

        // Resume the schedule
        await schedule.UnpauseAsync("Maintenance complete");

        // Trigger immediate execution
        await schedule.TriggerAsync();

        // Update the schedule
        await schedule.UpdateAsync(input =>
        {
            // Change to run every 2 hours instead
            input.Description.Schedule.Spec.Intervals = new[]
            {
                new ScheduleIntervalSpec { Every = TimeSpan.FromHours(2) }
            };
            return new ScheduleUpdate(input.Description.Schedule);
        });

        // Delete the schedule
        await schedule.DeleteAsync();
    }

    /// <summary>
    /// Example 10: List and filter schedules
    /// </summary>
    public static async Task ListSchedulesExample(XiansWorkflow workflow)
    {
        var schedules = await workflow.Schedules!.ListAsync();

        await foreach (var schedule in schedules)
        {
            var desc = await workflow.Schedules.GetAsync(schedule.Id);
            var details = await desc.DescribeAsync();
            
            Console.WriteLine($"Schedule: {schedule.Id}");
            Console.WriteLine($"  Paused: {details.Schedule.State.Paused}");
            Console.WriteLine($"  Next Run: {details.Info.NextActionTimes.FirstOrDefault()}");
        }
    }

    /// <summary>
    /// Example 11: Backfill missed executions
    /// </summary>
    public static async Task BackfillExample(XiansWorkflow workflow)
    {
        var schedule = await workflow.Schedules!.GetAsync("daily-report");

        // Backfill for the past week
        await schedule.BackfillAsync(new[]
        {
            new ScheduleBackfill
            {
                StartAt = DateTime.UtcNow.AddDays(-7),
                EndAt = DateTime.UtcNow,
                Overlap = ScheduleOverlapPolicy.AllowAll
            }
        });
    }

    /// <summary>
    /// Example 12: System-scoped agent scheduling for specific tenant
    /// </summary>
    public static async Task CrossTenantScheduleExample(XiansWorkflow workflow)
    {
        // Only works with system-scoped agents
        var schedule = await workflow.Schedules!
            .Create("tenant-specific-task")
            .Daily(hour: 8)
            .ForTenant("acme-corp")
            .WithInput("ACME Corp Report")
            .StartAsync();
    }

    /// <summary>
    /// Example 13: Advanced schedule with custom spec
    /// </summary>
    public static async Task AdvancedScheduleExample(XiansWorkflow workflow)
    {
        var spec = new ScheduleSpec
        {
            // Run on weekdays at 9 AM
            CronExpressions = new[]
            {
                new ScheduleCronExpression("0 9 * * 1-5")
                {
                    TimeZoneName = "America/New_York"
                }
            },
            // Also run every 4 hours during weekends
            Intervals = new[]
            {
                new ScheduleIntervalSpec
                {
                    Every = TimeSpan.FromHours(4)
                }
            }
        };

        var schedule = await workflow.Schedules!
            .Create("hybrid-schedule")
            .WithScheduleSpec(spec)
            .WithInput("Hybrid Task")
            .StartAsync();
    }

    /// <summary>
    /// Example 14: Conditional schedule creation
    /// </summary>
    public static async Task ConditionalScheduleExample(XiansWorkflow workflow)
    {
        // Check if schedule already exists
        var exists = await workflow.Schedules!.ExistsAsync("daily-backup");

        if (!exists)
        {
            var schedule = await workflow.Schedules!
                .Create("daily-backup")
                .Daily(hour: 2, minute: 0)
                .WithInput("Backup")
                .StartAsync();
            
            Console.WriteLine("Schedule created");
        }
        else
        {
            Console.WriteLine("Schedule already exists");
        }
    }

    /// <summary>
    /// Example 15: Agent tool for creating schedules
    /// </summary>
    public class ScheduleManagementTool
    {
        private readonly XiansWorkflow _workflow;

        public ScheduleManagementTool(XiansWorkflow workflow)
        {
            _workflow = workflow;
        }

        public async Task<string> CreateDailySchedule(string scheduleName, int hour, string input)
        {
            try
            {
                var schedule = await _workflow.Schedules!
                    .Create(scheduleName)
                    .Daily(hour)
                    .WithInput(input)
                    .StartAsync();

                return $"✅ Schedule '{scheduleName}' created. Runs daily at {hour}:00 UTC";
            }
            catch (Exception ex)
            {
                return $"❌ Failed to create schedule: {ex.Message}";
            }
        }

        public async Task<string> ListAllSchedules()
        {
            var result = new System.Text.StringBuilder();
            var schedules = await _workflow.Schedules!.ListAsync();

            await foreach (var schedule in schedules)
            {
                result.AppendLine($"📅 {schedule.Id}");
            }

            return result.Length > 0 ? result.ToString() : "No schedules found";
        }

        public async Task<string> PauseSchedule(string scheduleName, string? reason = null)
        {
            try
            {
                await _workflow.Schedules!.PauseAsync(scheduleName, reason);
                return $"⏸️ Schedule '{scheduleName}' paused";
            }
            catch (Exception ex)
            {
                return $"❌ Failed to pause schedule: {ex.Message}";
            }
        }
    }
}

