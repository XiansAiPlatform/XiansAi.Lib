# Feasibility Analysis: Workflow-Aware Schedule SDK

## Proposal

Make `ScheduleCollection` methods workflow-aware so workflows can call schedule operations directly without going through activities:

```csharp
// Current approach (via activity):
var created = await Workflow.ExecuteActivityAsync(
    (ScheduleManagementActivity act) => act.CreateScheduleIfNotExists(...));

// Proposed approach (direct call):
var schedule = await XiansContext.CurrentWorkflow.Schedules
    .Create("my-schedule")
    .Daily(hour: 9)
    .StartAsync();
```

## Technical Analysis

### ✅ Feasible Aspects

#### 1. **XiansContext Integration**
```csharp
// Can add to XiansContext.cs:
public static XiansWorkflow CurrentWorkflow
{
    get
    {
        var agentName = AgentName;
        var workflowType = WorkflowType;
        var agent = GetAgent(agentName);
        return agent.Workflows.GetAll().FirstOrDefault(w => w.WorkflowType == workflowType)
            ?? throw new InvalidOperationException($"Workflow '{workflowType}' not found");
    }
}
```
**Status**: ✅ **Technically feasible**

#### 2. **Workflow Detection**
```csharp
public async Task<XiansSchedule> StartAsync()
{
    if (Workflow.InWorkflow)
    {
        // Handle differently when called from workflow
    }
    else
    {
        // Direct execution
    }
}
```
**Status**: ✅ **Technically feasible**

### ❌ Critical Issues (Blocking)

#### 1. **Workflow Determinism Violation**

**Temporal Requirement**: Workflows MUST be deterministic for replay
- All I/O operations must go through activities
- Direct network calls break determinism
- Creating schedules involves Temporal client calls (I/O)

```csharp
// This violates determinism:
await _temporalService.GetClientAsync(); // ❌ I/O in workflow
await client.CreateScheduleAsync(...);   // ❌ Network call in workflow
```

**Impact**: 
- ❌ Workflow replay would fail
- ❌ Could cause data corruption
- ❌ Violates Temporal's core architecture principles

**Severity**: 🔴 **BLOCKING** - Cannot proceed without activity wrapper

#### 2. **Code Replay Issues**

When Temporal replays workflows:
```csharp
// First execution:
var schedule = await workflow.Schedules.Create("test").StartAsync();
// → Creates schedule "test"

// Replay (after restart):
var schedule = await workflow.Schedules.Create("test").StartAsync();
// → Tries to create again → Error: already exists
// → Workflow state diverges from original execution
```

**Impact**: 
- ❌ Non-deterministic behavior
- ❌ Schedule creation errors on replay
- ❌ Workflow cannot recover from crashes

**Severity**: 🔴 **BLOCKING**

#### 3. **Hidden Activity Complexity**

If we auto-wrap in activities:
```csharp
public async Task<XiansSchedule> StartAsync()
{
    if (Workflow.InWorkflow)
    {
        // Auto-execute as activity
        return await Workflow.ExecuteActivityAsync(
            () => CreateScheduleInternal(...));
    }
}
```

**Problems**:
- ❌ Activity must be pre-registered (chicken-and-egg)
- ❌ Hidden magic - developers don't see activity call
- ❌ Can't configure activity options (timeout, retry)
- ❌ Debugging becomes very difficult
- ❌ Violates principle of explicit over implicit

**Severity**: 🟡 **Major Issue**

## Alternative Approaches

### ✅ Option 1: Workflow-Aware Helper (Recommended)

Provide a workflow helper that makes the activity pattern cleaner:

```csharp
// In ScheduleCollection.cs
public class WorkflowScheduleHelper
{
    private readonly string _workflowType;
    
    public async Task<bool> CreateIfNotExists(
        string scheduleId,
        string cronExpression,
        object[] input,
        string? timezone = null)
    {
        // This is designed to be called from Workflow.ExecuteActivityAsync
        // It returns a simple delegate that can be used with the activity
        // Developers still explicitly use ExecuteActivityAsync but with cleaner syntax
        throw new InvalidOperationException(
            "This method must be called via Workflow.ExecuteActivityAsync. " +
            "Use: await Workflow.ExecuteActivityAsync((ScheduleManagementActivity act) => " +
            "act.CreateScheduleIfNotExists(...))");
    }
}

// Usage in workflow:
var created = await Workflow.ExecuteActivityAsync(
    (ScheduleManagementActivity act) => act.CreateScheduleIfNotExists(
        XiansContext.WorkflowType, // Auto-populated
        "my-schedule",
        "0 9 * * *",
        new[] { "input" }
    ));
```

**Verdict**: ✅ Feasible but doesn't eliminate activity pattern

### ✅ Option 2: Pre-Create Schedules (Current Best Practice)

Create schedules BEFORE starting workflows:

```csharp
// In Program.cs (NOT in workflow):
var workflow = await agent.Workflows.DefineCustom<ScheduledWashWorkflow>(workers: 1);

// Create schedule upfront - no activity needed!
var schedule = await workflow.Schedules!
    .Create("scheduled-wash-every-10sec")
    .WithIntervalSchedule(TimeSpan.FromSeconds(10))
    .WithInput($"wash-{DateTime.UtcNow:yyyyMMddHHmmss}")
    .StartAsync();

// Workflow just runs on schedule - no self-scheduling needed
```

**Benefits**:
- ✅ Simple and clear
- ✅ No determinism issues
- ✅ No activity overhead
- ✅ Schedules created once, workflows execute many times
- ✅ Better separation of concerns

**Verdict**: ✅ **RECOMMENDED** - This is the cleanest approach

### ❌ Option 3: Direct Workflow Calls (Not Feasible)

Allow direct calls from workflows with automatic activity wrapping.

**Verdict**: ❌ **NOT FEASIBLE** due to:
1. Workflow determinism violations
2. Replay issues
3. Hidden complexity
4. Activity registration challenges

## Recommendation

### ✅ **Keep Current Architecture**

The current design is **correct** and follows Temporal best practices:

1. **For one-time setup**: Create schedules in `Program.cs` using the SDK directly
2. **For dynamic scheduling**: Use activities (current pattern with `ScheduleManagementActivity`)
3. **For workflow context**: Use `XiansContext` to access workflow metadata

### Why This Is The Right Pattern

```csharp
// ✅ GOOD: Create schedule outside workflow
var workflow = await agent.Workflows.DefineCustom<MyWorkflow>();
var schedule = await workflow.Schedules.Create("daily").Daily(9).StartAsync();

// ✅ GOOD: Dynamic scheduling via activity (when needed)
await Workflow.ExecuteActivityAsync(
    (ScheduleManagementActivity act) => act.CreateScheduleIfNotExists(...));

// ❌ BAD: Direct call from workflow (violates determinism)
var schedule = await XiansContext.CurrentWorkflow.Schedules
    .Create("test").StartAsync(); // ← Would break replay!
```

## Enhancements We Can Make

### ✅ 1. Add XiansContext.CurrentWorkflow

```csharp
// In XiansContext.cs
public static XiansWorkflow CurrentWorkflow
{
    get
    {
        if (!InWorkflow && !InActivity)
            throw new InvalidOperationException("Not in workflow/activity context");
            
        var agent = CurrentAgent;
        var workflowType = WorkflowType;
        
        return agent.Workflows.GetAll()
            .FirstOrDefault(w => w.WorkflowType == workflowType)
            ?? throw new InvalidOperationException($"Workflow '{workflowType}' not found");
    }
}
```

**Use Case**: Get workflow metadata from within workflows/activities
**Safe**: ✅ Yes - just reading metadata, no I/O

### ✅ 2. Improve Activity API

Make the activity pattern cleaner:

```csharp
// Simplified activity calls with auto-populated workflow type
public async Task<bool> CreateSchedule(
    string scheduleId,
    string cronExpression,
    params object[] input)
{
    // Workflow type automatically determined from context
    var workflowType = XiansContext.WorkflowType;
    ...
}

// Usage becomes cleaner:
var created = await Workflow.ExecuteActivityAsync(
    (ScheduleManagementActivity act) => act.CreateSchedule(
        "my-schedule",
        "0 9 * * *",
        "input-data"
    ));
```

**Safe**: ✅ Yes - still uses activities, just simpler API

### ✅ 3. Guard Against Misuse

Add runtime checks to prevent direct calls from workflows:

```csharp
public async Task<XiansSchedule> StartAsync()
{
    // Guard against calling from workflow
    if (Workflow.InWorkflow)
    {
        throw new InvalidOperationException(
            "Cannot create schedules directly from workflows. " +
            "Workflows must be deterministic. " +
            "Use ScheduleManagementActivity via Workflow.ExecuteActivityAsync() instead.");
    }
    
    // Safe to proceed - not in workflow context
    ...
}
```

**Safe**: ✅ Yes - prevents errors, educates developers

## Conclusion

### ❌ **Direct workflow calls: NOT FEASIBLE**
**Reason**: Violates Temporal's fundamental determinism requirement

### ✅ **Recommended Improvements**:

1. **Add `XiansContext.CurrentWorkflow`** - Safe metadata access
2. **Simplify activity API** - Auto-populate workflow type from context
3. **Add safety guards** - Prevent misuse with clear error messages
4. **Document best practices** - Show when to use direct SDK vs activities

### Current Pattern Is Correct ✅

```csharp
// Setup: Create schedules upfront (most common)
var schedule = await workflow.Schedules.Create(...).StartAsync();

// Dynamic: Use activities when needed (less common)
await Workflow.ExecuteActivityAsync(
    (ScheduleManagementActivity act) => act.CreateSchedule(...));
```

## Decision

**Do NOT make ScheduleCollection directly callable from workflows.**

**Reason**: Would violate Temporal's determinism requirements and cause replay issues.

**Instead**: Enhance the current pattern with better ergonomics and safety guards.

