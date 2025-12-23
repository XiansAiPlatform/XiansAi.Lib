# Tenant Handling Design - Centralized Approach

## Overview

This document describes the centralized tenant handling design in Xians.Lib, which consolidates tenant-related logic into a single utility class for improved maintainability and consistency.

## Problem Statement

### Before Centralization

The tenant handling logic was scattered across multiple files:

1. **DefaultWorkflow.cs** (lines 182-269)
   - WorkflowId parsing: `workflowId.Split(':')`
   - Tenant extraction logic duplicated
   - System-scoped vs non-system-scoped validation mixed with workflow logic

2. **XiansWorkflow.cs** (lines 100-114)
   - Task queue naming logic: `if (systemScoped) { ... } else { ... }`
   - Duplicate tenant ID retrieval

3. **UserMessageContext.cs** (lines 20, 57)
   - Tenant ID property management

4. **MessageActivities.cs** (lines 180, 392, 431)
   - X-Tenant-Id header handling in multiple places

### Issues

- **Code Duplication**: Workflow ID parsing logic repeated 4+ times
- **Inconsistent Error Handling**: Different error messages and logging
- **Hard to Maintain**: Changes require updating multiple files
- **Testing Complexity**: No single place to test tenant logic
- **Unclear Separation of Concerns**: Business logic mixed with infrastructure concerns

## Solution: TenantContext Utility

### New Class: `Common/TenantContext.cs`

A static utility class that centralizes all tenant-related operations:

```csharp
public static class TenantContext
{
    // Extract tenant ID from workflow ID
    public static string ExtractTenantId(string workflowId)
    
    // Extract workflow type from workflow ID
    public static string ExtractWorkflowType(string workflowId)
    
    // Generate task queue name based on system scope
    public static string GetTaskQueueName(string workflowType, bool systemScoped, string? tenantId)
    
    // Validate tenant isolation
    public static bool ValidateTenantIsolation(string workflowTenantId, string? expectedTenantId, bool systemScoped, ILogger? logger)
    
    // Parse workflow ID into components
    public static WorkflowIdentifier Parse(string workflowId)
}
```

### Benefits

1. **Single Source of Truth**
   - All tenant parsing logic in one place
   - Consistent behavior across the library
   - Easier to update and maintain

2. **DRY Principle**
   - No duplicate code
   - Reduces bugs from inconsistent implementations

3. **Better Testing**
   - Easy to unit test all tenant logic
   - Test edge cases once, applies everywhere

4. **Improved Readability**
   - Clear intent: `TenantContext.ExtractTenantId(workflowId)`
   - Self-documenting code

5. **Consistent Error Handling**
   - Uniform error messages
   - Single place to improve error handling

6. **Alignment with XiansAi.Lib.Src**
   - Matches the pattern from `WorkflowIdentifier.cs`
   - Consistent architecture across both libraries

## Implementation

### 1. Workflow ID Parsing

**Before:**
```csharp
// In DefaultWorkflow.cs
var workflowIdParts = workflowId.Split(':');
if (workflowIdParts.Length < 2)
{
    Workflow.Logger.LogError("Invalid WorkflowId format...");
    return;
}
var workflowTenantId = workflowIdParts[0];
```

**After:**
```csharp
// In DefaultWorkflow.cs
try
{
    var workflowTenantId = TenantContext.ExtractTenantId(workflowId);
}
catch (InvalidOperationException ex)
{
    Workflow.Logger.LogError(ex, "Failed to extract tenant ID...");
    return;
}
```

### 2. Tenant Validation

**Before:**
```csharp
// In DefaultWorkflow.cs (40+ lines of if-else logic)
if (metadata.SystemScoped)
{
    Workflow.Logger.LogDebug(...);
}
else
{
    if (metadata.TenantId != workflowTenantId)
    {
        Workflow.Logger.LogError(...);
        await SendSimpleMessageAsync(...);
        return;
    }
    Workflow.Logger.LogDebug(...);
}
```

**After:**
```csharp
// In DefaultWorkflow.cs (clean and concise)
if (!TenantContext.ValidateTenantIsolation(
    workflowTenantId, 
    metadata.TenantId, 
    metadata.SystemScoped,
    Workflow.Logger))
{
    await SendSimpleMessageAsync(...);
    return;
}
```

### 3. Task Queue Naming

**Before:**
```csharp
// In XiansWorkflow.cs
string taskQueue;
if (_agent.SystemScoped)
{
    taskQueue = WorkflowType;
}
else
{
    var tenantId = _agent.Options?.TenantId ?? 
        throw new InvalidOperationException(...);
    taskQueue = $"{tenantId}:{WorkflowType}";
}
```

**After:**
```csharp
// In XiansWorkflow.cs
string? tenantId = null;
if (!_agent.SystemScoped)
{
    tenantId = _agent.Options?.TenantId ?? 
        throw new InvalidOperationException(...);
}

var taskQueue = TenantContext.GetTaskQueueName(
    WorkflowType, 
    _agent.SystemScoped, 
    tenantId);
```

## Workflow ID Format

The library uses a consistent format across both system-scoped and non-system-scoped workflows:

```
Format: {TenantId}:{WorkflowType}:{OptionalSuffix}

Examples:
  - "acme-corp:CustomerService:Default Workflow:uuid-123"
  - "contoso:GlobalNotifications:Alerts:uuid-456"
  - "fabrikam:BillingService:Invoices:uuid-789"
```

### Parsing Logic

```csharp
var identifier = TenantContext.Parse(workflowId);
// identifier.TenantId = "acme-corp"
// identifier.WorkflowType = "CustomerService"
// identifier.WorkflowId = "acme-corp:CustomerService:Default Workflow:uuid-123"
```

## Task Queue Naming

Task queue names differ based on system scope:

### Non-System-Scoped (Tenant Isolated)
```
Format: {TenantId}:{WorkflowType}
Example: "acme-corp:CustomerService:Default Workflow"

Worker: Listens only on "acme-corp:CustomerService:Default Workflow"
Isolation: Only processes workflows from "acme-corp" tenant
```

### System-Scoped (Multi-Tenant)
```
Format: {WorkflowType}
Example: "GlobalNotifications:Default Workflow"

Worker: Listens on "GlobalNotifications:Default Workflow"
Multi-Tenant: Processes workflows from ANY tenant
```

## Tenant Validation Rules

### System-Scoped Agents
- ✅ Extract tenant from WorkflowId
- ✅ Pass tenant context to handlers
- ❌ **Do NOT validate** tenant against registered tenant
- ✅ Can handle multiple tenants simultaneously

### Non-System-Scoped Agents
- ✅ Extract tenant from WorkflowId
- ✅ **Validate** tenant matches registered tenant
- ❌ **Reject** messages from other tenants
- ✅ Pass tenant context to handlers

## Testing

### Unit Tests

The `TenantContext` utility should be tested for:

1. **Valid WorkflowId Parsing**
   - Correct tenant extraction
   - Correct workflow type extraction
   - Handling of optional suffixes

2. **Invalid WorkflowId Handling**
   - Null/empty WorkflowId
   - Missing colons
   - Malformed format

3. **Task Queue Naming**
   - System-scoped: returns WorkflowType
   - Non-system-scoped: returns TenantId:WorkflowType
   - Error when tenantId missing for non-system-scoped

4. **Tenant Validation**
   - System-scoped: always passes
   - Non-system-scoped: validates match
   - Non-system-scoped: rejects mismatch

### Integration Tests

Test the library with:
- Multiple tenants simultaneously
- System-scoped and non-system-scoped agents
- Tenant isolation verification
- Cross-tenant message rejection

## Migration Guide

### For Library Maintainers

1. **Use TenantContext for all tenant operations**
   ```csharp
   // Old: var parts = workflowId.Split(':'); var tenant = parts[0];
   // New: var tenant = TenantContext.ExtractTenantId(workflowId);
   ```

2. **Use centralized validation**
   ```csharp
   // Old: if (metadata.SystemScoped) { ... } else { ... }
   // New: TenantContext.ValidateTenantIsolation(...)
   ```

3. **Use centralized task queue naming**
   ```csharp
   // Old: taskQueue = systemScoped ? workflowType : $"{tenantId}:{workflowType}"
   // New: TenantContext.GetTaskQueueName(workflowType, systemScoped, tenantId)
   ```

### For Library Users

No changes required! The API remains the same:
- `XiansAgentRegistration.SystemScoped` works as before
- `UserMessageContext.TenantId` works as before
- Task queue routing is automatic

## Future Enhancements

1. **Add More Helper Methods**
   - `IsValidWorkflowId(string workflowId)`: Validate format
   - `BuildWorkflowId(string tenantId, string workflowType, string? suffix)`: Construct WorkflowId
   - `GetAgentName(string workflowType)`: Extract agent name from workflow type

2. **Performance Optimization**
   - Cache parsed WorkflowId components
   - Reduce string allocations

3. **Enhanced Error Messages**
   - Include suggestions for fixing invalid formats
   - Reference documentation

4. **Telemetry**
   - Track tenant usage metrics
   - Monitor isolation violations
   - Alert on parsing errors

## Related Documentation

- [System-Scoped Agents](SystemScopedAgents.md) - Multi-tenant agent design
- [Worker Registration](WorkerRegistration.md) - How workers are configured
- [Configuration](Configuration.md) - Platform configuration options

## Comparison with XiansAi.Lib.Src

The `TenantContext` utility in Xians.Lib is inspired by `WorkflowIdentifier` in XiansAi.Lib.Src:

### Similarities
- Centralized WorkflowId parsing
- Tenant extraction from WorkflowId
- WorkflowType extraction
- Static utility class pattern

### Differences
- **Validation**: Xians.Lib adds `ValidateTenantIsolation` method
- **Task Queue Naming**: Xians.Lib adds `GetTaskQueueName` method
- **Logging Integration**: Xians.Lib accepts ILogger for validation
- **Error Handling**: Xians.Lib uses exceptions for invalid formats

Both libraries follow the same architectural principle: **centralize tenant handling logic for consistency and maintainability**.

## Summary

The centralized `TenantContext` utility provides:

1. ✅ **Single source of truth** for tenant operations
2. ✅ **Reduced code duplication** (40+ lines removed)
3. ✅ **Consistent error handling** across the library
4. ✅ **Easier testing** with isolated unit tests
5. ✅ **Better maintainability** for future changes
6. ✅ **Alignment with XiansAi.Lib.Src** patterns
7. ✅ **No breaking changes** for existing users

This design follows SOLID principles and makes the codebase more maintainable while preserving the existing functionality.


