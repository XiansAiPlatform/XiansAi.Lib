# A2A Communication with Custom Workflows

## Overview

This document describes how to enable Agent-to-Agent (A2A) communication with custom workflows using Temporal's native primitives: **Signals**, **Queries**, and **Updates**.

Unlike built-in workflows that use a standardized message handler registry, custom workflows define their own specific signal/query/update handlers with custom signatures. This provides maximum flexibility and type safety.

## Architecture

### Key Components

1. **A2AContextOperations** - SDK methods for sending signals/queries/updates
   - `SendSignalAsync()` - Fire-and-forget signal
   - `QueryAsync<TResult>()` - Read workflow state
   - `UpdateAsync<TResult>()` - Synchronous request-response

2. **A2ASignalQueryActivities** - Temporal activities for workflow context
3. **A2ASignalQueryService** - Direct service for activity context
4. **A2ASignalQueryExecutor** - Context-aware executor (auto-switches between activity/service)

### Context-Aware Execution

All A2A methods automatically handle execution based on context:
- **From workflow**: Executes as activity (retryable, fault-tolerant)
- **From activity**: Calls service directly (avoids nested activities)

## Custom Workflow Implementation

### 1. Define Signal Handlers

Custom workflows use standard Temporal signal handlers:

```csharp
[Workflow("MyAgent:DataProcessor")]
public class DataProcessorWorkflow
{
    private readonly Queue<ProcessRequest> _requestQueue = new();
    
    [WorkflowRun]
    public async Task RunAsync()
    {
        await ProcessRequestsLoopAsync();
    }

    // Signal handler - fire-and-forget
    [WorkflowSignal("ProcessData")]
    public Task ProcessDataSignal(ProcessRequest request)
    {
        _requestQueue.Enqueue(request);
        return Task.CompletedTask;
    }

    private async Task ProcessRequestsLoopAsync()
    {
        while (true)
        {
            await Workflow.WaitConditionAsync(() => _requestQueue.Count > 0);
            var request = _requestQueue.Dequeue();
            await ProcessRequestAsync(request);
        }
    }
}
```

### 2. Define Query Handlers

Queries allow reading workflow state without modification:

```csharp
[Workflow("MyAgent:DataProcessor")]
public class DataProcessorWorkflow
{
    private readonly Dictionary<string, ProcessResult> _results = new();
    
    // Query handler - read-only
    [WorkflowQuery("GetResult")]
    public ProcessResult? GetResult(string requestId)
    {
        _results.TryGetValue(requestId, out var result);
        return result;
    }
    
    [WorkflowQuery("GetStatus")]
    public WorkflowStatus GetStatus()
    {
        return new WorkflowStatus
        {
            PendingRequests = _requestQueue.Count,
            CompletedRequests = _results.Count,
            IsHealthy = true
        };
    }
}
```

### 3. Define Update Handlers (Temporal 1.20+)

Updates provide synchronous request-response with validation:

```csharp
[Workflow("MyAgent:DataProcessor")]
public class DataProcessorWorkflow
{
    // Update handler - validates and returns result synchronously
    [WorkflowUpdate("ProcessDataSync")]
    public async Task<ProcessResult> ProcessDataUpdate(ProcessRequest request)
    {
        // Validate (runs immediately, can reject)
        ValidateRequest(request);
        
        // Process (durable execution)
        var result = await ProcessRequestAsync(request);
        
        // Store
        _results[request.Id] = result;
        
        // Return (client receives this)
        return result;
    }
    
    [WorkflowUpdateValidator(nameof(ProcessDataUpdate))]
    public void ValidateProcessData(ProcessRequest request)
    {
        if (string.IsNullOrEmpty(request.Id))
            throw new ApplicationFailureException("Request ID is required");
            
        if (_results.Count >= MAX_QUEUE_SIZE)
            throw new ApplicationFailureException("Queue is full");
    }
}
```

## Sending A2A Messages

### Pattern 1: Fire-and-Forget Signal

Use signals when you don't need a response:

```csharp
// From another workflow or activity
var dataProcessor = XiansContext.GetCustomWorkflowReference<DataProcessorWorkflow>(
    "data-processor-workflow-123");

await XiansContext.A2A.SendSignalAsync(
    dataProcessor,
    "ProcessData",
    new ProcessRequest 
    { 
        Id = "req-1", 
        Data = "some data" 
    });
```

### Pattern 2: Signal + Query (Async Request-Response)

Send signal, wait, then query for result:

```csharp
var requestId = Guid.NewGuid().ToString();

// Send signal to start processing
await XiansContext.A2A.SendSignalAsync(
    dataProcessor,
    "ProcessData",
    new ProcessRequest { Id = requestId, Data = "some data" });

// Wait for processing (or use polling)
await Workflow.DelayAsync(TimeSpan.FromSeconds(5));

// Query for result
var result = await XiansContext.A2A.QueryAsync<ProcessResult>(
    dataProcessor,
    "GetResult",
    requestId);

if (result != null)
{
    Console.WriteLine($"Processed: {result.Status}");
}
```

### Pattern 3: Query Only (Read State)

Read workflow state without modification:

```csharp
// Check workflow health
var status = await XiansContext.A2A.QueryAsync<WorkflowStatus>(
    dataProcessor,
    "GetStatus");

Console.WriteLine($"Pending: {status.PendingRequests}, Completed: {status.CompletedRequests}");
```

### Pattern 4: Update (Sync Request-Response)

Use updates for synchronous request-response:

```csharp
// Send update and get immediate result
var result = await XiansContext.A2A.UpdateAsync<ProcessResult>(
    dataProcessor,
    "ProcessDataSync",
    new ProcessRequest { Id = "req-1", Data = "some data" });

// Result is available immediately after processing
Console.WriteLine($"Processed: {result.Status}");
```

### Pattern 5: By Workflow ID (Without Workflow Type)

If you only have the workflow ID:

```csharp
await XiansContext.A2A.SendSignalAsync(
    "data-processor-workflow-123",
    "ProcessData",
    new ProcessRequest { Id = "req-1", Data = "data" });

var result = await XiansContext.A2A.QueryAsync<ProcessResult>(
    "data-processor-workflow-123",
    "GetResult",
    "req-1");
```

## Getting Workflow References

### Option 1: Typed Reference

```csharp
var workflow = XiansContext.GetCustomWorkflowReference<DataProcessorWorkflow>(
    "data-processor-workflow-123");
```

### Option 2: By Workflow Type

```csharp
var workflow = XiansContext.GetCustomWorkflowReference(
    "data-processor-workflow-123",
    "MyAgent:DataProcessor");
```

### Option 3: Direct by ID

```csharp
// Just use the workflow ID directly
await XiansContext.A2A.SendSignalAsync(
    "data-processor-workflow-123",
    "ProcessData",
    args);
```

## Complete Example

### DataProcessorWorkflow.cs

```csharp
using Temporalio.Workflows;
using Temporalio.Exceptions;

[Workflow("MyAgent:DataProcessor")]
public class DataProcessorWorkflow
{
    private readonly Queue<ProcessRequest> _requestQueue = new();
    private readonly Dictionary<string, ProcessResult> _results = new();
    private const int MAX_QUEUE_SIZE = 100;

    [WorkflowRun]
    public async Task RunAsync()
    {
        await ProcessRequestsLoopAsync();
    }

    // Signal: Fire-and-forget
    [WorkflowSignal("ProcessData")]
    public Task ProcessDataSignal(ProcessRequest request)
    {
        _requestQueue.Enqueue(request);
        return Task.CompletedTask;
    }

    // Query: Get specific result
    [WorkflowQuery("GetResult")]
    public ProcessResult? GetResult(string requestId)
    {
        _results.TryGetValue(requestId, out var result);
        return result;
    }

    // Query: Get workflow status
    [WorkflowQuery("GetStatus")]
    public WorkflowStatus GetStatus()
    {
        return new WorkflowStatus
        {
            PendingRequests = _requestQueue.Count,
            CompletedRequests = _results.Count,
            IsHealthy = true
        };
    }

    // Update: Synchronous processing
    [WorkflowUpdate("ProcessDataSync")]
    public async Task<ProcessResult> ProcessDataUpdate(ProcessRequest request)
    {
        ValidateRequest(request);
        var result = await ProcessRequestAsync(request);
        _results[request.Id] = result;
        return result;
    }

    [WorkflowUpdateValidator(nameof(ProcessDataUpdate))]
    public void ValidateProcessData(ProcessRequest request)
    {
        if (string.IsNullOrEmpty(request.Id))
            throw new ApplicationFailureException("Request ID is required");
            
        if (_results.Count >= MAX_QUEUE_SIZE)
            throw new ApplicationFailureException("Queue is full");
    }

    private async Task ProcessRequestsLoopAsync()
    {
        while (true)
        {
            await Workflow.WaitConditionAsync(() => _requestQueue.Count > 0);
            var request = _requestQueue.Dequeue();
            var result = await ProcessRequestAsync(request);
            _results[request.Id] = result;
        }
    }

    private async Task<ProcessResult> ProcessRequestAsync(ProcessRequest request)
    {
        // Your processing logic
        var activity = Workflow.CreateActivityInvoker();
        var processed = await Workflow.ExecuteActivityAsync(
            () => activity.ProcessDataActivity(request),
            new() { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
        
        return new ProcessResult
        {
            RequestId = request.Id,
            Status = "Completed",
            Data = processed
        };
    }

    private void ValidateRequest(ProcessRequest request)
    {
        if (string.IsNullOrEmpty(request.Id))
            throw new ApplicationFailureException("Request ID is required");
    }
}

// Models
public class ProcessRequest
{
    public string Id { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

public class ProcessResult
{
    public string RequestId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

public class WorkflowStatus
{
    public int PendingRequests { get; set; }
    public int CompletedRequests { get; set; }
    public bool IsHealthy { get; set; }
}
```

### CallerWorkflow.cs

```csharp
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;

[Workflow("MyAgent:Caller")]
public class CallerWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        // Get reference to target workflow
        var dataProcessor = XiansContext.GetCustomWorkflowReference<DataProcessorWorkflow>(
            "data-processor-workflow-123");

        // Pattern 1: Fire-and-forget
        await XiansContext.A2A.SendSignalAsync(
            dataProcessor,
            "ProcessData",
            new ProcessRequest { Id = "req-1", Data = "async data" });

        // Pattern 2: Sync request-response
        var result = await XiansContext.A2A.UpdateAsync<ProcessResult>(
            dataProcessor,
            "ProcessDataSync",
            new ProcessRequest { Id = "req-2", Data = "sync data" });

        Workflow.Logger.LogInformation("Result: {Status}", result.Status);

        // Pattern 3: Query status
        var status = await XiansContext.A2A.QueryAsync<WorkflowStatus>(
            dataProcessor,
            "GetStatus");

        Workflow.Logger.LogInformation(
            "Status - Pending: {Pending}, Completed: {Completed}",
            status.PendingRequests,
            status.CompletedRequests);
    }
}
```

## Best Practices

### 1. Signal vs Query vs Update

- **Signal**: Use for fire-and-forget, async operations
- **Query**: Use for reading state (no side effects)
- **Update**: Use for synchronous request-response with validation

### 2. Naming Conventions

```csharp
// Clear, descriptive names
[WorkflowSignal("ProcessOrder")]
[WorkflowQuery("GetOrderStatus")]
[WorkflowUpdate("UpdateOrderPriority")]
```

### 3. Validation

Always validate update inputs:

```csharp
[WorkflowUpdateValidator(nameof(ProcessDataUpdate))]
public void ValidateProcessData(ProcessRequest request)
{
    if (string.IsNullOrEmpty(request.Id))
        throw new ApplicationFailureException("Invalid request");
}
```

### 4. Error Handling

```csharp
try
{
    var result = await XiansContext.A2A.UpdateAsync<ProcessResult>(
        workflow, "ProcessData", request);
}
catch (ApplicationFailureException ex)
{
    Workflow.Logger.LogError(ex, "Validation failed");
    // Handle validation error
}
catch (Exception ex)
{
    Workflow.Logger.LogError(ex, "Processing failed");
    // Handle processing error
}
```

### 5. Timeout Configuration

Updates and queries inherit default timeouts. For custom timeouts, signals/queries/updates are executed as activities when in workflow context, so they use standard activity retry policies.

## Comparison: Built-in vs Custom Workflows

| Feature | Built-in Workflows | Custom Workflows |
|---------|-------------------|------------------|
| Handler Registration | `OnUserChatMessage()` | `[WorkflowSignal]` attribute |
| Message Format | Standardized `A2AMessage` | Custom types |
| Response Pattern | Always synchronous | Signal (async), Query (read), Update (sync) |
| Flexibility | Limited to platform patterns | Full control over signatures |
| Type Safety | Runtime validation | Compile-time validation |

## Migration from Built-in A2A

If migrating from built-in workflow A2A:

**Before (Built-in):**
```csharp
var response = await XiansContext.A2A.SendChatToBuiltInAsync(
    "WebWorkflow",
    new A2AMessage { Text = "Fetch data" });
```

**After (Custom):**
```csharp
var workflow = XiansContext.GetCustomWorkflowReference<WebWorkflow>("web-workflow-id");
var result = await XiansContext.A2A.UpdateAsync<WebResult>(
    workflow,
    "FetchData",
    new FetchRequest { Url = "..." });
```

## See Also

- [A2A.md](A2A.md) - A2A communication with built-in workflows
- [Temporal Signals](https://docs.temporal.io/workflows#signal)
- [Temporal Queries](https://docs.temporal.io/workflows#query)
- [Temporal Updates](https://docs.temporal.io/workflows#update)




