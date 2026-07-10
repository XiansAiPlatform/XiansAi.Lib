# Sub-Workflow Execution

The Xians.Lib SDK provides comprehensive support for starting and executing sub-workflows (child workflows). This allows you to compose complex workflows from simpler, reusable workflow components.

## Overview

Sub-workflows can be started in two contexts:

1. **Within a workflow** - Creates a true child workflow with parent-child relationship
2. **Outside a workflow** - Starts a new independent workflow via the Temporal client

The SDK automatically detects the context and handles both scenarios seamlessly.

## Quick Start

All sub-workflow operations go through `XiansContext.Workflows`, which works both inside and
outside workflow context: inside a workflow it starts a true child workflow; outside a workflow
it uses the Temporal client directly (see [Overview](#overview) above).

### Starting a Sub-Workflow (Fire and Forget)

```csharp
// Using workflow class (recommended - compile-time checked)
await XiansContext.Workflows.StartAsync<ProcessOrderWorkflow>(
    new object[] { orderData },
    uniqueKey: "order-123"
);

// Using workflow type string (for dynamic workflow selection)
await XiansContext.Workflows.StartAsync(
    "MyAgent:ProcessOrder",
    new object[] { orderData },
    uniqueKey: "order-123"
);
```

### Executing a Sub-Workflow (Wait for Result)

```csharp
// Using workflow class (recommended - compile-time checked)
var result = await XiansContext.Workflows.ExecuteAsync<ProcessOrderWorkflow, OrderResult>(
    new object[] { orderData },
    uniqueKey: "order-123"
);

// Using workflow type string
var result = await XiansContext.Workflows.ExecuteAsync<OrderResult>(
    "MyAgent:ProcessOrder",
    new object[] { orderData },
    uniqueKey: "order-123"
);
```

**Note:** Use the workflow type string form whenever the target workflow's class type isn't
available to reference as a generic type parameter (e.g. it lives in another assembly, or is
selected dynamically at runtime). `"WorkflowName"` must match the name declared in the target
class's `[Workflow("AgentName:WorkflowName")]` attribute - not necessarily the C# class name.

## Cross-Agent Sub-Workflows and Activations

Sub-workflows can belong to a **different agent** than the caller - the target agent is always
derived from the workflow type (`"AgentName:WorkflowName"`), so task queue routing works across
agents automatically.

Activations, however, are **agent-specific**. The caller's activation name (idPostfix) therefore
propagates to children as follows:

- **Same-agent child**: the caller's idPostfix is inherited (workflow ID, memo, and search
  attributes), preserving the activation context.
- **Cross-agent child**: the caller's idPostfix is NOT inherited - the caller's activation does
  not exist for the target agent. Pass `activationName` to run the child under one of the target
  agent's activations.
- **Explicit `activationName`**: always wins, for both same-agent and cross-agent children.

```csharp
// Cross-agent child under a specific activation of the target agent
var result = await XiansContext.Workflows.ExecuteAsync<FraudDetectionWorkflow, string>(
    [invoiceId],
    uniqueKey: invoiceId,
    activationName: "fraud-detection-eu"
);

// Cross-agent child with no activation context (default)
var result2 = await XiansContext.Workflows.ExecuteAsync<FraudDetectionWorkflow, string>(
    [invoiceId],
    uniqueKey: invoiceId
);
```

When starting cross-agent children without an activation, provide a `uniqueKey` (e.g. the entity
ID being processed) - otherwise concurrent parents would produce the same child workflow ID.

When an explicit `activationName` is passed, the SDK validates against the server that the
activation exists and is active for the target agent in the current tenant **before** starting the
workflow. This prevents orphaned Temporal workflows sitting on a task queue no worker listens on
(e.g. when the agent is not activated in the tenant). If the activation does not exist or is
deactivated, the call fails instead of starting the workflow with a typed
`ActivationNotFoundException` or `ActivationDeactivatedException` (both derive from
`InvalidOperationException`). The same typed exceptions are thrown in **all contexts**: inside a
workflow the check runs through a system activity (workflows cannot make HTTP calls) that reports
the activation status as a value, and the SDK converts a negative status into the typed exception.
Because the activity completes successfully even for a negative result, Temporal does not log
failed-activity warning traces for an expected "not found" outcome. A plain catch works
everywhere. When no HTTP service is available (local mode), the check is skipped.

```csharp
try
{
    await XiansContext.Workflows.ExecuteAsync<string>(
        "Fraud Detection Agent:Fraud Detection Workflow",
        [invoiceId],
        uniqueKey: invoiceId,
        activationName: "fraud-detection-eu");
}
catch (ActivationNotFoundException ex)
{
    // Target activation does not exist in this tenant
    // ex.AgentName, ex.ActivationName, ex.TenantId
}
catch (ActivationDeactivatedException ex)
{
    // Target activation exists but is deactivated
}
```

If these exceptions are not caught inside a workflow, the workflow **fails** (Xians workers
register both types as workflow failure exception types), rather than suspending on workflow
task retries as unknown exception types normally would in Temporal.

The system-scoped flag used for task queue routing is resolved from the **target agent** when it
is registered in the same process; it falls back to the parent's setting otherwise.

Signaling follows the same rules. `SignalAsync` and `SignalWithStartAsync` include the caller's
idPostfix in the target workflow ID only for same-agent targets. To signal a workflow that runs
under a specific activation (e.g. one started with an explicit `activationName`), use the
`SignalAsync` overload that takes an activation name:

```csharp
await XiansContext.Workflows.SignalAsync<FraudDetectionWorkflow>(
    "review-completed",
    [reviewResult],
    activationName: "fraud-detection-eu"
);
```

`SignalWithStartAsync` also accepts an optional `activationName`. Because signal-with-start can
create a new workflow, an explicit activation is validated up front exactly like
`StartAsync`/`ExecuteAsync` - a missing or deactivated activation throws
`ActivationNotFoundException` / `ActivationDeactivatedException` before anything is started.
`SignalAsync` performs no such validation: it never starts a workflow, so a missing activation
simply means the target workflow is not running and Temporal reports a not-found error.

```csharp
// By workflow class
await XiansContext.Workflows.SignalWithStartAsync<FraudDetectionWorkflow>(
    workflowArgs: [invoiceId],
    signalName: "review-requested",
    uniqueKey: invoiceId,
    activationName: "fraud-detection-eu",
    signalArgs: [reviewRequest]
);

// By workflow type string (useful when the workflow class isn't available)
await XiansContext.Workflows.SignalWithStartAsync(
    "Fraud Detection Agent:Fraud Detection Workflow",
    workflowArgs: [invoiceId],
    signalName: "review-requested",
    uniqueKey: invoiceId,
    activationName: "fraud-detection-eu",
    signalArgs: [reviewRequest]
);
```

## Usage from XiansContext.Workflows

`XiansContext.Workflows` (a `WorkflowHelper` instance) is the entry point for all sub-workflow
operations. Every method has a generic, compile-time-checked overload keyed by workflow class,
and a string-based overload keyed by `"AgentName:WorkflowName"` for cases where the workflow class
is not available (e.g. dynamic workflow selection, or the workflow lives in another assembly).

### StartAsync

Starts a sub-workflow without waiting for completion. Returns immediately after starting.

```csharp
public async Task StartAsync<TWorkflow>(
    object[] args,
    string? uniqueKey = null,
    TimeSpan? executionTimeout = null,
    string? activationName = null)

public async Task StartAsync(
    string workflowType,
    object[] args,
    string? uniqueKey = null,
    TimeSpan? executionTimeout = null,
    string? activationName = null)
```

**Parameters:**

- `TWorkflow` - Generic type parameter for the workflow class (omit and use `workflowType` instead when the class isn't available)
- `workflowType` - The workflow type in format `"AgentName:WorkflowName"`
- `args` - Arguments to pass to the workflow
- `uniqueKey` - Optional unique key appended to the workflow ID for uniqueness
- `executionTimeout` - Optional workflow execution timeout
- `activationName` - Optional target activation name (idPostfix); see [Cross-Agent Sub-Workflows and Activations](#cross-agent-sub-workflows-and-activations)

### ExecuteAsync

Executes a sub-workflow and waits for its result. Returns when the workflow completes.

```csharp
public async Task<TResult> ExecuteAsync<TWorkflow, TResult>(
    object[] args,
    string? uniqueKey = null,
    TimeSpan? executionTimeout = null,
    string? activationName = null)

public async Task<TResult> ExecuteAsync<TResult>(
    string workflowType,
    object[] args,
    string? uniqueKey = null,
    TimeSpan? executionTimeout = null,
    string? activationName = null)
```

**Parameters:**

- Same as `StartAsync`, plus `TResult` - the expected return type from the workflow

### SignalAsync and SignalWithStartAsync

See [Cross-Agent Sub-Workflows and Activations](#cross-agent-sub-workflows-and-activations) above
for `SignalAsync` and `SignalWithStartAsync` usage, including the activation-targeted overloads.
Both also have plain string-based overloads (`SignalAsync(string workflowType, ...)` and
`SignalWithStartAsync(string workflowType, ...)`) mirroring `StartAsync`/`ExecuteAsync`. The
XiansAi.Docs "Workflows" and "Cross-Agent Workflows and Activations" concept pages have the full
method reference tables.

## Examples

### Example 1: Orchestrating Multiple Sub-Workflows

```csharp
[Workflow("OrderService:ProcessOrder")]
public class ProcessOrderWorkflow
{
    [WorkflowRun]
    public async Task<OrderResult> RunAsync(Order order)
    {
        // Start payment processing (fire and forget), by workflow type string
        await XiansContext.Workflows.StartAsync(
            "PaymentService:ProcessPayment",
            new object[] { order.PaymentInfo },
            uniqueKey: order.Id
        );

        // Execute inventory check and wait for result
        var inventoryResult = await XiansContext.Workflows.ExecuteAsync<InventoryResult>(
            "InventoryService:CheckInventory",
            new object[] { order.Items },
            uniqueKey: order.Id
        );

        // Execute shipping workflow
        var shippingResult = await XiansContext.Workflows.ExecuteAsync<ShippingResult>(
            "ShippingService:CreateShipment",
            new object[] { order.ShippingAddress },
            uniqueKey: order.Id
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
            XiansContext.Workflows.StartAsync(
                "NotificationService:SendNotification",
                new object[] { user, message },
                uniqueKey: user.Id
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
            await XiansContext.Workflows.StartAsync(
                "OrderService:ProcessOrder",
                new object[] { order },
                uniqueKey: order.Id
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

For advanced scenarios, you can create custom `SubWorkflowOptions` (used internally by
`XiansContext.Workflows`, and available directly for full control over child-workflow start):

```csharp
using Temporalio.Common;
using Xians.Lib.Agents.Workflows;

// Within a workflow
var options = new SubWorkflowOptions(
    workflowType: "MyAgent:MyWorkflow",
    uniqueKeys: new[] { "custom-id" },
    retryPolicy: new RetryPolicy
    {
        MaximumAttempts = 3,
        InitialInterval = TimeSpan.FromSeconds(1)
    }
    // activationName: "some-activation" // optional, for cross-agent targeting
);

await Workflow.StartChildWorkflowAsync("MyAgent:MyWorkflow", new[] { arg1 }, options);
```

### SubWorkflowOptions Properties

- `TaskQueue` - Automatically determined based on system scope (resolved from the target agent when registered in-process, otherwise inherited from parent) and tenant ID
- `Id` - Generated as `{TenantId}:{AgentName}:{WorkflowName}[:{idPostfix}][:{uniqueKey}]` (see [Workflow ID Format](#workflow-id-format) below)
- `Memo` - Propagates `TenantId`, `Agent`, `UserId`, and `SystemScoped`; `Agent` and `idPostfix` are corrected for the child (see Cross-Agent Sub-Workflows above)
- `TypedSearchAttributes` - Propagates searchable attributes for workflow discovery, with `agent`, `tenantId`, and `idPostfix` corrected for the child
- `RetryPolicy` - Defaults to single attempt (fail fast)
- `ParentClosePolicy` - Set to `Abandon` (child continues if parent closes)

**Note:** The `SystemScoped` flag is an agent-level property that determines tenant isolation behavior. It is resolved from the target agent when that agent is registered in the same process, and inherited from the parent workflow otherwise. It cannot be overridden per-call.

## Workflow ID Format

Sub-workflows follow the standard Xians workflow ID format:

```text
{TenantId}:{AgentName}:{WorkflowName}[:{idPostfix}][:{uniqueKey}]
```

Examples:

- `acme-corp:OrderService:ProcessOrder`
- `acme-corp:OrderService:ProcessOrder:order-123`
- `contoso:PaymentService:Charge:payment-456`
- `contoso:PaymentService:Charge:fraud-detection-eu:payment-456` (with an explicit `activationName`)

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
    uniqueKeys: new[] { "instance-1" },
    retryPolicy: new RetryPolicy
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
await XiansContext.Workflows.StartAsync<MyWorkflow>(new object[] { arg1, arg2 }, uniqueKey: "postfix");
await XiansContext.Workflows.ExecuteAsync<MyWorkflow, Result>(new object[] { arg1 }, uniqueKey: "postfix");
```

**Key Improvements:**

- Cleaner async/await patterns with `Async` suffix
- Explicit `activationName` targeting for cross-agent activations (see [Cross-Agent Sub-Workflows and Activations](#cross-agent-sub-workflows-and-activations))
- Better error messages and validation, with typed activation exceptions
- Automatic context detection and handling
- Improved multi-tenancy support

## See Also

- XiansAi.Docs "Workflows" concept page - core `XiansContext.Workflows` API reference
- XiansAi.Docs "Cross-Agent Workflows and Activations" concept page
- [Multi-Tenancy Documentation](./Multi-tenancy.md)
- [Scheduling Documentation](./Scheduling.md)
- [A2A Communication](./A2A.md)
