# Logger<T> Wrapper Guide

## Overview

The `Logger<T>` wrapper provides a simplified, context-aware logging API that automatically captures workflow context and routes logs to the appropriate logger (Workflow.Logger or standard logger).

This is the recommended way to add logging to your workflows, activities, and agents.

## Key Features

✅ **Automatic Context Capture** - Workflow ID, Agent, Tenant, etc. automatically added  
✅ **Smart Routing** - Uses `Workflow.Logger` inside workflows, standard logger elsewhere  
✅ **Cached Instances** - Logger instances are created once and reused  
✅ **Simple API** - Just call `LogInformation`, `LogError`, etc.  
✅ **API Logging** - Automatically sends logs to server via `ApiLoggerProvider`  
✅ **Type-Safe** - Generic type parameter provides clear logger categories  

---

## Quick Start

### **Option 1: Using Logger<T> Directly (Recommended)**

```csharp
using Xians.Lib.Logging;

public class MyAgent
{
    private static readonly Logger<MyAgent> _logger = Logger<MyAgent>.For();
    
    public async Task ProcessAsync(string data)
    {
        _logger.LogInformation("Processing started");
        
        try
        {
            // Your logic here
            _logger.LogDebug("Processing data: {Data}", data);
            
            // Workflow context is automatically captured!
            // No need to manually add WorkflowId, Agent, etc.
        }
        catch (Exception ex)
        {
            _logger.LogError("Processing failed", ex);
            throw;
        }
    }
}
```

### **Option 2: Using XiansLogger.For<T>() Helper**

```csharp
using Xians.Lib.Agents.Core;

public class MyWorkflow
{
    private readonly Xians.Lib.Logging.Logger<MyWorkflow> _logger = XiansLogger.For<MyWorkflow>();
    
    public void DoWork()
    {
        _logger.LogInformation("Workflow started");
        // Context automatically captured!
    }
}
```

---

## API Reference

### Logger<T> Methods

| Method | Description | Example |
|--------|-------------|---------|
| `For()` | Get cached logger for type T | `Logger<MyClass>.For()` |
| `For<T>()` | Get cached logger for specified type | `Logger<MyClass>.For<OtherClass>()` |
| `LogTrace(message)` | Log trace message | `logger.LogTrace("Details")` |
| `LogDebug(message)` | Log debug message | `logger.LogDebug("Debug info")` |
| `LogInformation(message)` | Log information | `logger.LogInformation("Started")` |
| `LogWarning(message)` | Log warning | `logger.LogWarning("Warning")` |
| `LogError(message, ex?)` | Log error with optional exception | `logger.LogError("Failed", ex)` |
| `LogCritical(message, ex?)` | Log critical with optional exception | `logger.LogCritical("Fatal", ex)` |

---

## Usage Patterns

### **Pattern 1: Static Field (Recommended)**

Best for classes that log frequently:

```csharp
public class MyService
{
    private static readonly Logger<MyService> _logger = Logger<MyService>.For();
    
    public void Method1()
    {
        _logger.LogInformation("Method1 called");
    }
    
    public void Method2()
    {
        _logger.LogInformation("Method2 called");
    }
}
```

**Benefits:**
- ✅ Logger created once, reused across all instances
- ✅ Fast - no allocation overhead
- ✅ Clean - stored at class level

### **Pattern 2: Property**

For classes with occasional logging:

```csharp
public class MyActivity
{
    private Xians.Lib.Logging.Logger<MyActivity> Logger => Logger<MyActivity>.For();
    
    public async Task ExecuteAsync()
    {
        Logger.LogInformation("Activity executing");
    }
}
```

### **Pattern 3: Local Variable**

For one-off logging:

```csharp
public void ProcessOrder(Order order)
{
    var logger = Logger<OrderProcessor>.For();
    logger.LogInformation("Processing order {OrderId}", order.Id);
}
```

---

## Context Capture

The logger automatically captures workflow context when available:

### Inside Workflow or Activity

```csharp
[Workflow]
public class MyWorkflow
{
    private static readonly Logger<MyWorkflow> _logger = Logger<MyWorkflow>.For();
    
    [WorkflowRun]
    public async Task RunAsync()
    {
        // This log will have:
        // - WorkflowId: "tenant-123:MyAgent:MyWorkflow:user-456"
        // - Agent: "MyAgent"
        // - WorkflowType: "MyAgent:MyWorkflow"
        // - WorkflowRunId: "abc-123-def-456"
        // - ParticipantId: "user-456"
        _logger.LogInformation("Workflow started");
    }
}
```

**Resulting Log Entry:**
```json
{
  "workflow_id": "tenant-123:MyAgent:MyWorkflow:user-456",
  "workflow_run_id": "abc-123-def-456",
  "workflow_type": "MyAgent:MyWorkflow",
  "agent": "MyAgent",
  "participant_id": "user-456",
  "level": "Information",
  "message": "Workflow started",
  "created_at": "2026-01-09T10:30:00Z"
}
```

### Outside Workflow (Startup, etc.)

```csharp
public class Program
{
    private static readonly Logger<Program> _logger = Logger<Program>.For();
    
    public static void Main()
    {
        // This log will have:
        // - WorkflowId: "Outside Workflows"
        // - Agent: "No Agent Available"
        _logger.LogInformation("Application starting");
    }
}
```

---

## Comparison with Other Logging Approaches

### ❌ Standard ILogger (Manual Context)

```csharp
private readonly ILogger<MyClass> _logger;

public void Method()
{
    // Must manually create scope for context
    var contextData = new Dictionary<string, object>
    {
        ["WorkflowId"] = XiansContext.SafeWorkflowId ?? "Unknown",
        ["Agent"] = XiansContext.SafeAgentName ?? "Unknown"
    };
    
    using (_logger.BeginScope(contextData))
    {
        _logger.LogInformation("Message");
    }
}
```

**Problems:**
- ❌ Boilerplate code for every log
- ❌ Easy to forget context
- ❌ Inconsistent context across logs

### ✅ Logger<T> (Automatic Context)

```csharp
private static readonly Logger<MyClass> _logger = Logger<MyClass>.For();

public void Method()
{
    // Context automatically captured!
    _logger.LogInformation("Message");
}
```

**Benefits:**
- ✅ No boilerplate
- ✅ Consistent context
- ✅ Less code

---

## Integration with Temporal

### Workflow Logging

Inside Temporal workflows, `Logger<T>` automatically delegates to `Workflow.Logger`:

```csharp
[Workflow]
public class MyWorkflow
{
    private static readonly Logger<MyWorkflow> _logger = Logger<MyWorkflow>.For();
    
    [WorkflowRun]
    public async Task RunAsync()
    {
        // Internally uses Workflow.Logger (required by Temporal)
        _logger.LogInformation("Workflow started");
        
        // With context scope automatically created
    }
}
```

### Activity Logging

In activities, `Logger<T>` uses standard logger with context:

```csharp
public class MyActivity
{
    private static readonly Logger<MyActivity> _logger = Logger<MyActivity>.For();
    
    [Activity]
    public async Task ExecuteAsync()
    {
        // Uses standard logger with workflow context
        _logger.LogInformation("Activity executing");
    }
}
```

---

## Advanced Usage

### Custom Logger Categories

Use different types for different logger categories:

```csharp
// Order processing logs
private static readonly Logger<OrderProcessor> _orderLogger = Logger<OrderProcessor>.For();

// Payment processing logs
private static readonly Logger<PaymentProcessor> _paymentLogger = Logger<PaymentProcessor>.For();

public void ProcessOrder(Order order)
{
    _orderLogger.LogInformation("Processing order {OrderId}", order.Id);
    
    if (order.RequiresPayment)
    {
        _paymentLogger.LogInformation("Processing payment for order {OrderId}", order.Id);
    }
}
```

### Exception Logging

```csharp
try
{
    await ProcessAsync(data);
}
catch (ValidationException ex)
{
    // Exception details automatically captured
    _logger.LogError("Validation failed for {Data}", ex, data);
    throw;
}
catch (Exception ex)
{
    // Critical errors
    _logger.LogCritical("Unexpected error processing {Data}", ex, data);
    throw;
}
```

---

## Configuration

The `Logger<T>` respects environment variable configuration:

### Environment Variables

```bash
# Console output level
CONSOLE_LOG_LEVEL=DEBUG

# API logging level (sent to server)
API_LOG_LEVEL=ERROR
```

### Behavior

- **Console**: Shows logs at `CONSOLE_LOG_LEVEL` or above
- **API**: Sends logs at `API_LOG_LEVEL` or above to the server

Example with `CONSOLE_LOG_LEVEL=INFO` and `API_LOG_LEVEL=ERROR`:

```csharp
_logger.LogDebug("Debug info");        // Not shown in console, not sent to API
_logger.LogInformation("Info");         // Shown in console, not sent to API
_logger.LogError("Error occurred");     // Shown in console, sent to API
```

---

## Migration from v2

If you're migrating from v2's `Logger<T>`, the API is mostly compatible:

### v2 Code

```csharp
using XiansAi.Logging;

private static readonly Logger<MyClass> _logger = Logger<MyClass>.For();

_logger.LogInformation("Message");
```

### v3 Code (Same!)

```csharp
using Xians.Lib.Logging;

private static readonly Logger<MyClass> _logger = Logger<MyClass>.For();

_logger.LogInformation("Message");
```

**Key Differences:**
1. ✅ Namespace changed from `XiansAi.Logging` to `Xians.Lib.Logging`
2. ✅ Context now captured via `XiansContext` (not `AgentContext`)
3. ✅ More efficient caching and context extraction
4. ✅ Better integration with v3's dependency injection

---

## Best Practices

### ✅ DO

- Use `Logger<T>.For()` in a static field for frequently-used loggers
- Use the logger's type parameter to match your class name
- Log at appropriate levels (Debug for details, Information for milestones, Error for failures)
- Include relevant data in log messages using structured logging
- Let the logger handle context automatically

### ❌ DON'T

- Create new logger instances in hot paths (use cached instances)
- Manually add workflow context (it's automatic)
- Use string concatenation for log messages (use structured logging)
- Log sensitive data (passwords, API keys, PII)
- Over-log (respect log levels)

---

## Troubleshooting

### Problem: Logs not showing in console

**Check:**
```csharp
// Verify environment variable
var consoleLevel = Environment.GetEnvironmentVariable("CONSOLE_LOG_LEVEL");
Console.WriteLine($"Console Log Level: {consoleLevel ?? "NOT SET (defaults to DEBUG)"}");
```

**Solution:** Set `CONSOLE_LOG_LEVEL=DEBUG` in your `.env` file

### Problem: Logs not sent to API

**Check:**
```csharp
// Verify environment variable
var apiLevel = Environment.GetEnvironmentVariable("API_LOG_LEVEL");
Console.WriteLine($"API Log Level: {apiLevel ?? "NOT SET (defaults to ERROR)"}");

// Verify LoggingServices is initialized
LoggingServices.Initialize(httpClientService);
```

**Solution:** 
1. Set `API_LOG_LEVEL=INFO` to send more logs
2. Ensure `LoggingServices.Initialize()` is called at startup

### Problem: Context shows "Outside Workflows"

**This is expected** if:
- ✅ Logging during application startup
- ✅ Logging outside of Temporal workflow/activity execution

**This is a problem** if:
- ❌ Logging inside `OnUserChatMessage` handler shows "Outside Workflows"
- ❌ Logging inside `[WorkflowRun]` method shows "Outside Workflows"

**Solution:** Debug using the [DEBUG_GUIDE.md](../../MyAgents/AgentLogTestor/DEBUG_GUIDE.md) to check `Workflow.InWorkflow`

---

## Examples

See [LoggingUsageExample.cs](Examples/LoggingUsageExample.cs) for complete examples of:
- Basic logging
- Workflow logging
- Activity logging
- Error handling
- Context capture
- API logging

---

## Summary

The `Logger<T>` wrapper provides the easiest way to add comprehensive logging to your Xians agents:

```csharp
// 1. Add to your class
private static readonly Logger<MyClass> _logger = Logger<MyClass>.For();

// 2. Use it
_logger.LogInformation("Processing started");
_logger.LogError("Failed", exception);

// 3. That's it! Context is automatic, logs go to console and API.
```

No configuration needed beyond environment variables. Context is captured automatically. Logs are routed correctly based on execution context.

**Simple. Powerful. Context-aware.** 🎯
