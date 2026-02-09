# Developer Guide: Tenant Handling and Multi-Tenant Agents

## Overview

This guide explains how to build agents with Xians.Lib that support single-tenant and multi-tenant scenarios. You'll learn about tenant isolation, system-scoped agents, and how the library automatically handles tenant context for you.

## Table of Contents

1. [Understanding Tenant Context](#understanding-tenant-context)
2. [Agent Types: System-Scoped vs Tenant-Scoped](#agent-types)
3. [Quick Start Examples](#quick-start-examples)
4. [How Tenant Context Works](#how-tenant-context-works)
5. [Development Patterns](#development-patterns)
6. [Security Best Practices](#security-best-practices)
7. [Troubleshooting](#troubleshooting)
8. [API Reference](#api-reference)

---

## Understanding Tenant Context

### What is a Tenant?

A **tenant** represents an isolated customer or organization in a multi-tenant application. Each tenant has:
- Unique tenant ID (e.g., "acme-corp", "contoso")
- Isolated data and resources
- Separate billing, users, and configurations

### What is Tenant Context?

**Tenant context** is the tenant information automatically tracked throughout your workflow execution. The library extracts this from the workflow ID and makes it available via `context.Message.TenantId`.

```csharp
workflow.OnUserMessage(async (context) =>
{
    // Tenant context is automatically available
    var tenantId = context.Message.TenantId;  // e.g., "acme-corp"
    
    Console.WriteLine($"Processing request for tenant: {tenantId}");
});
```

### Why Does Tenant Context Matter?

1. **Data Isolation**: Ensures tenant A cannot access tenant B's data
2. **Security**: Prevents cross-tenant information leakage
3. **Billing**: Track usage per tenant
4. **Configuration**: Apply tenant-specific settings
5. **Compliance**: Meet regulatory requirements for data separation

---

## Agent Types

Xians.Lib supports two types of agents based on their tenant handling:

### Non-System-Scoped Agents (Tenant-Isolated)

**Use when**: Building tenant-specific services where each tenant should have isolated workers.

**Characteristics**:
- ✅ One worker pool per tenant
- ✅ Strict tenant isolation enforced automatically
- ✅ Tenant ID extracted from your API key
- ❌ Cannot handle requests from other tenants

**Example Use Cases**:
- Customer support chatbots
- Tenant-specific data processing
- Isolated environments
- Custom business logic per tenant

### System-Scoped Agents (Multi-Tenant)

**Use when**: Building services shared across all tenants from a single worker pool.

**Characteristics**:
- ✅ One worker pool handles all tenants
- ✅ Can process requests from any tenant
- ✅ Tenant context provided for each request
- ⚠️ Your code must implement tenant isolation

**Example Use Cases**:
- Global notification services
- Billing and payment processing
- Analytics and reporting
- Admin tools
- Cross-tenant operations

### Comparison Table

| Feature | System-Scoped | Non-System-Scoped |
|---------|--------------|-------------------|
| **Worker Pool** | Shared across all tenants | Isolated per tenant |
| **Tenant Validation** | No automatic validation | Automatic validation |
| **Tenant Context** | Available via `context.Message.TenantId` | Available via `context.Message.TenantId` |
| **Use Case** | Multi-tenant services | Single-tenant services |
| **Worker Efficiency** | High (shared pool) | Lower (separate pools) |
| **Isolation** | Manual (in your code) | Automatic (by library) |
| **API Key** | Can be system-level | Must be tenant-specific |

---

## Quick Start Examples

### Example 1: Non-System-Scoped Agent (Tenant-Isolated)

```csharp
using Xians.Lib.Agents;

// Initialize the platform
var platform = await XiansPlatform.InitializeAsync(new XiansOptions
{
    ServerUrl = "https://api.xians.io",
    ApiKey = apiKey  // Tenant ID automatically extracted from API key
});

// Register a tenant-isolated agent
var agent = platform.Agents.Register(new XiansAgentRegistration
{
    Name = "CustomerSupport",
    SystemScoped = false  // Tenant-isolated (default)
});

// Define workflow
var workflow = await agent.Workflows.DefineBuiltIn(
    name: "SupportTickets",
    workers: 2
);

// Register message handler
workflow.OnUserMessage(async (context) =>
{
    // context.Message.TenantId is always your registered tenant
    // The library automatically rejects requests from other tenants
    
    var ticketId = await CreateSupportTicket(
        tenantId: context.Message.TenantId,  // Your tenant only
        userId: context.Message.ParticipantId,
        message: context.Message.Text
    );
    
    await context.ReplyAsync($"Ticket #{ticketId} created!");
});

// Run the agent
await agent.RunAllAsync();
```

**What happens**:
1. Tenant ID is extracted from your API key certificate
2. Worker listens on queue: `{yourTenantId}:CustomerSupport:Default Workflow - SupportTickets`
3. Only processes workflows from your tenant
4. Automatically rejects requests from other tenants

### Example 2: System-Scoped Agent (Multi-Tenant)

```csharp
using Xians.Lib.Agents;

// Initialize the platform
var platform = await XiansPlatform.InitializeAsync(new XiansOptions
{
    ServerUrl = "https://api.xians.io",
    ApiKey = systemApiKey  // Can be system-level API key
});

// Register a system-scoped (multi-tenant) agent
var agent = platform.Agents.Register(new XiansAgentRegistration
{
    Name = "NotificationService",
    SystemScoped = true  // Multi-tenant
});

// Define workflow
var workflow = await agent.Workflows.DefineBuiltIn(
    name: "EmailAlerts",
    workers: 5  // Shared pool handling multiple tenants
);

// Register message handler
workflow.OnUserMessage(async (context) =>
{
    // context.Message.TenantId tells you which tenant this request is for
    // You MUST implement tenant isolation in your code
    
    var tenantId = context.Message.TenantId;  // e.g., "acme-corp", "contoso", etc.
    
    // Load tenant-specific configuration
    var config = await GetTenantConfig(tenantId);
    
    // Send notification using tenant's settings
    await SendEmail(
        to: config.NotificationEmail,
        subject: $"Alert for {tenantId}",
        body: context.Message.Text,
        smtpSettings: config.SmtpSettings
    );
    
    await context.ReplyAsync($"Notification sent for tenant {tenantId}");
});

// Run the agent
await agent.RunAllAsync();
```

**What happens**:
1. Worker listens on queue: `NotificationService:Default Workflow - EmailAlerts`
2. Can process workflows from ANY tenant
3. Each request includes tenant context via `context.Message.TenantId`
4. Your code is responsible for tenant isolation

---

## How Tenant Context Works

### Workflow ID Format

Every workflow has an ID that contains the tenant information:

```
Format: {TenantId}:{WorkflowType}:{OptionalSuffix}

Examples:
  - "acme-corp:CustomerSupport:Default Workflow - SupportTickets:uuid-123"
  - "contoso:NotificationService:Default Workflow - EmailAlerts:uuid-456"
  - "tenant-xyz:BillingService:Default Workflow:uuid-789"
```

### Automatic Tenant Extraction

The library automatically extracts the tenant ID from the workflow ID and provides it to you:

```csharp
workflow.OnUserMessage(async (context) =>
{
    // Tenant ID is automatically extracted and available
    var tenantId = context.Message.TenantId;  // "acme-corp"
    
    // Use it in your business logic
    var data = await LoadTenantData(tenantId);
    await ProcessRequest(data, context.Message.Text);
    await context.ReplyAsync("Processed!");
});
```

### Task Queue Routing

The library uses different queue naming based on agent type:

**Non-System-Scoped (Tenant-Isolated)**:
```
Queue Name: {TenantId}:{WorkflowType}
Example: "acme-corp:CustomerSupport:Default Workflow - SupportTickets"

Result: Only processes workflows for "acme-corp"
```

**System-Scoped (Multi-Tenant)**:
```
Queue Name: {WorkflowType}
Example: "NotificationService:Default Workflow - EmailAlerts"

Result: Processes workflows for ALL tenants
```

### Tenant Context Flow

```
1. User from "acme-corp" sends a message
   │
2. Server creates workflow with ID:
   └─> "acme-corp:NotificationService:Default Workflow:uuid-123"
   │
3. Server routes to task queue based on agent type:
   ├─> System-scoped: Queue "NotificationService:Default Workflow"
   └─> Non-system-scoped: Queue "acme-corp:NotificationService:Default Workflow"
   │
4. Your worker picks up the workflow
   │
5. Library extracts tenant: "acme-corp"
   │
6. Your handler receives context with context.Message.TenantId = "acme-corp"
   │
7. When you call context.ReplyAsync():
   └─> Library adds X-Tenant-Id: acme-corp header
       └─> Reply goes to correct tenant
```

---

## Development Patterns

### Pattern 1: Tenant-Specific Database Queries

**Always scope database queries by tenant ID:**

```csharp
workflow.OnUserMessage(async (context) =>
{
    // ✅ CORRECT: Filter by tenant
    var users = await db.Users
        .Where(u => u.TenantId == context.Message.TenantId)
        .ToListAsync();
    
    await context.ReplyAsync($"Found {users.Count} users in your organization");
});
```

**❌ WRONG - Data leakage:**
```csharp
// DON'T DO THIS - Returns data from ALL tenants!
var users = await db.Users.ToListAsync();
```

### Pattern 2: Tenant-Specific Configuration

```csharp
workflow.OnUserMessage(async (context) =>
{
    // Load tenant-specific settings
    var config = await GetTenantConfiguration(context.Message.TenantId);
    
    // Apply tenant-specific behavior
    if (config.Features.EnableAdvancedSearch)
    {
        var results = await AdvancedSearch(context.Message.Text, context.Message.TenantId);
        await context.ReplyAsync(FormatResults(results));
    }
    else
    {
        var results = await BasicSearch(context.Message.Text, context.Message.TenantId);
        await context.ReplyAsync(FormatResults(results));
    }
});
```

### Pattern 3: Tenant-Specific Resources

```csharp
workflow.OnUserMessage(async (context) =>
{
    var tenantId = context.Message.TenantId;
    
    // Get tenant-specific database connection
    using var db = GetTenantDatabase(tenantId);
    
    // Get tenant-specific storage bucket
    var storage = GetTenantStorage(tenantId);
    
    // Get tenant-specific API keys
    var apiKeys = await GetTenantApiKeys(tenantId);
    
    // Process with tenant-isolated resources
    await ProcessWithTenantResources(db, storage, apiKeys);
});
```

### Pattern 4: Tenant-Aware Logging and Monitoring

```csharp
workflow.OnUserMessage(async (context) =>
{
    // Include tenant ID in all logs
    _logger.LogDebug(
        "Processing request for tenant {TenantId}, user {UserId}, request {RequestId}",
        context.Message.TenantId,
        context.Message.ParticipantId,
        context.Message.RequestId
    );
    
    try
    {
        await ProcessRequest(context);
        
        // Track metrics per tenant
        _metrics.IncrementCounter("requests_processed", new Dictionary<string, string>
        {
            { "tenant_id", context.Message.TenantId },
            { "status", "success" }
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex,
            "Request failed for tenant {TenantId}",
            context.Message.TenantId
        );
        
        _metrics.IncrementCounter("requests_processed", new Dictionary<string, string>
        {
            { "tenant_id", context.Message.TenantId },
            { "status", "error" }
        });
        
        throw;
    }
});
```

### Pattern 5: Tenant-Specific Caching

```csharp
// ❌ WRONG - Cache key without tenant ID
var cacheKey = $"user:{userId}";  // Can collide across tenants!

// ✅ CORRECT - Include tenant ID in cache key
var cacheKey = $"{context.Message.TenantId}:user:{userId}";

workflow.OnUserMessage(async (context) =>
{
    var cacheKey = $"{context.Message.TenantId}:config";
    
    // Try to get from cache
    if (!_cache.TryGetValue(cacheKey, out var config))
    {
        // Load from database
        config = await LoadTenantConfig(context.Message.TenantId);
        
        // Cache with tenant-specific key
        _cache.Set(cacheKey, config, TimeSpan.FromMinutes(10));
    }
    
    // Use config
    await ProcessWithConfig(config);
});
```

### Pattern 6: Multi-Tenant Rate Limiting

```csharp
workflow.OnUserMessage(async (context) =>
{
    var tenantId = context.Message.TenantId;
    
    // Check rate limit per tenant
    if (!await _rateLimiter.AllowRequest(tenantId))
    {
        await context.ReplyAsync(
            "Rate limit exceeded. Please try again later."
        );
        return;
    }
    
    // Process request
    await ProcessRequest(context);
});
```

---

## Security Best Practices

### ✅ DO: Always Use context.Message.TenantId

```csharp
workflow.OnUserMessage(async (context) =>
{
    // ✅ CORRECT - Use the library-provided tenant ID
    var tenantId = context.Message.TenantId;
    
    var data = await LoadData(tenantId);
    await context.ReplyAsync($"Loaded data for {tenantId}");
});
```

### ❌ DON'T: Trust External Tenant Claims

```csharp
workflow.OnUserMessage(async (context) =>
{
    // ❌ WRONG - Never trust tenant ID from user input
    var tenantId = context.Message.Data["tenantId"];  // Could be forged!
    
    // ❌ WRONG - Don't parse from message
    var tenantId = ExtractTenantFromMessage(context.Message.Text);
    
    // ✅ CORRECT - Use context.Message.TenantId
    var tenantId = context.Message.TenantId;
});
```

### ✅ DO: Scope All Queries by Tenant

```csharp
workflow.OnUserMessage(async (context) =>
{
    // ✅ CORRECT - Always filter by tenant
    var orders = await db.Orders
        .Where(o => o.TenantId == context.Message.TenantId)
        .Where(o => o.UserId == context.Message.ParticipantId)
        .ToListAsync();
    
    await context.ReplyAsync($"Found {orders.Count} orders");
});
```

### ✅ DO: Validate User Belongs to Tenant

```csharp
workflow.OnUserMessage(async (context) =>
{
    // ✅ Verify user belongs to the tenant
    var user = await db.Users
        .Where(u => u.Id == context.Message.ParticipantId)
        .Where(u => u.TenantId == context.Message.TenantId)
        .FirstOrDefaultAsync();
    
    if (user == null)
    {
        await context.ReplyAsync("Access denied");
        return;
    }
    
    // Process request
    await ProcessForUser(user);
});
```

### ✅ DO: Use Tenant-Specific Storage

```csharp
workflow.OnUserMessage(async (context) =>
{
    // ✅ CORRECT - Separate storage per tenant
    var storagePath = $"tenants/{context.Message.TenantId}/uploads/{fileName}";
    await SaveFile(storagePath, fileData);
    
    // ❌ WRONG - Shared storage without tenant separation
    // var storagePath = $"uploads/{fileName}";  // Data leakage!
});
```

### ✅ DO: Audit Tenant Access

```csharp
workflow.OnUserMessage(async (context) =>
{
    // Log all tenant access for audit trail
    await _auditLog.LogAccessAsync(new AuditEntry
    {
        TenantId = context.Message.TenantId,
        UserId = context.Message.ParticipantId,
        Action = "ViewSensitiveData",
        Timestamp = DateTime.UtcNow,
        RequestId = context.Message.RequestId
    });
    
    // Process request
    await ProcessSensitiveData(context);
});
```

---

## Troubleshooting

### Issue: "Tenant isolation violation" Error

**Symptom**: Non-system-scoped agent rejects a message with tenant mismatch error.

**Cause**: The workflow's tenant ID doesn't match your agent's registered tenant.

**Solution**:
1. Verify your API key is for the correct tenant
2. Check that the workflow was created with the correct tenant ID
3. Ensure server-side routing is correct

```csharp
// Check your tenant ID
var platform = await XiansPlatform.InitializeAsync(new XiansOptions
{
    ServerUrl = "https://api.xians.io",
    ApiKey = apiKey
});

Console.WriteLine($"Tenant ID: {platform.Options.TenantId}");
```

### Issue: Wrong Tenant Receiving Replies (System-Scoped)

**Symptom**: System-scoped agent sends reply to wrong tenant.

**Cause**: Not using `context.Message.TenantId` consistently.

**Solution**:
```csharp
workflow.OnUserMessage(async (context) =>
{
    // ✅ CORRECT - Library automatically routes to correct tenant
    await context.ReplyAsync("Message");
    
    // The library adds X-Tenant-Id header automatically using context.Message.TenantId
});
```

### Issue: Data Leakage Between Tenants

**Symptom**: Tenant A can see tenant B's data.

**Cause**: Database queries not filtered by tenant ID.

**Solution**:
```csharp
// ❌ WRONG - Returns data from all tenants
var data = await db.Records.ToListAsync();

// ✅ CORRECT - Filter by tenant
var data = await db.Records
    .Where(r => r.TenantId == context.Message.TenantId)
    .ToListAsync();
```

### Issue: Invalid WorkflowId Format Error

**Symptom**: Error parsing workflow ID.

**Cause**: WorkflowId doesn't follow expected format: `TenantId:WorkflowType:...`

**Solution**:
- Check server-side workflow creation logic
- Verify workflow starter includes tenant in ID
- Contact platform support if issue persists

### Issue: High Memory Usage with System-Scoped Agents

**Symptom**: System-scoped agent consuming excessive memory.

**Cause**: Caching data globally instead of per-tenant, or not cleaning up tenant-specific resources.

**Solution**:
```csharp
// ✅ Use tenant-specific cache with expiration
var cacheKey = $"{context.Message.TenantId}:data:{key}";
_cache.Set(cacheKey, data, TimeSpan.FromMinutes(10));

// ✅ Implement cache cleanup
_cache.RegisterEvictionCallback((key, value, reason, state) =>
{
    // Clean up tenant-specific resources
    if (value is IDisposable disposable)
    {
        disposable.Dispose();
    }
});
```

---

## API Reference

### UserMessageContext Properties

```csharp
public class UserMessageContext
{
    // The tenant ID for this request (automatically extracted from WorkflowId)
    public string TenantId { get; }
    
    // The user/participant ID
    public string ParticipantId { get; }
    
    // The user's message
    public UserMessage Message { get; }
    
    // Unique request ID for tracking
    public string RequestId { get; }
    
    // Message scope
    public string Scope { get; }
    
    // Conversation thread ID
    public string? ThreadId { get; }
    
    // Additional data
    public object Data { get; }
}
```

### UserMessageContext Methods

```csharp
// Send a text reply
await context.ReplyAsync("Your message");

// Send a reply with structured data
await context.ReplyWithDataAsync("Your message", new { 
    items = items,
    count = items.Count
});

// Get conversation history
var history = await context.GetChatHistoryAsync(page: 1, pageSize: 50);
```

### XiansAgentRegistration

```csharp
public class XiansAgentRegistration
{
    // Agent name (required)
    public string Name { get; set; }
    
    // Whether this is a system-scoped (multi-tenant) agent
    // Default: false (tenant-isolated)
    public bool SystemScoped { get; set; } = false;
}
```

### XiansWorkflow Configuration

```csharp
// Define a default workflow
var workflow = await agent.Workflows.DefineBuiltIn(
    name: "MyWorkflow",      // Optional workflow name
    workers: 3               // Number of worker instances (default: 1)
);

// Register message handler
workflow.OnUserMessage(async (context) =>
{
    // Your handler logic
});
```

---

## Advanced Topics

### Using the TenantContext Utility (Internal)

While the library handles tenant extraction automatically, advanced users can use the `TenantContext` utility:

```csharp
using Xians.Lib.Common;

// Extract tenant ID from a workflow ID
var tenantId = TenantContext.ExtractTenantId(workflowId);

// Extract workflow type
var workflowType = TenantContext.ExtractWorkflowType(workflowId);

// Parse complete workflow identifier
var identifier = TenantContext.Parse(workflowId);
Console.WriteLine($"Tenant: {identifier.TenantId}");
Console.WriteLine($"Type: {identifier.WorkflowType}");
```

### Temporal Determinism

All tenant handling is deterministic and replay-safe:

- ✅ Tenant extraction uses immutable workflow ID
- ✅ String operations are pure functions
- ✅ No external I/O during tenant handling
- ✅ Workflows replay consistently

**What this means for you**: Don't worry about tenant context changing during workflow replays. The library guarantees consistent behavior.

---

## Complete Example: Multi-Tenant SaaS Application

```csharp
using Xians.Lib.Agents;
using Microsoft.Extensions.Logging;

public class MultiTenantSaasAgent
{
    private readonly ILogger<MultiTenantSaasAgent> _logger;
    private readonly IDatabase _database;
    private readonly ICache _cache;
    
    public async Task RunAsync(string apiKey)
    {
        // Initialize platform
        var platform = await XiansPlatform.InitializeAsync(new XiansOptions
        {
            ServerUrl = "https://api.xians.io",
            ApiKey = apiKey
        });
        
        // Register system-scoped agent for shared services
        var notificationAgent = platform.Agents.Register(new XiansAgentRegistration
        {
            Name = "NotificationService",
            SystemScoped = true
        });
        
        var notificationWorkflow = await notificationAgent.Workflows.DefineBuiltIn(
            name: "Alerts",
            workers: 5
        );
        
        notificationWorkflow.OnUserMessage(async (context) =>
        {
            await HandleNotification(context);
        });
        
        // Register tenant-scoped agent for customer-specific logic
        var customerAgent = platform.Agents.Register(new XiansAgentRegistration
        {
            Name = "CustomerService",
            SystemScoped = false  // Tenant-isolated
        });
        
        var customerWorkflow = await customerAgent.Workflows.DefineBuiltIn(
            name: "Support",
            workers: 2
        );
        
        customerWorkflow.OnUserMessage(async (context) =>
        {
            await HandleCustomerSupport(context);
        });
        
        // Run all agents
        await Task.WhenAll(
            notificationAgent.RunAllAsync(),
            customerAgent.RunAllAsync()
        );
    }
    
    private async Task HandleNotification(UserMessageContext context)
    {
        _logger.LogDebug(
            "Processing notification for tenant {TenantId}",
            context.Message.TenantId
        );
        
        // Load tenant-specific notification settings
        var settings = await GetTenantNotificationSettings(context.Message.TenantId);
        
        // Send notification using tenant's preferences
        if (settings.EmailEnabled)
        {
            await SendEmail(
                to: settings.NotificationEmail,
                subject: "Alert",
                body: context.Message.Text,
                smtpConfig: settings.SmtpConfig
            );
        }
        
        if (settings.SmsEnabled)
        {
            await SendSms(
                to: settings.NotificationPhone,
                message: context.Message.Text,
                smsConfig: settings.SmsConfig
            );
        }
        
        await context.ReplyAsync($"Notification sent via {string.Join(", ", GetEnabledChannels(settings))}");
    }
    
    private async Task HandleCustomerSupport(UserMessageContext context)
    {
        _logger.LogDebug(
            "Processing support request for tenant {TenantId}, user {UserId}",
            context.Message.TenantId,
            context.Message.ParticipantId
        );
        
        // Get conversation history
        var history = await context.GetChatHistoryAsync(page: 1, pageSize: 10);
        
        // Load tenant-specific data
        var user = await _database.Users
            .Where(u => u.TenantId == context.Message.TenantId)
            .Where(u => u.Id == context.Message.ParticipantId)
            .FirstOrDefaultAsync();
        
        if (user == null)
        {
            await context.ReplyAsync("User not found");
            return;
        }
        
        // Create support ticket
        var ticket = new SupportTicket
        {
            TenantId = context.Message.TenantId,
            UserId = user.Id,
            Subject = "Support Request",
            Description = context.Message.Text,
            CreatedAt = DateTime.UtcNow
        };
        
        await _database.SupportTickets.AddAsync(ticket);
        await _database.SaveChangesAsync();
        
        // Send confirmation
        await context.ReplyAsync($"Support ticket #{ticket.Id} created. We'll respond within 24 hours.");
    }
    
    private async Task<NotificationSettings> GetTenantNotificationSettings(string tenantId)
    {
        // Try cache first
        var cacheKey = $"{tenantId}:notification-settings";
        if (_cache.TryGetValue(cacheKey, out NotificationSettings? settings) && settings != null)
        {
            return settings;
        }
        
        // Load from database
        settings = await _database.NotificationSettings
            .Where(s => s.TenantId == tenantId)
            .FirstOrDefaultAsync();
        
        if (settings != null)
        {
            _cache.Set(cacheKey, settings, TimeSpan.FromMinutes(10));
        }
        
        return settings ?? NotificationSettings.Default;
    }
}
```

---

## Summary

### Key Takeaways

1. **Use Non-System-Scoped** for tenant-specific services where isolation is critical
2. **Use System-Scoped** for shared services that need to handle multiple tenants
3. **Always use `context.Message.TenantId`** - never trust external tenant claims
4. **Scope all queries** by tenant ID to prevent data leakage
5. **Include tenant ID** in cache keys, logs, and metrics
6. **Validate permissions** - ensure users belong to their claimed tenant
7. **Test multi-tenant scenarios** to verify isolation

### Quick Decision Guide

**Should I use System-Scoped or Non-System-Scoped?**

```
Is your service shared across ALL tenants?
├─ YES → System-Scoped
│  └─ Examples: Notifications, Billing, Analytics
│
└─ NO → Non-System-Scoped
   └─ Examples: Customer Support, Custom Workflows
```

### Next Steps

1. Review the [Quick Start Examples](#quick-start-examples)
2. Choose your agent type (system-scoped vs non-system-scoped)
3. Implement tenant isolation in your code
4. Test with multiple tenants
5. Monitor logs for tenant context
6. Review security best practices

---

## Related Documentation

- [Getting Started](GettingStarted.md) - Initial setup guide
- [Configuration](Configuration.md) - Platform configuration options
- [Worker Registration](WorkerRegistration.md) - Advanced worker configuration
- [Temporal Constraints](../Workflows/TEMPORAL_CONSTRAINTS.md) - Workflow determinism rules

## Support

For issues or questions:
- Check [Troubleshooting](#troubleshooting) section
- Review code examples
- Contact platform support

---

**Version**: 1.0  
**Last Updated**: December 2025


