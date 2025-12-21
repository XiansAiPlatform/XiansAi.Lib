# Temporal Workflow Constraints

## Critical: Workflows Must Be Deterministic

Temporal workflows must produce the same output given the same input, allowing them to be replayed from history. This requires following specific constraints.

## ❌ **Common Mistakes That Break Determinism**

### Critical: .NET Task Determinism Issues

Many .NET async APIs implicitly use `TaskScheduler.Default` instead of the deterministic `TaskScheduler.Current` required by Temporal. These are easy to accidentally use and will break workflow determinism.

### 1. Using `Task.Run()` in Workflows

**Error**: `Task during workflow run was not scheduled on workflow scheduler`

**❌ WRONG**:
```csharp
[WorkflowSignal]
public async Task HandleInboundChatOrData(InboundMessage message)
{
    await Task.Run(() => _messageQueue.Enqueue(message)); // ❌ BREAKS DETERMINISM
}
```

**✅ CORRECT**:
```csharp
[WorkflowSignal]
public Task HandleInboundChatOrData(InboundMessage message)
{
    _messageQueue.Enqueue(message); // Synchronous operation
    return Task.CompletedTask;      // ✅ Satisfies Temporal's Task requirement
}
```

**Why**: `Task.Run()` schedules work on .NET's ThreadPool, which is outside Temporal's control and breaks replay determinism.

### 2. Using `Thread.Sleep()` or `Task.Delay()` Directly

**❌ WRONG**:
```csharp
await Task.Delay(1000); // ❌ Non-deterministic
Thread.Sleep(1000);     // ❌ Blocks workflow thread
```

**✅ CORRECT**:
```csharp
await Workflow.DelayAsync(TimeSpan.FromSeconds(1)); // ✅ Temporal-managed delay
```

### 3. Using Current Time Directly

**❌ WRONG**:
```csharp
var now = DateTime.Now;     // ❌ Changes on replay
var utcNow = DateTime.UtcNow; // ❌ Changes on replay
```

**✅ CORRECT**:
```csharp
var now = Workflow.UtcNow; // ✅ Deterministic workflow time
```

### 4. Using Random Numbers Directly

**❌ WRONG**:
```csharp
var random = new Random();
var value = random.Next(); // ❌ Different on replay
```

**✅ CORRECT**:
```csharp
var value = Workflow.Random.Next(); // ✅ Deterministic random
```

### 5. Using External I/O Directly

**❌ WRONG**:
```csharp
var data = File.ReadAllText("file.txt");        // ❌ Non-deterministic
var response = await httpClient.GetAsync(url);   // ❌ Non-deterministic
var dbData = await database.QueryAsync(sql);     // ❌ Non-deterministic
```

**✅ CORRECT**:
```csharp
// Use activities for non-deterministic operations
var data = await Workflow.ExecuteActivityAsync(
    (MyActivities act) => act.ReadFileAsync("file.txt"),
    new ActivityOptions { ... }
);
```

### 6. Using Parallel Task Operations Incorrectly

**❌ WRONG**:
```csharp
var task1 = Task.Run(() => DoWork1()); // ❌ Task.Run forbidden
var task2 = Task.Run(() => DoWork2());
await Task.WhenAll(task1, task2);
```

**✅ CORRECT**:
```csharp
// For workflow operations
var task1 = Workflow.ExecuteActivityAsync(...);
var task2 = Workflow.ExecuteActivityAsync(...);
await Workflow.WhenAllAsync(task1, task2); // ✅ Use Workflow.WhenAllAsync

// For background workflow tasks
var task1 = Workflow.RunTaskAsync(() => DoWork1());
var task2 = Workflow.RunTaskAsync(() => DoWork2());
```

### 7. Using `Task.ConfigureAwait(false)`

**❌ WRONG**:
```csharp
await SomeWorkflowOperation().ConfigureAwait(false); // ❌ Loses workflow context
```

**✅ CORRECT**:
```csharp
await SomeWorkflowOperation(); // ✅ Keep context
// OR
await SomeWorkflowOperation().ConfigureAwait(true); // ✅ Explicit context retention
```

**Note**: There's no performance benefit to `ConfigureAwait(false)` in workflows anyway.

### 8. Using `Task.Delay()` or `Task.Wait()`

**❌ WRONG**:
```csharp
await Task.Delay(1000);           // ❌ Non-deterministic timer
Task.Wait(task);                  // ❌ Blocking wait
var cts = new CancellationTokenSource(1000); // ❌ Timeout-based
```

**✅ CORRECT**:
```csharp
await Workflow.DelayAsync(TimeSpan.FromSeconds(1)); // ✅ Deterministic delay
await Workflow.WaitConditionAsync(() => condition); // ✅ Condition wait
var cts = new CancellationTokenSource(); // ✅ No timeout
```

### 9. Using `Task.WhenAny()` or `Task.WhenAll()`

**❌ WRONG**:
```csharp
var completed = await Task.WhenAny(task1, task2); // ❌ Default scheduler
var results = await Task.WhenAll(tasks);          // ❌ Risky
```

**✅ CORRECT**:
```csharp
var completed = await Workflow.WhenAnyAsync(task1, task2); // ✅ Workflow scheduler
var results = await Workflow.WhenAllAsync(tasks);          // ✅ Workflow scheduler
```

**Note**: `Task.WhenAll` is currently safe in .NET but use `Workflow.WhenAllAsync` to be future-proof.

### 10. Using Threading Primitives

**❌ WRONG**:
```csharp
var semaphore = new System.Threading.SemaphoreSlim(1); // ❌ Can deadlock
var mutex = new System.Threading.Mutex();              // ❌ Non-deterministic
await semaphore.WaitAsync();
```

**✅ CORRECT**:
```csharp
var semaphore = new Temporalio.Workflows.Semaphore(1); // ✅ Workflow-safe
var mutex = new Temporalio.Workflows.Mutex();          // ✅ Deterministic
await semaphore.WaitAsync();
```

### 11. Using `CancellationTokenSource.CancelAsync()`

**❌ WRONG**:
```csharp
await cts.CancelAsync(); // ❌ Async cancellation
```

**✅ CORRECT**:
```csharp
cts.Cancel(); // ✅ Synchronous cancellation
```

## ✅ **What IS Allowed in Workflows**

### 1. Synchronous Operations
```csharp
// Queue operations
_messageQueue.Enqueue(item);
var item = _messageQueue.Dequeue();

// Collections
_list.Add(item);
var count = _list.Count;

// Pure computations
var result = Calculate(x, y);
```

### 2. Temporal Workflow APIs
```csharp
// Delays
await Workflow.DelayAsync(TimeSpan.FromSeconds(1));

// Conditions
await Workflow.WaitConditionAsync(() => _queue.Count > 0);

// Background tasks
_ = Workflow.RunTaskAsync(async () => await ProcessAsync());

// Activities
await Workflow.ExecuteActivityAsync(...);

// Time
var now = Workflow.UtcNow;

// Random
var value = Workflow.Random.Next();

// Logging
Workflow.Logger.LogInformation("Message");
```

### 3. Signal and Query Handlers
```csharp
[WorkflowSignal]
public Task HandleSignal(MyData data)
{
    // Synchronous updates are fine
    _state = data;
    return Task.CompletedTask; // Required by Temporal
}

[WorkflowQuery]
public MyState GetState()
{
    // Synchronous reads are fine
    return _state;
}
```

## **Our DefaultWorkflow Implementation**

### Signal Handler ✅
```csharp
[WorkflowSignal("HandleInboundChatOrData")]
public Task HandleInboundChatOrData(InboundMessage message)
{
    // ✅ Simple queue operation - deterministic
    _messageQueue.Enqueue(message);
    return Task.CompletedTask; // ✅ Satisfies Temporal's async requirement
}
```

### Processing Loop ✅
```csharp
private async Task ProcessMessagesLoopAsync()
{
    while (true)
    {
        // ✅ Temporal-managed wait - deterministic
        await Workflow.WaitConditionAsync(() => _messageQueue.Count > 0);
        
        if (_messageQueue.TryDequeue(out var message))
        {
            // ✅ Temporal background task - deterministic
            _ = Workflow.RunTaskAsync(async () =>
            {
                try
                {
                    await ProcessMessageAsync(message);
                }
                catch (Exception ex)
                {
                    // ✅ Temporal logging - deterministic
                    Workflow.Logger.LogError(ex, "Error processing message");
                    
                    try
                    {
                        await SendErrorResponseAsync(message, ex.Message);
                    }
                    catch (Exception errorEx)
                    {
                        Workflow.Logger.LogError(errorEx, "Failed to send error");
                    }
                }
            });
        }
    }
}
```

### HTTP Calls in Context ✅
```csharp
// HTTP calls happen in background workflow tasks
// UserMessageContext.SendMessageToUserAsync is called from user handlers
// which run inside Workflow.RunTaskAsync
private async Task SendMessageToUserAsync(string content, object? data)
{
    var response = await _httpClient.PostAsJsonAsync("/api/messages/send", payload);
    // ✅ This runs inside Workflow.RunTaskAsync in the event loop
    // ✅ Exceptions bubble up to top-level handler
    // ✅ Non-determinism handled by error recovery in event loop
}
```

### Workflow Compliance Checklist ✅

Our `DefaultWorkflow` implementation follows all Temporal rules:

| Component | Compliance | Details |
|-----------|-----------|----------|
| Signal Handler | ✅ | Returns `Task.CompletedTask`, no async operations |
| Queue Operations | ✅ | Direct synchronous `Enqueue`/`Dequeue` |
| Wait Condition | ✅ | Uses `Workflow.WaitConditionAsync()` |
| Background Tasks | ✅ | Uses `Workflow.RunTaskAsync()` |
| Logging | ✅ | Uses `Workflow.Logger` |
| No `Task.Run()` | ✅ | Removed in favor of proper patterns |
| No `Task.Delay()` | ✅ | Not used |
| No Direct I/O | ✅ | HTTP calls in background tasks with error handling |
| No Random/Time | ✅ | Not used |
| Deterministic | ✅ | Queue-based processing, deterministic execution |

## **Why These Constraints Exist**

### Workflow Replay
When a workflow is loaded from history:
1. Temporal replays all events from the beginning
2. Code must execute identically to produce same state
3. External operations (I/O, time, random) would break this

### Example of Replay:
```
Initial Run:
  Event 1: Signal received with message A → Queue message A
  Event 2: Message A processed → Send response A
  
Replay (after restart):
  Event 1: Signal received with message A → Queue message A
  Event 2: Message A processed → Send response A
  
✅ Same result = deterministic
```

### What Breaks Replay:
```
Initial Run:
  Event 1: Signal at 10:00 → Store DateTime.Now = 10:00
  
Replay at 11:00:
  Event 1: Signal at 10:00 → Store DateTime.Now = 11:00
  
❌ Different result = non-deterministic = ERROR
```

## **Best Practices**

### 1. Keep Workflows Simple
- Minimal logic
- Delegate work to activities
- Use signals for external input

### 2. Use Activities for I/O
```csharp
// Workflow
var result = await Workflow.ExecuteActivityAsync(
    (MyActivities act) => act.FetchDataAsync(url),
    new ActivityOptions { 
        StartToCloseTimeout = TimeSpan.FromMinutes(1) 
    }
);

// Activity (can do anything)
public async Task<string> FetchDataAsync(string url)
{
    return await _httpClient.GetStringAsync(url); // ✅ OK in activity
}
```

### 3. Never Block Workflow Thread
```csharp
// ❌ WRONG
Thread.Sleep(1000);

// ✅ CORRECT
await Workflow.DelayAsync(TimeSpan.FromSeconds(1));
```

### 4. Use Workflow APIs for Common Operations
```csharp
// Time
var now = Workflow.UtcNow;

// Random
var random = Workflow.Random.Next(1, 100);

// Logging
Workflow.Logger.LogInformation("Processing {Count} items", count);

// Wait
await Workflow.WaitConditionAsync(() => _ready);
```

## **Testing for Determinism**

### Replay Test
```csharp
[Fact]
public async Task Workflow_Replays_Deterministically()
{
    var worker = new TemporalWorker(client, options);
    
    // Run workflow first time
    var handle = await client.StartWorkflowAsync(...);
    
    // Restart worker (simulates restart)
    await worker.DisposeAsync();
    worker = new TemporalWorker(client, options);
    
    // Workflow should replay correctly
    var result = await handle.GetResultAsync();
    
    Assert.NotNull(result); // ✅ Replay succeeded
}
```

## **Quick Reference**

### .NET Task Gotchas (Critical!)

| ❌ Don't Use | ✅ Use Instead | Why |
|-------------|----------------|-----|
| `Task.Run()` | `Workflow.RunTaskAsync()` | Uses ThreadPool scheduler |
| `Task.Delay()` | `Workflow.DelayAsync()` | Non-deterministic timer |
| `Task.Wait()` | `await` the task | Blocks workflow thread |
| `Task.WhenAny()` | `Workflow.WhenAnyAsync()` | Default scheduler |
| `Task.WhenAll()` | `Workflow.WhenAllAsync()` | Future-proof |
| `ConfigureAwait(false)` | `ConfigureAwait(true)` or omit | Loses workflow context |
| `Task.Factory.StartNew()` | `Workflow.RunTaskAsync()` | Unless with current scheduler |
| `CancellationTokenSource(timeout)` | Non-timeout CTS | Non-deterministic timer |
| `cts.CancelAsync()` | `cts.Cancel()` | Async cancellation issues |
| `SemaphoreSlim` | `Temporalio.Workflows.Semaphore` | Can deadlock workflow |
| `Mutex` | `Temporalio.Workflows.Mutex` | Non-deterministic |

### Other Common Operations

| Operation | In Workflow | Use Instead |
|-----------|-------------|-------------|
| `DateTime.Now/UtcNow` | ❌ | `Workflow.UtcNow` |
| `new Random()` | ❌ | `Workflow.Random` |
| `HttpClient.Get()` | ❌ | Activities |
| `File.Read()` | ❌ | Activities |
| `Database.Query()` | ❌ | Activities |
| `Thread.Sleep()` | ❌ | `Workflow.DelayAsync()` |
| Queue operations | ✅ | Direct use OK |
| Pure functions | ✅ | Direct use OK |
| `Workflow.*` APIs | ✅ | Preferred |

## **⚠️ Third-Party Library Warning**

Be extremely careful with third-party libraries in workflows. Many libraries implicitly use `TaskScheduler.Default` or other non-deterministic operations.

### Examples of Hidden Issues:
- **TPL Dataflow**: Has hidden uses of `TaskScheduler.Default` even when you specify a scheduler
- **Async Libraries**: May use `Task.Run()` internally
- **HTTP Libraries**: May use non-deterministic timeouts or default schedulers
- **ORM Libraries**: Database calls are inherently non-deterministic

### Safe Approach:
1. **Use activities** for any third-party library interactions
2. **Test workflow replay** to catch determinism violations
3. **Review library source** if you must use it in workflows
4. **Prefer Temporal's built-in APIs** whenever possible

### Example: Using External Library Safely
```csharp
// ❌ WRONG - Library in workflow
[WorkflowRun]
public async Task RunAsync()
{
    var result = await ThirdPartyLibrary.DoSomethingAsync(); // ❌ May be non-deterministic
}

// ✅ CORRECT - Library in activity
[WorkflowRun]
public async Task RunAsync()
{
    var result = await Workflow.ExecuteActivityAsync(
        (MyActivities act) => act.UseThirdPartyLibraryAsync(),
        new ActivityOptions { ... }
    );
}

// Activity implementation
public async Task<Result> UseThirdPartyLibraryAsync()
{
    return await ThirdPartyLibrary.DoSomethingAsync(); // ✅ Safe in activity
}
```

## **Summary**

**Golden Rule**: If an operation involves external state, time, randomness, or uses .NET's default task scheduler, it must go through Temporal's workflow APIs or be executed in an activity.

**Fixed Issue**: Changed signal handler from `await Task.Run(() => ...)` to direct `_messageQueue.Enqueue()` to respect Temporal's deterministic execution model.

**Result**: ✅ Workflow now respects Temporal constraints and will replay correctly.

