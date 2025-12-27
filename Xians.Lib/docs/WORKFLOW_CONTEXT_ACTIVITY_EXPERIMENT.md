# Can Library Methods Auto-Switch to Activities in Workflow Context?

## The Question

When a workflow calls a library method, can that library method:
1. Detect it's being called from workflow context using `Workflow.InWorkflow`?
2. Call `Workflow.ExecuteActivityAsync()` to perform I/O operations?

## Call Stack Example

```csharp
[Workflow("My Workflow")]
public class MyWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        // Workflow method
        var workflow = XiansContext.CurrentWorkflow;
        
        // Calls library method
        await workflow.Schedules.Create("test").StartAsync();
        //                                      ↑
        // Can StartAsync() call Workflow.ExecuteActivityAsync()?
    }
}

public class ScheduleBuilder
{
    public async Task<XiansSchedule> StartAsync()
    {
        if (Workflow.InWorkflow)  // ✅ This WILL be true
        {
            // Can we call this from here? 
            await Workflow.ExecuteActivityAsync(...);
            //    ↑ The question: Does this work?
        }
    }
}
```

## Analysis

### ✅ **Detection Works**
`Workflow.InWorkflow` will return `true` when called from library methods invoked by workflows.

**Evidence from codebase**:
```csharp
// XiansContext.cs does this successfully:
public static string WorkflowId
{
    get
    {
        if (Workflow.InWorkflow)  // ← Works even though XiansContext is a library class
        {
            return Workflow.Info.WorkflowId;
        }
    }
}
```

### ✅ **ExecuteActivityAsync Works from Library Methods**

Temporal workflows maintain context across the entire call stack.

**This means**:
- ✅ Library methods CAN call `Workflow.ExecuteActivityAsync()`
- ✅ Workflow context is preserved through regular method calls
- ✅ As long as the call originates from a `[WorkflowRun]` method, activities can be executed

## Proposed Implementation

### Option A: Automatic Activity Switching (Transparent)

```csharp
public class ScheduleBuilder
{
    public async Task<XiansSchedule> StartAsync()
    {
        if (Workflow.InWorkflow)
        {
            // Automatically execute via activity
            return await ExecuteViaActivityAsync();
        }
        else
        {
            // Direct execution (normal path)
            return await ExecuteDirectlyAsync();
        }
    }
    
    private async Task<XiansSchedule> ExecuteViaActivityAsync()
    {
        // Challenge: How do we call the activity?
        // We need a reference to ScheduleManagementActivity
        
        // This won't work because we don't have activity type registered:
        await Workflow.ExecuteActivityAsync(
            () => /* what activity? */
        );
    }
}
```

**Problems with Option A**:
1. ❌ Don't know which activity type to call
2. ❌ Activity must be pre-registered by name/type
3. ❌ Can't dynamically create activity references
4. ❌ Parameters must be serializable (scheduleBuilder state is complex)

### Option B: Activity-Aware API (Semi-Transparent)

```csharp
public class ScheduleCollection
{
    // New method that works in both contexts
    public async Task<XiansSchedule> CreateScheduleAsync(
        string scheduleId,
        string cronExpression,
        object[] input,
        string? timezone = null)
    {
        if (Workflow.InWorkflow)
        {
            // In workflow: delegate to activity by name
            var created = await Workflow.ExecuteActivityAsync(
                "CreateScheduleActivity",  // ← Activity name
                new object[] { scheduleId, _workflowType, cronExpression, input, timezone }
            );
            
            // Return handle to created schedule
            return await GetAsync(scheduleId);
        }
        else
        {
            // Outside workflow: use builder pattern
            return await Create(scheduleId)
                .WithCronSchedule(cronExpression, timezone)
                .WithInput(input)
                .StartAsync();
        }
    }
}
```

**Problems with Option B**:
1. ❌ Still requires activity to be pre-registered
2. ❌ String-based activity names are fragile
3. ❌ Loses fluent builder API when called from workflows
4. ❌ Two different APIs for same operation (confusing)

### Option C: Explicit Activity Pattern with Helpers (Current + Enhanced)

```csharp
// ScheduleCollection adds workflow-friendly helpers
public class ScheduleCollection
{
    /// <summary>
    /// Creates a schedule with automatic workflow context handling.
    /// When called from outside workflows, executes directly.
    /// When called from workflows, provides activity-ready parameters.
    /// </summary>
    public ScheduleCreationParams GetCreationParams(
        string scheduleId,
        string cronExpression,
        object[] input,
        string? timezone = null)
    {
        return new ScheduleCreationParams
        {
            ScheduleId = scheduleId,
            WorkflowType = _workflowType,
            CronExpression = cronExpression,
            Input = input,
            Timezone = timezone
        };
    }
}

// Usage in workflow:
var params = workflow.Schedules.GetCreationParams("test", "0 9 * * *", new[] { "data" });

await Workflow.ExecuteActivityAsync(
    (ScheduleManagementActivity act) => act.CreateScheduleIfNotExists(
        params.ScheduleId,
        params.CronExpression,
        params.Input,
        params.Timezone
    ));
```

**Verdict**: ✅ Feasible but not much better than current

## Fundamental Limitations

### 1. **Activity Registration Challenge**

Activities must be registered BEFORE the workflow runs:

```csharp
// In XiansWorkflow.RunAsync():
workerOptions.AddAllActivities(activityInstance.GetType(), activityInstance);
var worker = new TemporalWorker(client, workerOptions);

// Activities are now available, but ONLY those explicitly registered
```

When `ScheduleBuilder.StartAsync()` tries to call an activity:
- It doesn't know what activity type was registered
- Can't dynamically discover/call activities
- Would need hardcoded activity name or type

### 2. **Temporal SDK Constraints**

```csharp
// This works:
await Workflow.ExecuteActivityAsync(
    (KnownActivityType act) => act.Method(params)
);

// This does NOT work:
var activityType = SomeClass.DiscoverActivity(); // ❌ Can't discover at runtime
await Workflow.ExecuteActivityAsync(activityType, ...); // ❌ No such overload
```

### 3. **State Serialization**

```csharp
public async Task<XiansSchedule> StartAsync()
{
    if (Workflow.InWorkflow)
    {
        // Need to serialize ScheduleBuilder state:
        await Workflow.ExecuteActivityAsync(
            "CreateSchedule",
            new[] {
                _scheduleId,          // ✅ OK
                _scheduleSpec,        // ❌ Complex object
                _workflowType,        // ✅ OK
                _agent,               // ❌ Not serializable!
                _temporalService,     // ❌ Not serializable!
                _retryPolicy,         // ❌ Complex object
                // ... etc
            }
        );
    }
}
```

Most of ScheduleBuilder's state is **not serializable** for activity parameters.

## Conclusion

### ❌ **NOT FEASIBLE** to auto-switch to activities

**Reasons**:
1. **Activity discovery** - Can't dynamically find/call activities from library code
2. **State serialization** - ScheduleBuilder state can't be serialized for activity params
3. **Registration complexity** - Activities must be pre-registered, can't be dynamic
4. **API confusion** - Would create two different execution paths with same API

### ✅ **RECOMMENDED: Keep Current Pattern**

The current pattern is **Temporal best practice**:

```csharp
// Outside workflow: Direct SDK usage
var schedule = await workflow.Schedules
    .Create("daily")
    .Daily(9)
    .StartAsync();

// Inside workflow: Explicit activity
await Workflow.ExecuteActivityAsync(
    (ScheduleManagementActivity act) => act.CreateScheduleIfNotExists(...));
```

**Why this is better**:
- ✅ Clear and explicit
- ✅ Follows Temporal patterns
- ✅ No hidden magic
- ✅ Easy to debug
- ✅ Proper separation of concerns

### Alternative: Add `XiansContext.CurrentWorkflow`?

I can add this helper for convenience, but it won't eliminate the activity pattern. Would you like me to implement it?
