# A2A Custom Workflow Example

This example demonstrates how to use A2A communication with custom workflows via Temporal's native signal/query/update primitives.

## Overview

- **DataProcessorWorkflow** - Custom workflow that handles signals, queries, and updates
- **CallerWorkflow** - Demonstrates calling the DataProcessorWorkflow using different A2A patterns

## Patterns Demonstrated

### 1. Fire-and-Forget Signal
Send request without waiting for response:
```csharp
await XiansContext.A2A.SendSignalAsync(
    workflow,
    "ProcessData",
    new ProcessRequest { Id = "req-1", Data = "data" });
```

### 2. Synchronous Update
Send request and get immediate response:
```csharp
var result = await XiansContext.A2A.UpdateAsync<ProcessResult>(
    workflow,
    "ProcessDataSync",
    new ProcessRequest { Id = "req-1", Data = "data" });
```

### 3. Query State
Read workflow state without modification:
```csharp
var status = await XiansContext.A2A.QueryAsync<WorkflowStatus>(
    workflow,
    "GetStatus");
```

### 4. Signal + Delayed Query
Send signal, wait, then query for result:
```csharp
// Send signal
await XiansContext.A2A.SendSignalAsync(workflow, "ProcessData", request);

// Wait for processing
await Workflow.DelayAsync(TimeSpan.FromSeconds(2));

// Query result
var result = await XiansContext.A2A.QueryAsync<ProcessResult>(
    workflow,
    "GetResult",
    requestId);
```

## Running the Example

This is a reference implementation showing the code structure. To run:

1. Create an agent and register the workflows
2. Start the DataProcessorWorkflow
3. Start the CallerWorkflow with the DataProcessorWorkflow's ID

Example:
```csharp
var agent = platform.CreateAgent("ExampleAgent");

// Define custom workflows
agent.Workflows.DefineCustom<DataProcessorWorkflow>();
agent.Workflows.DefineCustom<CallerWorkflow>();

// Run workflows
await agent.RunAllAsync(cancellationToken);
```

## Key Concepts

- **Signals** - Asynchronous, fire-and-forget
- **Queries** - Read-only, synchronous state access
- **Updates** - Synchronous request-response with validation
- **Validation** - Updates can validate inputs before processing

## See Also

- [A2A-CustomWorkflows.md](../../Xians.Lib/docs/A2A-CustomWorkflows.md) - Full documentation
- [A2A.md](../../Xians.Lib/docs/A2A.md) - Built-in workflow A2A


