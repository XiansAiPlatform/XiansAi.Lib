# System-Scoped Agents

This document explains how system-scoped agents work in Xians.Lib and how they handle multi-tenant scenarios with proper tenant isolation.

## Overview

System-scoped agents are designed to handle requests from **multiple tenants** through a single set of workers, while non-system-scoped agents are **tenant-isolated** and only process requests from their registered tenant.

### Key Differences

| Aspect | System-Scoped | Non-System-Scoped |
|--------|---------------|-------------------|
| **Purpose** | Multi-tenant services | Single-tenant agents |
| **Worker Queue** | Shared across tenants | Isolated per tenant |
| **Tenant Context** | Extracted from WorkflowId | Validated against registered tenant |
| **Use Cases** | Global services, admin tools, shared infrastructure | Customer-specific agents, isolated workflows |
| **Example** | Notification service, billing system, analytics | Customer support bot, tenant-specific workflows |

## Architecture

### Task Queue Naming

```
Non-System-Scoped: {TenantId}:{WorkflowType}
Example: "acme-corp:CustomerService : Default Workflow"

System-Scoped: {WorkflowType}
Example: "GlobalNotifications : Default Workflow"
```

### Workflow ID Format

**Both system-scoped and non-system-scoped workflows use the same WorkflowId format:**

```
Format: {TenantId}:{WorkflowType}:{OptionalSuffix}

Examples:
  - "acme-corp:GlobalNotifications:Alerts:uuid-123"
  - "contoso:GlobalNotifications:Alerts:uuid-456"
  - "fabrikam:CustomerService:Support:uuid-789"
```

**Critical:** Even system-scoped workflows have tenant context in their WorkflowId. This is how the workflow knows which tenant initiated it.

### Tenant Context Flow

```
1. Client creates workflow with TenantId in WorkflowId
   ├─> "acme-corp:GlobalNotifications:Alerts:uuid-123"
   
2. Server routes to appropriate task queue
   ├─> System-scoped: Queue "GlobalNotifications"
   └─> Non-system-scoped: Queue "acme-corp:CustomerService"
   
3. Worker picks up workflow from queue

4. Workflow extracts tenant from WorkflowId
   ├─> Split by ':' → ["acme-corp", "GlobalNotifications", "Alerts", "uuid-123"]
   └─> TenantId = "acme-corp"
   
5. Tenant context passed to handler
   └─> context.TenantId = "acme-corp"
```

## Tenant Isolation

### System-Scoped Agents

System-scoped agents:
- ✅ **Extract** tenant from WorkflowId
- ✅ **Pass** tenant context to handlers via `context.TenantId`
- ✅ **Include** `X-Tenant-Id` header in API requests (automatic)
- ❌ **Do NOT validate** tenant against a registered tenant
- ✅ **Can handle** multiple tenants simultaneously

**Critical Implementation Detail:**  
When a system-scoped agent sends a reply, the tenant context from the workflow's `WorkflowId` is automatically added as an `X-Tenant-Id` HTTP header. This ensures that replies are routed to the correct tenant that initiated the workflow, not the tenant from the agent's API key.

```csharp
// System-scoped workflow processing
if (metadata.SystemScoped)
{
    // Extract tenant from WorkflowId and log
    Workflow.Logger.LogDebug(
        "System-scoped workflow: Tenant={Tenant}, Type={Type}",
        workflowTenantId, workflowType);
    
    // NO validation - multi-tenant by design
    // Tenant context passed to handler
}
```

### Non-System-Scoped Agents

Non-system-scoped agents:
- ✅ **Extract** tenant from WorkflowId
- ✅ **Validate** tenant matches registered tenant
- ✅ **Reject** messages from other tenants
- ✅ **Pass** tenant context to handlers via `context.TenantId`
- ✅ **Include** `X-Tenant-Id` header in API requests (automatic)

**Note:** For non-system-scoped agents, the tenant from the WorkflowId always matches the agent's registered tenant (validated at runtime).

```csharp
// Non-system-scoped workflow processing
if (!metadata.SystemScoped)
{
    // Extract and validate tenant
    if (metadata.TenantId != workflowTenantId)
    {
        Workflow.Logger.LogError(
            "Tenant isolation violation: Expected={Expected}, Got={Got}",
            metadata.TenantId, workflowTenantId);
        
        await context.ReplyAsync("Error: Tenant isolation violation.");
        return; // REJECT
    }
}
```

## Usage Examples

### System-Scoped Agent Example

```csharp
using Xians.Lib.Agents;

// Initialize platform
var platform = await XiansPlatform.InitializeAsync(new XiansOptions
{
    ServerUrl = "https://api.example.com",
    ApiKey = apiKey
});

// Register system-scoped agent
var agent = platform.Agents.Register(new XiansAgentRegistration
{
    Name = "GlobalNotificationService",
    SystemScoped = true  // Multi-tenant
});

// Define workflow
var workflow = await agent.Workflows.DefineBuiltIn(
    name: "Alerts",
    workers: 3  // Handle multiple tenants
);

// Register handler with tenant-aware logic
workflow.OnUserMessage(async (context) =>
{
    // Tenant context available via context.TenantId
    var tenant = context.TenantId;  // e.g., "acme-corp"
    
    Console.WriteLine($"Processing alert for tenant: {tenant}");
    
    // Load tenant-specific settings
    var settings = await GetTenantSettings(tenant);
    
    // Send notification with tenant's preferences
    await SendNotification(
        tenant: tenant,
        message: context.Message.Text,
        settings: settings
    );
    
    await context.ReplyAsync($"Alert sent for tenant {tenant}");
});

// Run agent
await agent.RunAllAsync();

// Worker listens on: "GlobalNotificationService : Default Workflow - Alerts"
// Can handle workflows from ANY tenant:
//   - "acme-corp:GlobalNotificationService:...:uuid1"
//   - "contoso:GlobalNotificationService:...:uuid2"
//   - "fabrikam:GlobalNotificationService:...:uuid3"
```

### Non-System-Scoped Agent Example

```csharp
// Register tenant-isolated agent
var agent = platform.Agents.Register(new XiansAgentRegistration
{
    Name = "CustomerSupport",
    SystemScoped = false  // Single-tenant
});

// Define workflow
var workflow = await agent.Workflows.DefineBuiltIn(
    name: "Tickets",
    workers: 2
);

// Register handler
workflow.OnUserMessage(async (context) =>
{
    // context.TenantId is always the agent's registered tenant
    var tenant = context.TenantId;  // Always same tenant
    
    await HandleSupportTicket(context.Message.Text);
    await context.ReplyAsync("Ticket created");
});

// Run agent
await agent.RunAllAsync();

// If registered tenant is "acme-corp":
// Worker listens on: "acme-corp:CustomerSupport : Default Workflow - Tickets"
// Only handles workflows from "acme-corp":
//   - "acme-corp:CustomerSupport:...:uuid1" ✅
//   - "contoso:CustomerSupport:...:uuid2"   ❌ REJECTED
```

## Multi-Tenant Handler Patterns

### Pattern 1: Tenant-Specific Configuration

```csharp
workflow.OnUserMessage(async (context) =>
{
    var config = await GetTenantConfig(context.TenantId);
    
    // Use tenant-specific settings
    if (config.EnableFeatureX)
    {
        await ProcessWithFeatureX(context);
    }
    else
    {
        await ProcessStandard(context);
    }
});
```

### Pattern 2: Tenant Database Isolation

```csharp
workflow.OnUserMessage(async (context) =>
{
    // Get tenant-specific database connection
    using var db = GetTenantDatabase(context.TenantId);
    
    // All operations scoped to tenant's data
    var userRecords = await db.Users
        .Where(u => u.TenantId == context.TenantId)
        .ToListAsync();
    
    await context.ReplyAsync($"Found {userRecords.Count} users");
});
```

### Pattern 3: Tenant-Aware Logging

```csharp
workflow.OnUserMessage(async (context) =>
{
    _logger.LogInformation(
        "Processing message for tenant {TenantId}, user {UserId}",
        context.TenantId,
        context.ParticipantId
    );
    
    // Process with full audit trail
    await ProcessMessage(context);
});
```

## Security Considerations

### ✅ Safe Practices

1. **Always use `context.TenantId`** - Don't trust external tenant claims
2. **Scope database queries** - Include tenant ID in WHERE clauses
3. **Validate permissions** - Check user belongs to tenant
4. **Audit tenant context** - Log tenant ID with all operations
5. **Isolate resources** - Use tenant-specific storage/queues when needed

### ❌ Anti-Patterns

```csharp
// ❌ WRONG: Trusting external tenant claim
workflow.OnUserMessage(async (context) =>
{
    var tenantId = context.Data["tenantId"]; // DON'T DO THIS
    // Use context.TenantId instead!
});

// ❌ WRONG: Not filtering by tenant
workflow.OnUserMessage(async (context) =>
{
    var allUsers = await db.Users.ToListAsync(); // LEAKS DATA
    // Filter by context.TenantId!
});

// ❌ WRONG: Mixing tenant data
var cache = new Dictionary<string, object>(); // Shared across tenants!
// Use tenant-specific cache keys!
```

### ✅ Correct Patterns

```csharp
// ✅ CORRECT: Use context.TenantId
workflow.OnUserMessage(async (context) =>
{
    var tenant = context.TenantId; // From WorkflowId, validated
    
    // ✅ Scope database query
    var users = await db.Users
        .Where(u => u.TenantId == tenant)
        .ToListAsync();
    
    // ✅ Tenant-specific cache key
    var cacheKey = $"{tenant}:users:{context.ParticipantId}";
    
    // ✅ Validate permissions
    if (!await HasAccess(tenant, context.ParticipantId))
    {
        await context.ReplyAsync("Access denied");
        return;
    }
});
```

## Configuration

### Registering System-Scoped Agents

```csharp
var agent = platform.Agents.Register(new XiansAgentRegistration
{
    Name = "MyAgent",
    SystemScoped = true  // Set to true for multi-tenant
});

// For non-system-scoped agents, ensure TenantId is set
var agent = platform.Agents.Register(new XiansAgentRegistration
{
    Name = "MyAgent",
    SystemScoped = false  // TenantId extracted from ApiKey
});
```

The `TenantId` for non-system-scoped agents is automatically extracted from the API key certificate during platform initialization.

## Temporal Determinism

All tenant extraction and validation is **deterministic** and **replay-safe**:

- ✅ `Workflow.Info.WorkflowId` - Immutable, set at creation
- ✅ String split operations - Pure functions
- ✅ Dictionary lookup - Static state set before workflow starts
- ✅ No external I/O during validation

**Replay behavior:**
```
Initial Run:
  1. Extract tenant from WorkflowId → "acme-corp"
  2. Validate or pass through based on SystemScoped
  3. Invoke handler with tenant context

Replay (after restart):
  1. Extract tenant from WorkflowId → "acme-corp" (same)
  2. Validate or pass through (same logic)
  3. Invoke handler with tenant context (same)
  
✅ Deterministic!
```

## HTTP Request Handling

### X-Tenant-Id Header

All HTTP requests to the Xians platform automatically include the `X-Tenant-Id` header:

```csharp
// Automatically added by MessageActivities
httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);
```

**How it works:**
1. Tenant ID is extracted from `Workflow.Info.WorkflowId` (first part before ":")
2. Passed to activities via `SendMessageRequest.TenantId`
3. Added as `X-Tenant-Id` header in HTTP requests
4. Server routes the message to the correct tenant

**Benefits:**
- ✅ System-scoped agents can send replies to any tenant
- ✅ Replies go to the tenant that initiated the workflow
- ✅ No risk of cross-tenant message leakage
- ✅ Matches XiansAi.Lib.Src behavior (TenantIdHandler)

## Troubleshooting

### Replies Going to Wrong Tenant (System-Scoped)

**Symptom:** System-scoped agent sends reply to wrong tenant

**Cause:** Missing or incorrect `X-Tenant-Id` header in HTTP request

**Solution:**
- Verify `context.TenantId` contains correct tenant
- Check that WorkflowId format is correct: `TenantId:WorkflowType:...`
- Ensure MessageActivities is adding the header (should be automatic)

### "Tenant isolation violation" Error

**Cause:** WorkflowId tenant doesn't match agent's registered tenant (non-system-scoped only)

**Solution:**
- Verify the workflow was created with correct tenant ID
- Check API key certificate contains correct tenant
- Ensure workflow routing is correct on server side

### "Invalid WorkflowId format" Error

**Cause:** WorkflowId doesn't contain tenant information

**Solution:**
- Ensure workflows are created with format `TenantId:WorkflowType:...`
- Check server-side workflow creation logic
- Verify workflow starter includes tenant context

### Agent Processing Wrong Tenant's Data

**Cause:** Handler not using `context.TenantId` for data isolation

**Solution:**
```csharp
// Always scope queries by tenant
var data = await db.Records
    .Where(r => r.TenantId == context.TenantId)  // Add this!
    .ToListAsync();
```

## Best Practices

1. **Use System-Scoped for Shared Services**
   - Billing, analytics, notifications
   - Admin tools, monitoring
   - Cross-tenant reporting

2. **Use Non-System-Scoped for Isolated Workflows**
   - Customer-specific logic
   - Tenant data processing
   - Isolated environments

3. **Always Validate Tenant Context**
   - Use `context.TenantId` for all tenant operations
   - Never trust external tenant claims
   - Scope all database queries by tenant

4. **Log Tenant Context**
   - Include tenant ID in all logs
   - Enable tenant-specific debugging
   - Maintain audit trails

5. **Test Multi-Tenant Scenarios**
   - Test with multiple tenants simultaneously
   - Verify tenant isolation
   - Check for data leakage

## Related Documentation

- [Worker Registration](WorkerRegistration.md) - How workers are created and managed
- [Configuration](Configuration.md) - Platform configuration options
- [Getting Started](GettingStarted.md) - Quick start guide

