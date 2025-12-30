# Scheduling

Modern, fluent API for scheduling workflow executions. Works seamlessly both inside and outside workflows.

## Quick Start

```csharp
using Xians.Lib.Agents;
using Xians.Lib.Agents.Scheduling;

// Define your workflow
var workflow = await agent.Workflows.DefineCustom<MyWorkflow>(workers: 1);

// Create a schedule - same API everywhere!
var schedule = await workflow.Schedules!
    .Create("daily-task")
    .Daily(hour: 9, timezone: "America/New_York")
    .WithInput("my-data")
    .StartAsync();
```

## Key Features

✅ **Workflow-Aware** - Same API works inside and outside workflows (automatic determinism)  
✅ **Zero Config** - System activities auto-registered  
✅ **Fluent API** - Modern, chainable interface  
✅ **Multi-Tenant** - Automatic tenant isolation  
✅ **Full Control** - Create, pause, resume, trigger, delete schedules  

## Usage Contexts

### Outside Workflows (Setup, Agent Tools)

```csharp
// In Program.cs or agent tools - direct execution
var workflow = await agent.Workflows.DefineCustom<MyWorkflow>(workers: 1);

var schedule = await workflow.Schedules!
    .Create("hourly-sync")
    .EveryHours(1)
    .WithInput("sync-data")
    .StartAsync();
```

### Inside Workflows (Self-Scheduling)

```csharp
using Xians.Lib.Agents;
using Xians.Lib.Agents.Scheduling;

[Workflow("My Workflow")]
public class MyWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        // Same API! Automatically uses ScheduleActivities for determinism
        var schedule = await XiansContext.CurrentWorkflow.Schedules!
            .Create("recurring-task")
            .Daily(hour: 6)
            .WithInput("task-data")
            .StartAsync();
    }
}
```

## Common Patterns

### Daily Schedules
```csharp
.Daily(hour: 9, minute: 30, timezone: "America/New_York")
```

### Interval Schedules
```csharp
.EveryMinutes(30)
.EveryHours(2)
.WithIntervalSchedule(TimeSpan.FromSeconds(10))
```

### Weekly Schedules
```csharp
.Weekly(DayOfWeek.Monday, hour: 10)
.Weekdays(hour: 8, minute: 30)  // Mon-Fri only
```

### Cron Expressions
```csharp
.WithCronSchedule("0 9 * * *")           // Daily at 9 AM
.WithCronSchedule("0 9 * * 1-5")         // Weekdays at 9 AM
.WithCronSchedule("0 0 1 * *")           // First of month
.WithCronSchedule("*/30 * * * *")        // Every 30 minutes
```

## Managing Schedules

```csharp
// Get schedule info
var schedule = await workflow.Schedules!.GetAsync("my-schedule");
var info = await schedule.DescribeAsync();

// Pause/Resume
await schedule.PauseAsync("Maintenance");
await schedule.UnpauseAsync("Ready");

// Trigger now
await schedule.TriggerAsync();

// Delete
await schedule.DeleteAsync();

// List all
var schedules = await workflow.Schedules!.ListAsync();
await foreach (var s in schedules)
{
    Console.WriteLine($"{s.Id} - Next: {s.Info.NextActionTimes.FirstOrDefault()}");
}
```

## Workflow-Aware API

The SDK automatically detects if you're in a workflow and uses activities to maintain determinism:

```csharp
[Workflow("My Workflow")]
public class MyWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        // Call SDK directly - it automatically uses ScheduleActivities!
        var schedule = await XiansContext.CurrentWorkflow.Schedules!
            .Create("self-schedule")
            .EveryMinutes(10)
            .WithInput("data")
            .StartAsync();
        
        Workflow.Logger.LogInformation("Schedule created: {Id}", schedule.Id);
    }
}
```

**How it works:**
- `Workflow.InWorkflow` detection
- Auto-delegates to system `ScheduleActivities`
- Maintains determinism automatically
- No manual activity registration needed

## Error Handling

```csharp
using Xians.Lib.Agents.Scheduling.Models;

try
{
    var schedule = await workflow.Schedules!
        .Create("my-schedule")
        .Daily(9)
        .WithInput("data")
        .StartAsync();
}
catch (ScheduleAlreadyExistsException ex)
{
    // Schedule exists - can update or skip
    Console.WriteLine($"Schedule {ex.ScheduleId} already exists");
}
catch (InvalidScheduleSpecException ex)
{
    // Invalid configuration
    Console.WriteLine($"Invalid spec: {ex.Message}");
}
```

## Complete Example

```csharp
using Xians.Lib.Agents;
using Xians.Lib.Agents.Scheduling;
using Xians.Lib.Agents.Scheduling.Models;

// Setup (Program.cs)
var agent = xiansPlatform.Agents.Register(new XiansAgentRegistration
{
    Name = "My Agent",
    SystemScoped = true
});

var workflow = await agent.Workflows.DefineCustom<MyWorkflow>(workers: 1);

// Create schedule
var schedule = await workflow.Schedules!
    .Create("daily-task")
    .Daily(hour: 9, timezone: "America/New_York")
    .WithInput("task-data")
    .StartAsync();

Console.WriteLine($"Schedule created: {schedule.Id}");

// Manage
await schedule.PauseAsync("Maintenance");
await schedule.UnpauseAsync();
await schedule.TriggerAsync();

// In Workflow (automatic activity delegation!)
[Workflow("My Workflow")]
public class MyWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        // Same API - works in workflows too!
        try
        {
            var schedule = await XiansContext.CurrentWorkflow.Schedules!
                .Create("self-schedule")
                .EveryMinutes(30)
                .WithInput("data")
                .StartAsync();
        }
        catch (ScheduleAlreadyExistsException)
        {
            Workflow.Logger.LogInformation("Schedule already exists");
        }
    }
}
```

## API Reference

### Extension Methods
- `.Daily(hour, minute?, timezone?)` - Daily at specific time
- `.Weekly(dayOfWeek, hour, minute?, timezone?)` - Weekly
- `.Monthly(dayOfMonth, hour, minute?)` - Monthly
- `.Hourly(minute?)` - Every hour
- `.Weekdays(hour, minute?, timezone?)` - Monday-Friday
- `.EveryMinutes(minutes)` - Interval in minutes
- `.EveryHours(hours)` - Interval in hours

### Core Methods
- `.WithCronSchedule(expression, timezone?)` - Cron expression
- `.WithIntervalSchedule(interval, offset?)` - Time interval
- `.WithInput(params)` - Workflow parameters
- `.WithRetryPolicy(policy)` - Retry configuration
- `.WithTimeout(timeout)` - Execution timeout
- `.StartAsync()` - Create the schedule

### Schedule Operations
- `GetAsync(id)` / `Get(id)` - Retrieve schedule
- `ListAsync()` - List all schedules
- `DeleteAsync(id)` - Delete schedule
- `ExistsAsync(id)` - Check existence
- `PauseAsync(id, note?)` - Pause schedule
- `UnpauseAsync(id, note?)` - Resume schedule
- `TriggerAsync(id)` - Trigger now

### XiansSchedule Methods
- `DescribeAsync()` - Get schedule details
- `PauseAsync(note?)` - Pause
- `UnpauseAsync(note?)` - Resume
- `TriggerAsync()` - Run immediately
- `UpdateAsync(updater)` - Modify configuration
- `DeleteAsync()` - Remove schedule
- `BackfillAsync(backfills)` - Replay missed runs

## What's Special

✅ **Same API everywhere** - Works in workflows AND regular code  
✅ **Auto-determinism** - SDK handles workflow constraints  
✅ **Zero config** - System activities pre-registered  
✅ **Type-safe** - Full IntelliSense support  
✅ **Tenant-aware** - Automatic isolation  

The Xians.Lib Schedule SDK is production-ready and developer-friendly! 🚀

