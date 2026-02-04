# Scheduling

Modern, fluent API for scheduling workflow executions. Works seamlessly both inside and outside workflows with automatic determinism and multi-tenant isolation.

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
✅ **Fluent API** - Modern, chainable interface with IntelliSense  
✅ **Multi-Tenant** - Automatic tenant isolation and security  
✅ **Full Control** - Create, pause, resume, trigger, update, delete schedules  
✅ **Overlap Policies** - Control behavior when schedules overlap  

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
// Daily at specific time
.Daily(hour: 9, minute: 30, timezone: "America/New_York")

// Every hour at specific minute
.Hourly(minute: 15)
```

### Interval Schedules
```csharp
// Simple intervals
.EverySeconds(30)
.EveryMinutes(30)
.EveryHours(2)
.EveryDays(3)  // Every 3 days

// Custom intervals with optional offset
.WithIntervalSchedule(TimeSpan.FromSeconds(10))
.WithIntervalSchedule(TimeSpan.FromMinutes(5), offset: TimeSpan.FromSeconds(30))
```

### Weekly Schedules
```csharp
// Specific day of week
.Weekly(DayOfWeek.Monday, hour: 10, timezone: "America/New_York")

// Weekdays only (Mon-Fri)
.Weekdays(hour: 8, minute: 30, timezone: "America/New_York")
```

### Monthly Schedules
```csharp
// First of month at 9 AM
.Monthly(dayOfMonth: 1, hour: 9, timezone: "America/New_York")

// 15th of every month at midnight
.Monthly(dayOfMonth: 15, hour: 0)
```

### Cron Expressions
```csharp
.WithCronSchedule("0 9 * * *")           // Daily at 9 AM UTC
.WithCronSchedule("0 9 * * 1-5")         // Weekdays at 9 AM UTC
.WithCronSchedule("0 0 1 * *")           // First of month at midnight
.WithCronSchedule("*/30 * * * *")        // Every 30 minutes
.WithCronSchedule("0 */2 * * *")         // Every 2 hours
.WithCronSchedule("0 9 * * *", timezone: "America/New_York")  // With timezone
```

### One-Time/Calendar Schedules
```csharp
// Specific date and time
var scheduledTime = new DateTime(2026, 12, 25, 9, 0, 0);
.WithCalendarSchedule(scheduledTime, timezone: "America/New_York")
```

## Timezone Support

Cron and calendar-based schedules support timezone configuration using IANA timezone names.

### Default Behavior
- **No timezone specified**: Defaults to **UTC**
- **Interval schedules**: Don't use timezones (duration-based, not time-of-day)

### Schedules with Timezone Support
```csharp
// Daily/Weekly/Monthly schedules
.Daily(hour: 9, timezone: "America/New_York")
.Weekly(DayOfWeek.Monday, hour: 10, timezone: "Europe/London")
.Monthly(dayOfMonth: 1, hour: 8, timezone: "Asia/Tokyo")
.Weekdays(hour: 9, timezone: "America/Los_Angeles")

// Cron expressions
.WithCronSchedule("0 9 * * *", timezone: "America/Chicago")

// Calendar-based
.WithCalendarSchedule(scheduledTime, timezone: "Australia/Sydney")
```

### Schedules WITHOUT Timezone Support
```csharp
// Interval-based schedules (duration-based, timezone N/A)
.EveryMinutes(30)
.EveryHours(2)
.WithIntervalSchedule(TimeSpan.FromSeconds(10))
```

### Common Timezone Examples
- **US**: `"America/New_York"`, `"America/Chicago"`, `"America/Denver"`, `"America/Los_Angeles"`
- **Europe**: `"Europe/London"`, `"Europe/Paris"`, `"Europe/Berlin"`
- **Asia**: `"Asia/Tokyo"`, `"Asia/Shanghai"`, `"Asia/Singapore"`
- **Australia**: `"Australia/Sydney"`, `"Australia/Melbourne"`

**Note**: Use IANA timezone database names (e.g., `"America/New_York"`, not `"EST"`).

## Advanced Configuration

### Overlap Policies

Control what happens when a new scheduled execution is triggered while a previous one is still running:

```csharp
// RECOMMENDED: Skip new execution if one is already running
.SkipIfRunning()

// Allow all concurrent executions
.AllowOverlap()

// Buffer one execution to run after current completes
.BufferOne()

// Cancel the currently running execution and start new one
.CancelOther()

// Terminate the currently running execution and start new one (use with caution)
.TerminateOther()

// Or use the overlap-specific method
.WithOverlapPolicy(ScheduleOverlapPolicy.Skip)

// Or the full policy method for advanced configuration
.WithSchedulePolicy(new SchedulePolicy 
{ 
    Overlap = ScheduleOverlapPolicy.Skip,
    CatchupWindow = TimeSpan.FromMinutes(10)
})
```

**When to use each:**
- **SkipIfRunning**: ✅ Best for most cases - prevents pile-up
- **AllowOverlap**: Use when executions are independent and fast
- **BufferOne**: When you must process at least one more time after current
- **CancelOther**: When newer data supersedes current processing
- **TerminateOther**: ⚠️ Use sparingly - forces immediate stop without cleanup

### Retry Policies and Timeouts

```csharp
await workflow.Schedules!
    .Create("resilient-task")
    .Daily(hour: 9)
    .WithInput("data")
    .WithRetryPolicy(new RetryPolicy
    {
        MaximumAttempts = 5,
        InitialInterval = TimeSpan.FromSeconds(10),
        BackoffCoefficient = 2.0,
        MaximumInterval = TimeSpan.FromMinutes(10)
    })
    .WithTimeout(TimeSpan.FromHours(1))
    .StartAsync();
```

### Workflow Memo

Add custom metadata that will be attached to each scheduled workflow execution:

```csharp
.WithMemo(new Dictionary<string, object>
{
    { "environment", "production" },
    { "priority", "high" },
    { "owner", "data-team" }
})
```

### Start Paused

Create a schedule in paused state for later activation:

```csharp
.StartPaused(paused: true, note: "Will be activated after review")
```

## Managing Schedules

### Retrieve and Inspect

```csharp
// Get workflow context
var workflow = XiansContext.CurrentWorkflow;

// Get existing schedule
var schedule = await workflow.Schedules!.GetAsync("my-schedule");

// Get detailed information
var description = await schedule.DescribeAsync();
var nextRun = description.Info.NextActionTimes.FirstOrDefault();
var recentRuns = description.Info.RecentActions;
var isPaused = description.Schedule.State.Paused;

Console.WriteLine($"Schedule: {schedule.Id}");
Console.WriteLine($"Next run: {nextRun}");
Console.WriteLine($"Status: {(isPaused ? "Paused" : "Active")}");
```

### Pause and Resume

```csharp
var workflow = XiansContext.CurrentWorkflow;

// Pause with optional note
await schedule.PauseAsync("System maintenance in progress");

// Resume with optional note
await schedule.UnpauseAsync("Maintenance complete");

// Or use collection methods
await workflow.Schedules!.PauseAsync("my-schedule", "Temporary pause");
await workflow.Schedules!.UnpauseAsync("my-schedule");
```

### Trigger Immediate Execution

```csharp
var workflow = XiansContext.CurrentWorkflow;

// Trigger now (doesn't affect schedule)
await schedule.TriggerAsync();

// Or via collection
await workflow.Schedules!.TriggerAsync("my-schedule");
```

### Update Configuration

```csharp
// Update schedule settings
await schedule.UpdateAsync(input => 
{
    var updatedSchedule = input.Description.Schedule;
    
    // Modify the schedule (e.g., change time)
    updatedSchedule = new Schedule(
        Action: updatedSchedule.Action,
        Spec: new ScheduleSpec
        {
            CronExpressions = new List<string> { "0 10 * * *" }  // Change to 10 AM
        })
    {
        Policy = updatedSchedule.Policy,
        State = updatedSchedule.State
    };
    
    return new ScheduleUpdate(updatedSchedule);
});
```

### Delete Schedule

```csharp
var workflow = XiansContext.CurrentWorkflow;

// Delete via schedule instance
await schedule.DeleteAsync();

// Or via collection
await workflow.Schedules!.DeleteAsync("my-schedule");

// Check existence first
if (await workflow.Schedules!.ExistsAsync("my-schedule"))
{
    await workflow.Schedules!.DeleteAsync("my-schedule");
}
```

### List All Schedules

```csharp
var workflow = XiansContext.CurrentWorkflow;

// List all schedules for this workflow (tenant-filtered)
var schedules = await workflow.Schedules!.ListAsync();

await foreach (var scheduleInfo in schedules)
{
    Console.WriteLine($"ID: {scheduleInfo.Id}");
    
    // Get full details if needed
    var schedule = await workflow.Schedules!.GetAsync(scheduleInfo.Id);
    var description = await schedule.DescribeAsync();
    
    Console.WriteLine($"  Next run: {description.Info.NextActionTimes.FirstOrDefault()}");
    Console.WriteLine($"  Status: {(description.Schedule.State.Paused ? "Paused" : "Active")}");
}
```

### Backfill Missed Executions

```csharp
// Run schedule for past time ranges (useful after downtime)
var backfills = new List<ScheduleBackfill>
{
    new(
        StartAt: DateTime.UtcNow.AddDays(-7),
        EndAt: DateTime.UtcNow.AddDays(-6),
        Overlap: ScheduleOverlapPolicy.AllowAll
    )
};

await schedule.BackfillAsync(backfills);
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
        
        Workflow.Logger.LogDebug("Schedule created: {Id}", schedule.Id);
    }
}
```

**How it works:**
- `Workflow.InWorkflow` detection
- Auto-delegates to system `ScheduleActivities`
- Maintains determinism automatically
- No manual activity registration needed

## Error Handling

The SDK provides specific exceptions for different error scenarios:

```csharp
using Xians.Lib.Agents.Scheduling.Models;

var workflow = XiansContext.CurrentWorkflow;

try
{
    var schedule = await workflow.Schedules!
        .Create("my-schedule")
        .Daily(hour: 9)
        .WithInput("data")
        .StartAsync();
    
    Console.WriteLine($"Schedule created: {schedule.Id}");
}
catch (ScheduleAlreadyExistsException ex)
{
    // Schedule with this ID already exists
    Console.WriteLine($"Schedule {ex.ScheduleId} already exists - skipping");
    
    // Option 1: Get existing schedule
    var existing = await workflow.Schedules!.GetAsync(ex.ScheduleId);
    
    // Option 2: Delete and recreate
    // await workflow.Schedules!.DeleteAsync(ex.ScheduleId);
}
catch (InvalidScheduleSpecException ex)
{
    // Invalid schedule configuration (missing spec, invalid params, etc.)
    Console.WriteLine($"Invalid schedule specification: {ex.Message}");
}
```

### Handling Missing Schedules

```csharp
var workflow = XiansContext.CurrentWorkflow;

try
{
    var schedule = await workflow.Schedules!.GetAsync("non-existent-schedule");
}
catch (ScheduleNotFoundException ex)
{
    Console.WriteLine($"Schedule {ex.ScheduleId} not found");
    
    // Create it instead
    var schedule = await workflow.Schedules!
        .Create(ex.ScheduleId)
        .Daily(hour: 9)
        .StartAsync();
}
```

### Idempotent Schedule Creation

Check for existence before creating:

```csharp
var workflow = XiansContext.CurrentWorkflow;
var scheduleId = "my-recurring-task";

if (!await workflow.Schedules!.ExistsAsync(scheduleId))
{
    await workflow.Schedules!
        .Create(scheduleId)
        .Daily(hour: 9)
        .WithInput("data")
        .StartAsync();
    
    Console.WriteLine("Schedule created");
}
else
{
    Console.WriteLine("Schedule already exists");
}
```

## Complete Examples

### Outside Workflow (Setup Code)

```csharp
using Xians.Lib.Agents;
using Xians.Lib.Agents.Scheduling;
using Xians.Lib.Agents.Scheduling.Models;
using Temporalio.Common;

// Register agent and define workflow
var agent = xiansPlatform.Agents.Register(new XiansAgentRegistration
{
    Name = "My Agent",
    SystemScoped = true
});

var workflow = await agent.Workflows.DefineCustom<MyWorkflow>(workers: 1);

// Create a production-ready schedule with all features
var schedule = await workflow.Schedules!
    .Create("daily-task")
    .Daily(hour: 9, timezone: "America/New_York")
    .WithInput("task-data", "additional-param")
    .WithRetryPolicy(new RetryPolicy
    {
        MaximumAttempts = 3,
        InitialInterval = TimeSpan.FromSeconds(10),
        BackoffCoefficient = 2.0
    })
    .WithTimeout(TimeSpan.FromHours(2))
    .SkipIfRunning()  // Don't start new run if previous still running
    .WithMemo(new Dictionary<string, object>
    {
        { "environment", "production" },
        { "team", "data-engineering" }
    })
    .StartAsync();

Console.WriteLine($"✅ Schedule created: {schedule.Id}");

// Get schedule information
var description = await schedule.DescribeAsync();
Console.WriteLine($"Next run: {description.Info.NextActionTimes.FirstOrDefault()}");

// Manage the schedule
await schedule.PauseAsync("Maintenance window");
await Task.Delay(TimeSpan.FromMinutes(30));  // Simulate maintenance
await schedule.UnpauseAsync("Maintenance complete");

// Trigger immediate execution
await schedule.TriggerAsync();

// List all schedules
var allSchedules = await workflow.Schedules!.ListAsync();
await foreach (var s in allSchedules)
{
    Console.WriteLine($"Schedule: {s.Id}");
}

// Cleanup
await schedule.DeleteAsync();
```

### Inside Workflow (Self-Scheduling)

```csharp
using Temporalio.Workflows;
using Xians.Lib.Agents;
using Xians.Lib.Agents.Scheduling;
using Xians.Lib.Agents.Scheduling.Models;

[Workflow("Content Discovery Workflow")]
public class ContentDiscoveryWorkflow
{
    private readonly ILogger _logger = Workflow.CreateLogger<ContentDiscoveryWorkflow>();

    [WorkflowRun]
    public async Task RunAsync(string contentUrl, int intervalHours)
    {
        _logger.LogInformation("Starting content discovery for {Url}", contentUrl);

        // Process content...
        await ProcessContent(contentUrl);

        // Create recurring schedule for future runs
        // SDK automatically detects workflow context and uses ScheduleActivities!
        try
        {
            var scheduleId = $"content-discovery-{contentUrl}-{intervalHours}h";
            
            var schedule = await XiansContext.CurrentWorkflow.Schedules!
                .Create(scheduleId)
                .EveryHours(intervalHours)
                .WithInput(contentUrl, intervalHours)
                .StartAsync();
            
            _logger.LogInformation("✅ Recurring schedule created: {ScheduleId}", schedule.Id);
        }
        catch (ScheduleAlreadyExistsException ex)
        {
            _logger.LogInformation("Schedule {ScheduleId} already exists", ex.ScheduleId);
        }
        catch (InvalidScheduleSpecException ex)
        {
            _logger.LogError("Invalid schedule specification: {Message}", ex.Message);
            throw;
        }
    }

    private async Task ProcessContent(string url)
    {
        // Content processing logic...
        await Task.CompletedTask;
    }
}
```

### Multiple Schedules Pattern

```csharp
var workflow = XiansContext.CurrentWorkflow;

// Create schedules for multiple entities
var companies = new[] { "ACME Corp", "TechCo", "GlobalInc" };

foreach (var company in companies)
{
    var scheduleId = $"research-{company.ToLower().Replace(" ", "-")}";
    
    try
    {
        var schedule = await workflow.Schedules!
            .Create(scheduleId)
            .Weekdays(hour: 8, minute: 30, timezone: "America/New_York")
            .WithInput(company)
            .SkipIfRunning()
            .StartAsync();
        
        Console.WriteLine($"✅ Schedule created for {company}");
    }
    catch (ScheduleAlreadyExistsException)
    {
        Console.WriteLine($"⚠️ Schedule for {company} already exists");
    }
}
```

## API Reference

### ScheduleBuilder - Fluent Configuration Methods

#### Schedule Timing (choose one)

**Convenience Extensions:**
- `.Daily(hour, minute = 0, timezone?)` - Daily at specific time
- `.Weekly(dayOfWeek, hour, minute = 0, timezone?)` - Weekly on specific day
- `.Monthly(dayOfMonth, hour, minute = 0, timezone?)` - Monthly on specific day
- `.Hourly(minute = 0)` - Every hour at specific minute
- `.Weekdays(hour, minute = 0, timezone?)` - Monday-Friday only
- `.EverySeconds(seconds)` - Interval in seconds
- `.EveryMinutes(minutes)` - Interval in minutes
- `.EveryHours(hours)` - Interval in hours
- `.EveryDays(days, hour = 0, minute = 0, timezone?)` - Interval in days (if days=1, uses Daily)

**Core Methods:**
- `.WithCronSchedule(expression, timezone?)` - Cron expression (5-field format)
- `.WithIntervalSchedule(interval, offset?)` - Duration-based interval
- `.WithCalendarSchedule(dateTime, timezone?)` - Specific date and time
- `.WithScheduleSpec(spec)` - Custom Temporal ScheduleSpec

#### Workflow Configuration

- `.WithInput(params object[] args)` - Input arguments for workflow execution
- `.WithMemo(Dictionary<string, object>)` - Custom metadata attached to workflow
- `.WithRetryPolicy(RetryPolicy)` - Retry policy for failed executions
- `.WithTimeout(TimeSpan)` - Workflow execution timeout

#### Overlap Policies

- `.SkipIfRunning()` - Skip new run if one is already running (✅ recommended)
- `.AllowOverlap()` - Allow all concurrent executions
- `.BufferOne()` - Queue one execution to run after current completes
- `.CancelOther()` - Cancel running execution and start new one
- `.TerminateOther()` - Terminate running execution and start new one (⚠️ use with caution)
- `.WithOverlapPolicy(ScheduleOverlapPolicy)` - Set specific overlap policy
- `.WithSchedulePolicy(SchedulePolicy)` - Custom schedule policy with advanced options

#### Schedule State

- `.StartPaused(paused = true, note?)` - Create schedule in paused state

#### Execution

- `.StartAsync()` - **Creates and starts the schedule** (required terminal method)

---

### ScheduleCollection - Schedule Management

Accessed via `workflow.Schedules!` or `XiansContext.CurrentWorkflow.Schedules!`

#### Creation
- `Create(scheduleId)` → `ScheduleBuilder` - Start building a new schedule

#### Retrieval
- `GetAsync(scheduleId)` → `Task<XiansSchedule>` - Get existing schedule (async)
- `Get(scheduleId)` → `XiansSchedule` - Get existing schedule (sync)
- `ListAsync()` → `Task<IAsyncEnumerable<ScheduleListDescription>>` - List all schedules (tenant-filtered)
- `ExistsAsync(scheduleId)` → `Task<bool>` - Check if schedule exists

#### Lifecycle Management
- `PauseAsync(scheduleId, note?)` → `Task` - Pause schedule
- `UnpauseAsync(scheduleId, note?)` → `Task` - Resume schedule
- `TriggerAsync(scheduleId)` → `Task` - Trigger immediate execution
- `DeleteAsync(scheduleId)` → `Task` - Delete schedule

---

### XiansSchedule - Individual Schedule Instance

#### Properties
- `Id` → `string` - Schedule identifier

#### Information
- `DescribeAsync()` → `Task<ScheduleDescription>` - Get detailed schedule information
  - `Info.NextActionTimes` - Upcoming execution times
  - `Info.RecentActions` - Recent execution history
  - `Schedule.State.Paused` - Whether schedule is paused
  - `Schedule.Spec` - Schedule specification (cron, interval, etc.)

#### Lifecycle Operations
- `PauseAsync(note?)` → `Task` - Pause this schedule
- `UnpauseAsync(note?)` → `Task` - Resume this schedule
- `TriggerAsync()` → `Task` - Trigger immediate execution
- `UpdateAsync(updater)` → `Task` - Modify schedule configuration
- `DeleteAsync()` → `Task` - Delete this schedule
- `BackfillAsync(backfills)` → `Task` - Execute for past time ranges

#### Advanced
- `GetHandle()` → `ScheduleHandle` - Get underlying Temporal handle

---

### Exceptions

Namespace: `Xians.Lib.Agents.Scheduling.Models`

- **`ScheduleAlreadyExistsException`**
  - Thrown when creating a schedule with an ID that already exists
  - Properties: `ScheduleId`

- **`ScheduleNotFoundException`**
  - Thrown when trying to access a schedule that doesn't exist
  - Properties: `ScheduleId`

- **`InvalidScheduleSpecException`**
  - Thrown when schedule specification is invalid or missing
  - Examples: No timing spec provided, invalid cron expression, invalid parameters

---

### Timezone Support

**Supported in:**
- `Daily()`, `Weekly()`, `Monthly()`, `Weekdays()` - IANA timezone parameter
- `WithCronSchedule()` - IANA timezone parameter
- `WithCalendarSchedule()` - IANA timezone parameter

**Not supported in:**
- `EveryMinutes()`, `EveryHours()`, `WithIntervalSchedule()` - Duration-based (no timezone)
- `Hourly()` - Uses cron internally but timezone not typically needed

**Default:** UTC if timezone not specified

**Examples:**
- `"America/New_York"`, `"America/Chicago"`, `"America/Los_Angeles"`
- `"Europe/London"`, `"Europe/Paris"`, `"Europe/Berlin"`
- `"Asia/Tokyo"`, `"Asia/Singapore"`, `"Australia/Sydney"`

## Best Practices

### 1. Always Use Overlap Policies

Prevent issues when scheduled executions take longer than the schedule interval:

```csharp
.EveryHours(1)
.SkipIfRunning()  // Recommended: don't pile up executions
```

### 2. Add Retry Policies for Production

Make scheduled workflows resilient to transient failures:

```csharp
.WithRetryPolicy(new RetryPolicy
{
    MaximumAttempts = 3,
    InitialInterval = TimeSpan.FromSeconds(10),
    BackoffCoefficient = 2.0
})
```

### 3. Use Timezones for User-Facing Schedules

Ensure schedules run at the expected local time:

```csharp
.Daily(hour: 9, timezone: "America/New_York")  // Always 9 AM ET, even during DST changes
```

### 4. Check Existence for Idempotency

Avoid errors when schedules might already exist:

```csharp
var workflow = XiansContext.CurrentWorkflow;

if (!await workflow.Schedules!.ExistsAsync("my-schedule"))
{
    await workflow.Schedules!.Create("my-schedule")...StartAsync();
}
```

### 5. Use Meaningful Schedule IDs

Make schedules easy to identify and manage:

```csharp
// Good: Descriptive, unique
.Create($"daily-sync-{companyId}")

// Bad: Generic, hard to identify
.Create("schedule1")
```

### 6. Add Metadata with Memo

Help track and debug scheduled workflows:

```csharp
.WithMemo(new Dictionary<string, object>
{
    { "createdBy", userId },
    { "purpose", "data-synchronization" },
    { "version", "2.0" }
})
```

---

## Multi-Tenant Security

The SDK automatically handles multi-tenant isolation:

### System-Scoped Agents
- **Tenant from context**: Uses `XiansContext.TenantId` (must be in workflow/activity)
- **Schedule ID format**: `{tenantId}:{scheduleId}`
- **Isolation**: Each tenant's schedules are completely isolated
- **Querying**: `ListAsync()` only returns schedules for current tenant

### Tenant-Scoped Agents
- **Tenant from registration**: Uses `agent.Options.CertificateTenantId`
- **Schedule ID format**: Same as system-scoped
- **Isolation**: Agent can only access schedules for its registered tenant

**You don't need to do anything** - tenant isolation is automatic and enforced! 🔒

---

## What Makes This SDK Special

✅ **Same API everywhere** - Works in workflows AND regular code with automatic context detection  
✅ **Auto-determinism** - SDK automatically uses activities when in workflow context  
✅ **Zero config** - System activities pre-registered, no manual setup needed  
✅ **Type-safe** - Full IntelliSense support with fluent builder pattern  
✅ **Tenant-aware** - Automatic isolation and security (multi-tenant ready)  
✅ **Production-ready** - Retry policies, overlap control, error handling built-in  
✅ **Developer-friendly** - Intuitive API with comprehensive error messages  

---

## Workflow-Aware Execution

The SDK automatically detects if you're in a workflow and adjusts behavior:

```csharp
// Outside workflow: Direct Temporal API calls
var schedule = await workflow.Schedules!.Create("schedule1")...StartAsync();
// → Calls Temporal client directly

// Inside workflow: Uses ScheduleActivities for determinism
[WorkflowRun]
public async Task RunAsync()
{
    var schedule = await XiansContext.CurrentWorkflow.Schedules!
        .Create("schedule1")...StartAsync();
    // → Automatically executes via ScheduleActivities
    // → Maintains workflow determinism
    // → No code changes needed!
}
```

**How it works:**
1. SDK checks `Workflow.InWorkflow` property
2. If true: Delegates to system `ScheduleActivities`
3. If false: Uses Temporal client directly
4. **Same API, same code, different execution paths!**

---

## Limitations

### In-Workflow Constraints

When calling from inside a workflow, only **simple schedule types** are supported via activities:

**✅ Supported:**
- `.WithCronSchedule()` - Cron expressions
- `.EveryMinutes()`, `.EveryHours()` - Interval schedules
- Extension methods: `.Daily()`, `.Weekly()`, `.Weekdays()`, etc. (they use cron)

**❌ Not Supported:**
- `.WithCalendarSchedule()` - Specific date/time calendars
- Complex custom `.WithScheduleSpec()` - Advanced Temporal specs

**Workaround:** Create complex schedules outside the workflow (e.g., in agent setup code or tools).

---

The Xians.Lib Schedule SDK is production-ready and developer-friendly! 🚀

---

## Quick Reference Cheat Sheet

### Common Schedule Patterns

```csharp
// Daily at 9 AM ET
.Daily(hour: 9, timezone: "America/New_York")

// Weekdays at 8:30 AM
.Weekdays(hour: 8, minute: 30, timezone: "America/New_York")

// Intervals
.EverySeconds(30)   // Every 30 seconds
.EveryMinutes(30)   // Every 30 minutes
.EveryHours(2)      // Every 2 hours
.EveryDays(3)       // Every 3 days

// Weekly
.Weekly(DayOfWeek.Monday, hour: 10)

// Monthly
.Monthly(dayOfMonth: 1, hour: 9)

// Custom cron
.WithCronSchedule("0 */2 * * *")  // Every 2 hours
```

### Essential Operations

```csharp
var workflow = XiansContext.CurrentWorkflow;

// Create
var schedule = await workflow.Schedules!
    .Create("my-schedule")
    .Daily(9)
    .WithInput("data")
    .StartAsync();

// Get
var schedule = await workflow.Schedules!.GetAsync("my-schedule");

// Pause/Resume
await schedule.PauseAsync("reason");
await schedule.UnpauseAsync("reason");

// Trigger now
await schedule.TriggerAsync();

// Delete
await schedule.DeleteAsync();

// List all
var schedules = await workflow.Schedules!.ListAsync();
```

### Production Template

```csharp
var workflow = XiansContext.CurrentWorkflow;

var schedule = await workflow.Schedules!
    .Create("production-task")
    .Daily(hour: 9, timezone: "America/New_York")
    .WithInput(param1, param2)
    .WithRetryPolicy(new RetryPolicy
    {
        MaximumAttempts = 3,
        InitialInterval = TimeSpan.FromSeconds(10),
        BackoffCoefficient = 2.0
    })
    .WithTimeout(TimeSpan.FromHours(1))
    .SkipIfRunning()
    .WithMemo(new Dictionary<string, object>
    {
        { "environment", "production" },
        { "owner", "team-name" }
    })
    .StartAsync();
```

---

## Need Help?

- 📖 **Full docs**: Check this guide for detailed information
- 🔍 **Examples**: See `Xians.Agent.Sample/SchedulingDemo.cs`
- 💡 **Best practices**: Follow the patterns in ContentDiscoveryWorkflow
- ⚠️ **Errors**: All exceptions in `Xians.Lib.Agents.Scheduling.Models`

