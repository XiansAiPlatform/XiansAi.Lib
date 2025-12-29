# Sub-Workflow Execution

The Xians.Lib SDK provides comprehensive support for starting and executing sub-workflows (child workflows). This allows you to compose complex workflows from simpler, reusable workflow components.

## Overview

Sub-workflows can be started in two contexts:

1. **Within a workflow** - Creates a true child workflow with parent-child relationship
2. **Outside a workflow** - Starts a new independent workflow via the Temporal client

The SDK automatically detects the context and handles both scenarios seamlessly.

## Quick Start

### Starting a Sub-Workflow (Fire and Forget)

```csharp
// Using workflow type
await XiansContext.StartWorkflowAsync("MyAgent:ProcessOrder", "order-123", orderData);

// Using workflow class
await XiansContext.StartWorkflowAsync<ProcessOrderWorkflow>("order-123", orderData);
```

### Executing a Sub-Workflow (Wait for Result)

```csharp
// Using workflow type
var result = await XiansContext.ExecuteWorkflowAsync<OrderResult>(
    "MyAgent:ProcessOrder", 
    "order-123", 
    orderData
);

// Using workflow class
var result = await XiansContext.ExecuteWorkflowAsync<ProcessOrderWorkflow, OrderResult>(
    "order-123", 
    orderData
);
```

## Usage from XiansContext

The `XiansContext` class provides convenient static methods for sub-workflow execution:

### StartWorkflowAsync

Starts a sub-workflow without waiting for completion. Returns immediately after starting.

```csharp
// Method signatures
public static async Task StartWorkflowAsync<TWorkflow>(
    string? idPostfix = null, 
    params object[] args)

public static async Task StartWorkflowAsync(
    string workflowType, 
    string? idPostfix = null, 
    params object[] args)
```

**Parameters:**

- `workflowType` - The workflow type in format "AgentName:WorkflowName"
- `TWorkflow` - Generic type parameter for the workflow class
- `idPostfix` - Optional unique identifier to append to workflow ID
- `args` - Arguments to pass to the workflow

### ExecuteWorkflowAsync

Executes a sub-workflow and waits for its result. Returns when the workflow completes.

```csharp
// Method signatures
public static async Task<TResult> ExecuteWorkflowAsync<TWorkflow, TResult>(
    string? idPostfix = null, 
    params object[] args)

public static async Task<TResult> ExecuteWorkflowAsync<TResult>(
    string workflowType, 
    string? idPostfix = null, 
    params object[] args)
```

**Parameters:**

- Same as `StartWorkflowAsync`
- `TResult` - The expected return type from the workflow

## Direct Usage via SubWorkflowService

For more control, you can use the `SubWorkflowService` directly:

```csharp
using Xians.Lib.Agents.Workflows;

// Start without waiting
await SubWorkflowService.StartAsync<MyWorkflow>("instance-1", arg1, arg2);

// Execute and wait for result
var result = await SubWorkflowService.ExecuteAsync<MyWorkflow, MyResult>(
    "instance-1", 
    arg1, 
    arg2
);
```

## Examples

### Example 1: Orchestrating Multiple Sub-Workflows

```csharp
[Workflow("OrderService:ProcessOrder")]
public class ProcessOrderWorkflow
{
    [WorkflowRun]
    public async Task<OrderResult> RunAsync(Order order)
    {
        // Start payment processing (fire and forget)
        await XiansContext.StartWorkflowAsync(
            "PaymentService:ProcessPayment",
            order.Id,
            order.PaymentInfo
        );

        // Execute inventory check and wait for result
        var inventoryResult = await XiansContext.ExecuteWorkflowAsync<InventoryResult>(
            "InventoryService:CheckInventory",
            order.Id,
            order.Items
        );

        // Execute shipping workflow
        var shippingResult = await XiansContext.ExecuteWorkflowAsync<ShippingResult>(
            "ShippingService:CreateShipment",
            order.Id,
            order.ShippingAddress
        );

        return new OrderResult
        {
            OrderId = order.Id,
            InventoryReserved = inventoryResult.Success,
            ShippingLabel = shippingResult.TrackingNumber
        };
    }
}
```

### Example 2: Parallel Sub-Workflow Execution

```csharp
[Workflow("NotificationService:SendBulkNotifications")]
public class BulkNotificationWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(List<User> users, string message)
    {
        // Start multiple sub-workflows in parallel
        var tasks = users.Select(user =>
            XiansContext.StartWorkflowAsync(
                "NotificationService:SendNotification",
                user.Id,
                user,
                message
            )
        );

        // ✅ Use Workflow.WhenAllAsync instead of Task.WhenAll
        await Workflow.WhenAllAsync(tasks);
    }
}
```

### Example 3: Using Outside of Workflow Context

```csharp
// In a regular application context (not within a workflow)
public class OrderController
{
    public async Task<IActionResult> CreateOrder(Order order)
    {
        try
        {
            // This will use the Temporal client to start a new workflow
            // (not a child workflow since we're not in a workflow context)
            await XiansContext.StartWorkflowAsync(
                "OrderService:ProcessOrder",
                order.Id,
                order
            );

            return Ok(new { orderId = order.Id, status = "processing" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }
}
```

## Advanced Configuration

### Custom SubWorkflowOptions

For advanced scenarios, you can create custom `SubWorkflowOptions`:

```csharp
using Temporalio.Common;
using Xians.Lib.Agents.Workflows;

// Within a workflow
var options = new SubWorkflowOptions(
    workflowType: "MyAgent:MyWorkflow",
    idPostfix: "custom-id",
    retryPolicy: new RetryPolicy 
    { 
        MaximumAttempts = 3,
        InitialInterval = TimeSpan.FromSeconds(1)
    }
);

await Workflow.StartChildWorkflowAsync("MyAgent:MyWorkflow", new[] { arg1 }, options);
```

### SubWorkflowOptions Properties

- `TaskQueue` - Automatically determined based on system scope (inherited from parent) and tenant ID
- `Id` - Generated as `{TenantId}:{WorkflowType}:{OptionalPostfix}`
- `Memo` - Propagates `TenantId`, `Agent`, `UserId`, and `SystemScoped` from parent
- `TypedSearchAttributes` - Propagates searchable attributes for workflow discovery
- `RetryPolicy` - Defaults to single attempt (fail fast)
- `ParentClosePolicy` - Set to `Abandon` (child continues if parent closes)

**Note:** The `SystemScoped` flag is always inherited from the parent workflow and cannot be overridden. This is an agent-level property that determines tenant isolation behavior.

## Workflow ID Format

Sub-workflows follow the standard Xians workflow ID format:

```text
{TenantId}:{WorkflowType}:{OptionalPostfix}
```

Examples:

- `acme-corp:OrderService:ProcessOrder`
- `acme-corp:OrderService:ProcessOrder:order-123`
- `contoso:PaymentService:Charge:payment-456`

## Multi-Tenancy and System Scoping

The sub-workflow service automatically handles:

- **Tenant Isolation** - Sub-workflows inherit the parent's tenant context
- **System Scoped Workflows** - Propagates system-scoped flag to children
- **Task Queue Routing** - Ensures proper worker pool assignment

### System-Scoped Workflows

For system-scoped agents:

- Task Queue: `{WorkflowType}`
- Can process requests from multiple tenants

### Tenant-Scoped Workflows

For non-system-scoped agents:

- Task Queue: `{TenantId}:{WorkflowType}`
- Enforces tenant isolation at worker level

## Error Handling

Sub-workflows use a fail-fast retry policy by default (`MaximumAttempts = 1`). To customize:

```csharp
// Create custom options with retry logic
var options = new SubWorkflowOptions(
    "MyAgent:MyWorkflow",
    "instance-1",
    new RetryPolicy
    {
        MaximumAttempts = 3,
        InitialInterval = TimeSpan.FromSeconds(1),
        MaximumInterval = TimeSpan.FromSeconds(10),
        BackoffCoefficient = 2.0
    }
);
```

## Important: Workflow Determinism Requirements

**⚠️ CRITICAL: Do not use standard .NET Task methods inside workflows!**

Workflows must be deterministic for Temporal to function correctly. You **MUST** use Temporal's workflow-safe alternatives:

### ❌ DO NOT USE (Non-Deterministic)

```csharp
// ❌ Task.Run - Uses default scheduler and thread pool
Task.Run(() => DoWork());

// ❌ Task.Delay - Uses .NET built-in timers
await Task.Delay(TimeSpan.FromSeconds(5));

// ❌ Task.Wait - Blocking call
task.Wait();

// ❌ Task.WhenAny - Not deterministic with results
await Task.WhenAny(task1, task2, task3);

// ❌ Task.WhenAll - Should use Temporal wrapper
await Task.WhenAll(tasks);

// ❌ Task.ConfigureAwait(false) - Loses workflow context
await SomeMethod().ConfigureAwait(false);

// ❌ Timeout-based CancellationTokenSource
var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
```

### ✅ DO USE (Deterministic)

```csharp
// ✅ Workflow.RunTaskAsync - Workflow-safe task execution
await Workflow.RunTaskAsync(() => DoWork());

// OR use Task.Factory.StartNew with current scheduler
await Task.Factory.StartNew(
    () => DoWork(),
    CancellationToken.None,
    TaskCreationOptions.None,
    TaskScheduler.Current
);

// ✅ Workflow.DelayAsync - Deterministic delays
await Workflow.DelayAsync(TimeSpan.FromSeconds(5));

// ✅ Workflow.WhenAnyAsync - Deterministic alternative
await Workflow.WhenAnyAsync(task1, task2, task3);

// ✅ Workflow.WhenAllAsync - Deterministic wrapper
await Workflow.WhenAllAsync(tasks);

// ✅ Task.ConfigureAwait(true) - Preserves workflow context (if needed)
await SomeMethod().ConfigureAwait(true);

// ✅ Workflow.WaitConditionAsync - For waiting on conditions
await Workflow.WaitConditionAsync(() => isReady, TimeSpan.FromMinutes(5));

// ✅ Non-timeout-based cancellation
var cts = new CancellationTokenSource();
```

### Why This Matters

Temporal workflows are **replayed** from history during recovery. Using standard .NET Task methods:

- Introduces non-determinism (thread pool scheduling, wall-clock timers)
- Can cause workflow replay failures
- May lead to data corruption or unexpected behavior

**Always use `Workflow.*` methods inside workflow code.**

## Best Practices

1. **Use Unique ID Postfixes** - When running multiple instances, always provide unique postfixes
2. **Handle Failures Gracefully** - Implement proper error handling and compensation logic
3. **Keep Sub-Workflows Small** - Each workflow should have a single, well-defined responsibility
4. **Propagate Context** - Important metadata (tenant, user) is automatically propagated
5. **Consider Parent-Child Lifecycle** - Understand that children are abandoned when parent closes
6. **Use Workflow.* Methods** - Never use standard Task methods inside workflows (see above)
7. **No Thread Pool Operations** - Avoid `Task.Run`, `ThreadPool.QueueUserWorkItem`, etc.
8. **No Wall-Clock Time** - Use `Workflow.UtcNow` instead of `DateTime.UtcNow`

## Comparison with Old SDK

If migrating from XiansAi.Lib.Src, the new API is similar but improved:

### Old SDK (XiansAi.Lib.Src)

```csharp
await AgentContext.StartWorkflow<MyWorkflow>("postfix", new object[] { arg1, arg2 });
await AgentContext.ExecuteWorkflow<MyWorkflow, Result>("postfix", new object[] { arg1 });
```

### New SDK (Xians.Lib)

```csharp
await XiansContext.StartWorkflowAsync<MyWorkflow>("postfix", arg1, arg2);
await XiansContext.ExecuteWorkflowAsync<MyWorkflow, Result>("postfix", arg1);
```

**Key Improvements:**

- Cleaner async/await patterns with `Async` suffix
- Params array instead of `new object[]`
- Better error messages and validation
- Automatic context detection and handling
- Improved multi-tenancy support

## See Also

- [Workflows Documentation](./Workflows.md)
- [Multi-Tenancy Documentation](./Multi-tenancy.md)
- [Scheduling Documentation](./Scheduling.md)
- [A2A Communication](./A2A.md)
