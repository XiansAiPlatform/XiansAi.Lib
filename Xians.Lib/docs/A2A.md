# Agent-to-Agent (A2A) Communication

## Overview

A2A (Agent-to-Agent) communication enables synchronous request-response messaging between workflows in the same runtime. It's designed for workflows to call other workflows' message handlers directly and receive responses.

**Two Modes:**
1. **Built-in Workflows** - Standardized message handlers (this document)
2. **Custom Workflows** - Temporal signals/queries/updates ([see A2A-CustomWorkflows.md](A2A-CustomWorkflows.md))

This document covers A2A with **built-in workflows**. For custom workflows, see [A2A-CustomWorkflows.md](A2A-CustomWorkflows.md).

## Architecture

### Key Components

1. **A2AClient** - Core client for sending A2A messages
2. **A2AService** - Service for direct message processing (activity context)
3. **A2AActivityExecutor** - Context-aware executor using the Activity Executor pattern
4. **A2AContextOperations** - Simplified API accessible via `XiansContext.A2A`
5. **A2AMessageContext** - Specialized context that captures responses instead of sending to users
6. **A2ACurrentMessage** - Message that captures responses

### Context-Aware Execution

A2A automatically handles execution based on context:
- **From workflow**: Executes target handler in isolated activity (retryable, fault-tolerant)
- **From activity**: Executes target handler directly (avoids nested activities)

## Usage

### Simple API (Within Workflows)

The easiest way to use A2A is through `XiansContext.A2A`:

```csharp
[Workflow]
public class ContentDiscoveryWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        // Send chat message to a built-in workflow by name
        var response = await XiansContext.A2A.SendChatToBuiltInAsync(
            "WebWorkflow",
            new A2AMessage 
            { 
                Text = "Fetch all URLs from example.com" 
            });
        
        // Use the response
        var urls = response.Text.Split(',');
    }
}
```

### Advanced API

For more control or when calling from outside workflows:

```csharp
// Get target workflow
var webWorkflow = XiansContext.GetBuiltInWorkflow("WebWorkflow");

// Create A2A client
var client = new A2AClient(webWorkflow);

// Send message
var response = await client.SendMessageAsync(new A2AMessage
{
    Text = "Process this request",
    Data = new { Key = "value" }
});
```

### Handling A2A Messages

Target workflows register handlers using the normal `OnUserChatMessage`:

```csharp
var webWorkflow = agent.Workflows.DefineBuiltIn(name: "WebWorkflow");

webWorkflow.OnUserChatMessage(async (context) =>
{
    // Cast to A2AMessageContext to access A2A-specific properties
    if (context is A2AMessageContext a2aContext)
    {
        Console.WriteLine($"Received from: {a2aContext.SourceAgentName}");
    }
    
    // Process the request
    var result = await ProcessWebRequest(context.Message.Text);
    
    // Send response back
    await context.Message.ReplyAsync(result);
});
```

## API Reference

### XiansContext.A2A Methods

#### Built-in Workflow Chat Messages
```csharp
// Send chat to built-in workflow (by name)
Task<A2AMessage> SendChatToBuiltInAsync(string builtInWorkflowName, A2AMessage message)

// Send chat to workflow instance
Task<A2AMessage> SendChatAsync(XiansWorkflow targetWorkflow, A2AMessage message)

// Send text-only chat message (convenience)
Task<A2AMessage> SendTextAsync(string builtInWorkflowName, string messageText)
```

#### Built-in Workflow Data Messages
```csharp
// Send data to built-in workflow (by name)
Task<A2AMessage> SendDataToBuiltInAsync(string builtInWorkflowName, A2AMessage message)

// Send data to workflow instance
Task<A2AMessage> SendDataAsync(XiansWorkflow targetWorkflow, A2AMessage message)
```

#### Custom Workflow Signal/Query/Update (New!)

For custom workflows, use Temporal's native primitives:

```csharp
// Send signal (fire-and-forget)
Task SendSignalAsync(XiansWorkflow targetWorkflow, string signalName, params object[] args)
Task SendSignalAsync(string workflowId, string signalName, params object[] args)

// Query workflow state (read-only)
Task<TResult> QueryAsync<TResult>(XiansWorkflow targetWorkflow, string queryName, params object[] args)
Task<TResult> QueryAsync<TResult>(string workflowId, string queryName, params object[] args)

// Update workflow (synchronous request-response)
Task<TResult> UpdateAsync<TResult>(XiansWorkflow targetWorkflow, string updateName, params object[] args)
Task<TResult> UpdateAsync<TResult>(string workflowId, string updateName, params object[] args)
```

See [A2A-CustomWorkflows.md](A2A-CustomWorkflows.md) for detailed usage and examples.

#### Graceful Error Handling
```csharp
// Try* methods return (Success, Response, ErrorMessage) tuple
Task<(bool, A2AMessage?, string?)> TrySendChatToBuiltInAsync(string builtInWorkflowName, A2AMessage message)
Task<(bool, A2AMessage?, string?)> TrySendChatAsync(XiansWorkflow targetWorkflow, A2AMessage message)
Task<(bool, A2AMessage?, string?)> TrySendDataToBuiltInAsync(string builtInWorkflowName, A2AMessage message)
Task<(bool, A2AMessage?, string?)> TrySendDataAsync(XiansWorkflow targetWorkflow, A2AMessage message)
```

### A2AMessage Structure

```csharp
public class A2AMessage
{
    public string Text { get; set; }           // Message text
    public object? Data { get; set; }          // Optional data payload
    public Dictionary<string, string>? Metadata { get; set; }  // Optional metadata
}
```

## Real-World Example

See `Xians.Examples/LeadDiscoveryAgent/ContentDiscovery/ContentDiscoveryWorkflow.cs` for a complete working example:

```csharp
private async Task<List<string>> FetchContentUrlsAsync(string contentSiteURL)
{
    // Send A2A chat message to web workflow using the simplified API
    var response = await XiansContext.A2A.SendChatToBuiltInAsync(
        Constants.WebWorkflowName,
        new A2AMessage 
        { 
            Text = $"Fetch all content article URLs from {contentSiteURL}..."
        });

    if (string.IsNullOrEmpty(response.Text))
    {
        throw new InvalidOperationException("No response from web agent");
    }

    return response.Text.Split(',').ToList();
}
```

## Benefits Over Alternative Approaches

1. **Synchronous** - Get response immediately vs. async signals
2. **Same Runtime** - No HTTP overhead, direct function calls
3. **Type-Safe** - Compile-time checking of workflow types
4. **Context-Aware** - Automatically handles workflow vs. activity context
5. **Simplified API** - `XiansContext.A2A` reduces boilerplate

## Context Requirements

A2A is designed to work in **Temporal contexts** (workflows and activities):

✅ **From Workflow Context**: Handler executes in isolated activity
✅ **From Activity Context**: Handler executes directly (no nested activities)
❌ **From Test/Script Context**: Not supported (no Temporal context available)

## Limitations

- **Same Runtime Only** - Both workflows must be running in the same process
- **Requires Temporal Context** - Must be called from within a workflow or activity
- **Synchronous Only** - Blocks until response received (use signals for async)

## Testing

A2A requires **Temporal context** (workflow or activity). Testing approaches:

### Integration Testing (Recommended)
Run actual workflows with Temporal workers:
```bash
# See Xians.Examples/LeadDiscoveryAgent for full example
dotnet run --project Xians.Examples/LeadDiscoveryAgent
```

### Unit Testing
A2A calls within workflows can be tested by:
1. Running the workflow with test workers
2. Mocking the target workflow handler
3. Using the LeadDiscoveryAgent pattern

### Why Tests Are Skipped
The `RealServerA2ATests.cs` tests are skipped because:
- They call A2A from plain test methods (no Temporal context)
- A2A requires `XiansContext` which is only available in workflows/activities
- To test properly, you need to run actual Temporal workers

**Working Example**: See `ContentDiscoveryWorkflow.cs` lines 146-149 for production A2A usage.

## Refactoring Summary

The A2A subsystem was refactored to:
- ✅ Use `ContextAwareActivityExecutor` pattern (eliminates manual branching)
- ✅ Eliminate code duplication (removed ~148 lines)
- ✅ Provide simplified `XiansContext.A2A` API
- ✅ Delete redundant classes (A2AActivityMessageCollection, A2AActivityMessageContext, A2AHelper)
- ✅ Consistent with other subsystems (Messaging, Documents, Knowledge)
