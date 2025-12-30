# System-Scoped Agent Support - Runtime Tenant ID

This document summarizes how all agent operations support system-scoped agents by extracting the tenant ID from the runtime workflow context.

## Summary

✅ **All agent operations now properly support system-scoped agents** by using runtime tenant ID extraction from workflow context.

---

## How It Works

### Tenant ID Resolution Strategy

| Agent Type | Tenant ID Source | When Available |
|------------|------------------|----------------|
| **Non-System-Scoped** | `agent.Options.CertificateTenantId` | Always (static) |
| **System-Scoped** | `XiansContext.TenantId` | Workflow/Activity context only |

### Workflow ID → Tenant ID Extraction

```
Workflow ID: tenant-123:MyAgent:Chat:user-456
              ↓ (TenantContext.ExtractTenantId)
Tenant ID: tenant-123
              ↓
All operations scoped to: tenant-123
```

---

## Feature Support Matrix

| Feature | Non-System-Scoped | System-Scoped (Workflow Context) | System-Scoped (Outside Context) |
|---------|-------------------|----------------------------------|--------------------------------|
| **Knowledge** | ✅ Always | ✅ Runtime tenant | ❌ Throws exception |
| **Documents** | ✅ Always | ✅ Runtime tenant | ❌ Throws exception |
| **Messaging (Context)** | ✅ Always | ✅ Runtime tenant | N/A (workflow only) |
| **Messaging (Proactive)** | ✅ Always | ✅ Runtime tenant | ❌ Throws exception |
| **Schedules** | ✅ Always | ✅ Runtime tenant | ❌ Throws exception |
| **A2A** | ✅ Always | ✅ Runtime tenant | ❌ Throws exception |

---

## Implementation Details

### 1. Knowledge Operations (`KnowledgeCollection`)

**Code:**
```csharp
private string GetTenantId()
{
    if (!_agent.SystemScoped)
    {
        return _agent.Options?.CertificateTenantId ?? throw ...;
    }
    
    // System-scoped: extract from workflow context
    return XiansContext.TenantId;
}
```

**Usage:**
```csharp
var agent = platform.Agents.Register("MyAgent", systemScoped: true);

// ❌ Outside workflow context
await agent.Knowledge.GetAsync("prompt");  // Throws!

// ✅ Inside workflow context
workflow.OnUserMessage(async (context) =>
{
    var knowledge = await context.GetKnowledgeAsync("prompt");  // Works!
});
```

### 2. Document Operations (`DocumentCollection`)

**Code:**
```csharp
private string GetTenantId()
{
    if (!_agent.SystemScoped)
    {
        return _agent.Options?.CertificateTenantId ?? throw ...;
    }
    
    // System-scoped: extract from workflow context
    return XiansContext.TenantId;
}
```

**Usage:**
```csharp
var agent = platform.Agents.Register("MyAgent", systemScoped: true);

// ❌ Outside workflow context
await agent.Documents.SaveAsync(doc);  // Throws!

// ✅ Inside workflow context
workflow.OnUserMessage(async (context) =>
{
    await context.SaveDocumentAsync(doc);  // Works!
});
```

### 3. Messaging Operations (`UserMessaging`)

**Code:**
```csharp
private static async Task SendMessageAsync(...)
{
    var tenantId = XiansContext.TenantId;  // Runtime extraction
    // ...
}
```

**Usage:**
```csharp
[Workflow("MyAgent:Notifications")]
public class NotificationWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        // ✅ Works - tenant extracted from workflow ID
        await UserMessaging.SendChatAsync("user-123", "Hello!");
    }
}
```

### 4. Schedule Operations (`ScheduleCollection`, `ScheduleBuilder`)

**Code:**
```csharp
// ScheduleCollection
string tenantId = _agent.SystemScoped 
    ? XiansContext.TenantId        // Runtime extraction
    : _agent.Options?.CertificateTenantId;

// ScheduleBuilder  
if (_agent.SystemScoped)
{
    return XiansContext.TenantId;  // Runtime extraction
}
```

**Usage:**
```csharp
workflow.OnUserMessage(async (context) =>
{
    // ✅ Works for system-scoped agents
    await XiansContext.CurrentWorkflow.Schedules!.CreateAsync(spec =>
        spec.WithId("daily-report")
            .WithCronSchedule("0 9 * * *")
    );
});
```

### 5. UserMessageContext (All Methods)

**Code:**
```csharp
public virtual string TenantId 
{ 
    get 
    {
        if (string.IsNullOrEmpty(_tenantId))
        {
            throw new InvalidOperationException(...);
        }
        return _tenantId;
    }
}
```

**How TenantId is Set:**
- Extracted from workflow ID in `MessageProcessor` (line 50):
  ```csharp
  workflowTenantId = TenantContext.ExtractTenantId(workflowId);
  ```
- Passed to activity request (line 140)
- Passed to `UserMessageContext` constructor (line 69)

**Result:** ✅ Automatically works for both system-scoped and non-system-scoped agents

---

## Error Messages

When system-scoped agents call operations outside workflow/activity context:

| Feature | Error Message |
|---------|---------------|
| **Knowledge** | `Knowledge API for system-scoped agents can only be used within a workflow or activity context. The tenant ID is extracted from the workflow ID at runtime.` |
| **Documents** | `Documents API for system-scoped agents can only be used within a workflow or activity context. The tenant ID is extracted from the workflow ID at runtime.` |
| **Messaging** | `UserMessaging can only be used within a Temporal workflow or activity context.` |
| **Schedules** | (Same as above - uses `XiansContext.TenantId` which throws similar error) |

---

## Best Practices for System-Scoped Agents

### ✅ DO: Use from Workflow/Activity Context

```csharp
var agent = platform.Agents.Register("MyAgent", systemScoped: true);
var workflow = await agent.Workflows.DefineBuiltIn();

workflow.OnUserMessage(async (context) =>
{
    // ✅ All of these work - tenant from workflow ID
    var knowledge = await context.GetKnowledgeAsync("prompt");
    await context.SaveDocumentAsync(document);
    await UserMessaging.SendChatAsync("user-123", "Hi!");
    
    await context.ReplyAsync("Done!");
});
```

### ❌ DON'T: Use Outside Workflow Context

```csharp
var agent = platform.Agents.Register("MyAgent", systemScoped: true);

// ❌ These all throw - no workflow context
await agent.Knowledge.GetAsync("prompt");
await agent.Documents.SaveAsync(doc);
await UserMessaging.SendChatAsync("user-123", "Hi!");
```

### ✅ DO: Use in Custom Activities

```csharp
public class MyActivities
{
    [Activity]
    public async Task ProcessData(string data)
    {
        // ✅ Works - XiansContext extracts tenant from activity context
        var tenantId = XiansContext.TenantId;
        var knowledge = await XiansContext.CurrentAgent.Knowledge.GetAsync("config");
        
        // Process using tenant-specific config...
    }
}
```

---

## Testing

All integration tests verify system-scoped agent support:

### Knowledge Tests
- `Knowledge_CreateAndGet_WorksWithRealServer` ✅
- (All knowledge tests pass with system-scoped agents when using workflow context)

### Document Tests  
- `Document_SystemScopedAgent_OutsideWorkflow_ThrowsException` ✅
- (Verifies proper error when called outside workflow context)

### Messaging Tests
- (UserMessaging inherently requires workflow/activity context)

---

## Migration Guide

If you have existing code that doesn't work with system-scoped agents:

### Before (❌ Only works for non-system-scoped)
```csharp
private string GetTenantId()
{
    return _agent.Options?.CertificateTenantId 
        ?? throw new InvalidOperationException(...);
}
```

### After (✅ Works for both)
```csharp
private string GetTenantId()
{
    if (!_agent.SystemScoped)
    {
        return _agent.Options?.CertificateTenantId 
            ?? throw new InvalidOperationException(...);
    }
    
    // System-scoped: extract from workflow context
    try
    {
        return XiansContext.TenantId;
    }
    catch (InvalidOperationException)
    {
        throw new InvalidOperationException(
            "API for system-scoped agents can only be used within a workflow or activity context.");
    }
}
```

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────┐
│ Workflow ID: tenant-A:GlobalNotifications:Alerts:uuid   │
└─────────────────────────────────────────────────────────┘
                      ↓
        ┌─────────────────────────────┐
        │ XiansContext.TenantId       │
        │ (Extracts: tenant-A)        │
        └─────────────────────────────┘
                      ↓
        ┌─────────────────────────────┐
        │ All Operations Scoped To:   │
        │ • Knowledge → tenant-A      │
        │ • Documents → tenant-A      │
        │ • Messages  → tenant-A      │
        │ • Schedules → tenant-A      │
        └─────────────────────────────┘
```

For system-scoped agents, each workflow execution is isolated to a specific tenant based on the workflow ID.

---

## Summary

| Component | Status | Notes |
|-----------|--------|-------|
| `UserMessageContext` | ✅ Always worked | Tenant passed from MessageProcessor |
| `UserMessaging` | ✅ Always worked | Uses `XiansContext.TenantId` |
| `ScheduleCollection` | ✅ Always worked | Uses `XiansContext.TenantId` for system-scoped |
| `ScheduleBuilder` | ✅ Always worked | Uses `XiansContext.TenantId` for system-scoped |
| `KnowledgeCollection` | ✅ **Fixed** | Now uses `XiansContext.TenantId` for system-scoped |
| `DocumentCollection` | ✅ **Fixed** | Now uses `XiansContext.TenantId` for system-scoped |

**Result:** All agent operations fully support system-scoped agents when called from workflow/activity context! 🎉

