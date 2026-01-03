# Document Storage

## Overview

The document storage system enables agents to persist and query structured data in a tenant-scoped database. Documents are JSON objects with metadata, types, and optional custom keys for semantic identification.

**Key Features:**
- 📝 **JSON Storage** - Store any JSON-serializable object
- 🔍 **Flexible Querying** - Filter by type, metadata, dates
- 🔑 **Semantic Keys** - Use meaningful keys like "user-123-preferences"
- ⏰ **TTL Support** - Auto-delete documents after expiration
- 🏢 **Tenant Isolation** - Documents scoped to agent and tenant

---

## Quick Start

```csharp
var agent = platform.Agents.Register("MyAgent");

// Save a document
var document = new Document
{
    Type = "user-preference",
    Key = "user-123",
    Content = JsonSerializer.SerializeToElement(new
    {
        Theme = "dark",
        Language = "en"
    })
};

var saved = await agent.Documents.SaveAsync(document);

// Retrieve it
var retrieved = await agent.Documents.GetAsync(saved.Id);
```

---

## Access Patterns

### 1. Agent-Level Access

Direct access from the agent instance:

```csharp
var agent = platform.Agents.Register("MyAgent");

// Save
await agent.Documents.SaveAsync(document);

// Get
var doc = await agent.Documents.GetAsync(documentId);

// Query
var results = await agent.Documents.QueryAsync(new DocumentQuery
{
    Type = "user-data"
});
```

### 2. Workflow Context Access

Access from message handlers:

```csharp
workflow.OnUserMessage(async (context) =>
{
    // Save user state
    var state = new Document
    {
        Type = "conversation-state",
        Key = context.Message.ParticipantId,
        Content = JsonSerializer.SerializeToElement(new
        {
            LastTopic = context.Message.Text,
            MessageCount = 5
        })
    };
    
    await context.SaveDocumentAsync(state);
    
    // Retrieve previous state
    var previousState = await context.GetDocumentAsync(state.Id);
    
    await context.ReplyAsync("State saved!");
});
```

---

## Document Model

```csharp
public class Document
{
    public string? Id { get; set; }              // Auto-generated if not provided
    public string? Type { get; set; }            // Categorization (e.g., "memory", "user-data")
    public string? Key { get; set; }             // Semantic key (e.g., "user-123-prefs")
    public JsonElement? Content { get; set; }    // The actual data
    public Dictionary<string, object>? Metadata { get; set; }  // Filterable metadata
    
    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    
    // Audit fields (populated automatically)
    public string? AgentId { get; set; }
    public string? WorkflowId { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}
```

---

## Basic Operations

### Save a Document

```csharp
var document = new Document
{
    Type = "order",
    Content = JsonSerializer.SerializeToElement(new
    {
        OrderId = "ORD-12345",
        Status = "Pending",
        Items = new[] { "Item 1", "Item 2" },
        Total = 99.99
    }),
    Metadata = new Dictionary<string, object>
    {
        ["customerId"] = "user-123",
        ["status"] = "pending"
    }
};

var saved = await agent.Documents.SaveAsync(document);
Console.WriteLine($"Saved with ID: {saved.Id}");
```

### Retrieve by ID

```csharp
var document = await agent.Documents.GetAsync("doc-id-123");

if (document != null)
{
    var content = document.Content!.Value;
    var orderId = content.GetProperty("OrderId").GetString();
    Console.WriteLine($"Order: {orderId}");
}
```

### Update a Document

```csharp
// Get existing document
var document = await agent.Documents.GetAsync("doc-id-123");

// Modify content
document.Content = JsonSerializer.SerializeToElement(new
{
    OrderId = "ORD-12345",
    Status = "Shipped",  // Updated status
    Items = new[] { "Item 1", "Item 2" },
    Total = 99.99
});

// Save changes
var updated = await agent.Documents.UpdateAsync(document);
Console.WriteLine($"Updated: {updated}");
```

### Delete a Document

```csharp
var deleted = await agent.Documents.DeleteAsync("doc-id-123");
Console.WriteLine($"Deleted: {deleted}");
```

---

## Semantic Keys

Use meaningful keys instead of random IDs for easier retrieval:

### Save with Key

```csharp
var userPreferences = new Document
{
    Type = "user-preferences",
    Key = $"user-{userId}",  // Semantic key
    Content = JsonSerializer.SerializeToElement(preferences)
};

var options = new DocumentOptions
{
    UseKeyAsIdentifier = true,  // Use Type+Key as unique identifier
    Overwrite = true            // Update if exists
};

await agent.Documents.SaveAsync(userPreferences, options);
```

### Retrieve by Key

```csharp
var prefs = await agent.Documents.GetByKeyAsync("user-preferences", $"user-{userId}");

if (prefs != null)
{
    // Got user preferences
}
```

**Benefits:**
- No need to remember/store document IDs
- Self-documenting code
- Easy to implement upsert semantics

---

## Querying Documents

### Basic Query

```csharp
var query = new DocumentQuery
{
    Type = "order",
    Limit = 10,
    SortBy = "CreatedAt",
    SortDescending = true
};

var orders = await agent.Documents.QueryAsync(query);
```

### Query with Metadata Filters

```csharp
var query = new DocumentQuery
{
    Type = "order",
    MetadataFilters = new Dictionary<string, object>
    {
        ["status"] = "pending",
        ["customerId"] = "user-123"
    },
    Limit = 20
};

var pendingOrders = await agent.Documents.QueryAsync(query);
```

### Date Range Query

```csharp
var query = new DocumentQuery
{
    Type = "analytics",
    CreatedAfter = DateTime.UtcNow.AddDays(-7),  // Last 7 days
    CreatedBefore = DateTime.UtcNow,
    Limit = 100
};

var recentAnalytics = await agent.Documents.QueryAsync(query);
```

### Pagination

```csharp
var query = new DocumentQuery
{
    Type = "logs",
    Limit = 50,      // Page size
    Skip = 100,      // Skip first 100 (page 3)
    SortBy = "CreatedAt",
    SortDescending = true
};

var page3 = await agent.Documents.QueryAsync(query);
```

---

## Advanced Features

### Time-to-Live (TTL)

Documents can auto-expire:

```csharp
var sessionDoc = new Document
{
    Type = "session",
    Content = JsonSerializer.SerializeToElement(sessionData)
};

var options = new DocumentOptions
{
    TtlMinutes = 60  // Delete after 1 hour
};

await agent.Documents.SaveAsync(sessionDoc, options);
```

**Common TTL Values:**
- `60` - 1 hour (sessions)
- `1440` - 1 day (temporary data)
- `10080` - 1 week (short-term cache)
- `43200` - 30 days (default)
- `null` - Never expires

### Bulk Operations

Delete multiple documents at once:

```csharp
var oldDocIds = new[] { "doc-1", "doc-2", "doc-3" };
var deletedCount = await agent.Documents.DeleteManyAsync(oldDocIds);
Console.WriteLine($"Deleted {deletedCount} documents");
```

### Overwrite Protection

```csharp
var options = new DocumentOptions
{
    Overwrite = false  // Fail if document exists
};

try
{
    await agent.Documents.SaveAsync(document, options);
}
catch (HttpRequestException)
{
    // Document already exists
}
```

---

## Use Cases

### User Preferences

```csharp
workflow.OnUserMessage(async (context) =>
{
    if (context.Message.Text.Contains("dark mode"))
    {
        // Save user preference
        var prefs = new Document
        {
            Type = "user-preferences",
            Key = context.Message.ParticipantId,
            Content = JsonSerializer.SerializeToElement(new
            {
                Theme = "dark",
                UpdatedAt = DateTime.UtcNow
            })
        };

        await context.SaveDocumentAsync(prefs, new DocumentOptions
        {
            UseKeyAsIdentifier = true,
            Overwrite = true
        });

        await context.ReplyAsync("Dark mode enabled! 🌙");
    }
});
```

### Conversation Memory

```csharp
workflow.OnUserMessage(async (context) =>
{
    // Retrieve conversation memory
    var memory = await context.GetDocumentAsync($"memory-{context.Message.ParticipantId}");
    
    var previousTopics = memory != null 
        ? memory.Content!.Value.GetProperty("Topics").Deserialize<List<string>>()
        : new List<string>();

    // Add current topic
    previousTopics.Add(ExtractTopic(context.Message.Text));

    // Save updated memory
    await context.SaveDocumentAsync(new Document
    {
        Id = memory?.Id,  // Update if exists
        Type = "conversation-memory",
        Key = context.Message.ParticipantId,
        Content = JsonSerializer.SerializeToElement(new
        {
            Topics = previousTopics,
            LastInteraction = DateTime.UtcNow
        })
    }, new DocumentOptions
    {
        UseKeyAsIdentifier = true,
        Overwrite = true,
        TtlMinutes = 10080  // 7 days
    });

    await context.ReplyAsync($"I remember we discussed: {string.Join(", ", previousTopics)}");
});
```

### Analytics Tracking

```csharp
// Track user interactions
var analytics = new Document
{
    Type = "user-analytics",
    Key = $"{userId}-{DateTime.UtcNow:yyyy-MM-dd}",
    Content = JsonSerializer.SerializeToElement(new
    {
        Date = DateTime.UtcNow.Date,
        MessageCount = 5,
        Topics = new[] { "orders", "support" },
        AverageResponseTime = 2.5
    }),
    Metadata = new Dictionary<string, object>
    {
        ["userId"] = userId,
        ["date"] = DateTime.UtcNow.Date.ToString("O")
    }
};

await agent.Documents.SaveAsync(analytics, new DocumentOptions
{
    UseKeyAsIdentifier = true,
    Overwrite = true,
    TtlMinutes = 43200  // 30 days
});

// Query last 7 days
var weeklyAnalytics = await agent.Documents.QueryAsync(new DocumentQuery
{
    Type = "user-analytics",
    MetadataFilters = new Dictionary<string, object> { ["userId"] = userId },
    CreatedAfter = DateTime.UtcNow.AddDays(-7),
    Limit = 7
});
```

### Session State

```csharp
// Save ephemeral session data
var session = new Document
{
    Type = "session",
    Key = sessionId,
    Content = JsonSerializer.SerializeToElement(new
    {
        UserId = userId,
        Cart = new[] { "item1", "item2" },
        CheckoutStep = 2
    })
};

await agent.Documents.SaveAsync(session, new DocumentOptions
{
    UseKeyAsIdentifier = true,
    Overwrite = true,
    TtlMinutes = 30  // 30 minute session
});
```

### Caching Expensive Operations

```csharp
// Check cache first
var cacheKey = $"api-result-{requestHash}";
var cached = await agent.Documents.GetByKeyAsync("api-cache", cacheKey);

if (cached != null)
{
    return cached.Content!.Value.Deserialize<ApiResponse>();
}

// Cache miss - make API call
var result = await ExpensiveApiCall();

// Cache the result
await agent.Documents.SaveAsync(new Document
{
    Type = "api-cache",
    Key = cacheKey,
    Content = JsonSerializer.SerializeToElement(result)
}, new DocumentOptions
{
    UseKeyAsIdentifier = true,
    TtlMinutes = 60  // 1 hour cache
});

return result;
```

---

## Document Types

Organize documents by type for easier querying:

| Type | Use Case | Example |
|------|----------|---------|
| `user-preferences` | User settings | Theme, language, notifications |
| `conversation-memory` | Chat context | Topics, entities, sentiment |
| `session` | Temporary state | Cart, wizard step, form data |
| `analytics` | Usage tracking | Metrics, events, aggregations |
| `api-cache` | Response caching | API results, computed values |
| `user-data` | Application data | Profiles, configurations |

---

## Metadata vs Content

### When to Use Metadata

Metadata is **filterable** - use it for properties you'll query by:

```csharp
Metadata = new Dictionary<string, object>
{
    ["userId"] = "user-123",      // ✅ Query by user
    ["status"] = "active",        // ✅ Filter by status
    ["priority"] = "high",        // ✅ Sort by priority
    ["category"] = "orders"       // ✅ Group by category
}
```

### When to Use Content

Content is the **main data** - not directly filterable, but can be any structure:

```csharp
Content = JsonSerializer.SerializeToElement(new
{
    // Complex nested objects
    Order = new
    {
        Id = "ORD-123",
        Items = new[] { /* ... */ },
        ShippingAddress = new { /* ... */ },
        PaymentMethod = new { /* ... */ }
    }
})
```

**Rule of Thumb:**
- Metadata → Simple values for filtering
- Content → Rich data structures

---

## Error Handling

```csharp
try
{
    var doc = await agent.Documents.SaveAsync(document);
}
catch (ArgumentException ex)
{
    // Validation error (e.g., missing Type/Key for UseKeyAsIdentifier)
    Console.WriteLine($"Invalid document: {ex.Message}");
}
catch (HttpRequestException ex)
{
    // Network/server error
    Console.WriteLine($"Failed to save: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    // Service not available or system-scoped agent
    Console.WriteLine($"Operation not supported: {ex.Message}");
}
```

---

## System-Scoped Agents

System-scoped agents **can use documents**, but with special considerations:

### From Workflow/Activity Context ✅

Documents work perfectly when called from workflow or activity context (tenant is extracted from workflow ID):

```csharp
var agent = platform.Agents.Register("MyAgent", systemScoped: true);
var workflow = await agent.Workflows.DefineBuiltIn();

workflow.OnUserMessage(async (context) =>
{
    // ✅ Works - tenant ID comes from workflow context
    var doc = new Document
    {
        Type = "user-data",
        Key = context.Message.ParticipantId,
        Content = JsonSerializer.SerializeToElement(new { Data = "value" })
    };
    
    await context.SaveDocumentAsync(doc);
    // Document is scoped to the tenant that initiated this workflow
    
    await context.ReplyAsync("Document saved!");
});
```

### Outside Workflow Context ❌

Direct agent-level calls outside workflows will fail for system-scoped agents:

```csharp
var agent = platform.Agents.Register("MyAgent", systemScoped: true);

// ❌ This will throw - no workflow context to extract tenant from
await agent.Documents.SaveAsync(document);
// Error: Documents API for system-scoped agents can only be used 
// within a workflow or activity context.
```

### How It Works

For **system-scoped agents**, the tenant ID is dynamically extracted from the workflow ID:

```
Workflow ID: tenant-123:MyAgent:Chat:user-456
              ↓
Extracted Tenant: tenant-123
              ↓
Document scoped to: tenant-123
```

Each workflow execution operates within a specific tenant context, ensuring proper isolation.

### Best Practice

For system-scoped agents, always access documents from workflow/activity context:

```csharp
// ✅ Good - From workflow handler
workflow.OnUserMessage(async (context) =>
{
    await context.SaveDocumentAsync(doc);  // Tenant from context
});

// ✅ Good - From activity
[Activity]
public async Task ProcessData(string data)
{
    var doc = new Document { /* ... */ };
    var tenantId = XiansContext.TenantId;  // Extracted from workflow
    // Use DocumentActivities or service directly
}

// ❌ Bad - Outside any context
await agent.Documents.SaveAsync(doc);  // No tenant context!
```

---

## API Reference

### DocumentCollection (Agent-Level)

| Method | Description |
|--------|-------------|
| `SaveAsync(document, options?, cancellationToken?)` | Save a new document |
| `GetAsync(id, cancellationToken?)` | Get by ID |
| `GetByKeyAsync(type, key, cancellationToken?)` | Get by Type+Key combination |
| `QueryAsync(query, cancellationToken?)` | Search with filters |
| `UpdateAsync(document, cancellationToken?)` | Update existing document |
| `DeleteAsync(id, cancellationToken?)` | Delete by ID |
| `DeleteManyAsync(ids, cancellationToken?)` | Bulk delete |
| `ExistsAsync(id, cancellationToken?)` | Check existence |

### UserMessageContext (Workflow-Level)

| Method | Description |
|--------|-------------|
| `SaveDocumentAsync(document, options?)` | Save from workflow |
| `GetDocumentAsync(id)` | Get from workflow |
| `QueryDocumentsAsync(query)` | Query from workflow |
| `UpdateDocumentAsync(document)` | Update from workflow |
| `DeleteDocumentAsync(id)` | Delete from workflow |

### DocumentOptions

```csharp
new DocumentOptions
{
    TtlMinutes = 60,              // Auto-delete after 1 hour
    Overwrite = true,             // Replace if exists
    UseKeyAsIdentifier = false    // Use Type+Key as ID
}
```

### DocumentQuery

```csharp
new DocumentQuery
{
    Type = "order",                                    // Filter by type
    Key = "specific-key",                              // Filter by key
    MetadataFilters = new Dictionary<string, object>   // Filter by metadata
    {
        ["status"] = "pending"
    },
    CreatedAfter = DateTime.UtcNow.AddDays(-7),       // Date range
    CreatedBefore = DateTime.UtcNow,
    Limit = 50,                                        // Pagination
    Skip = 0,
    SortBy = "CreatedAt",                              // Sorting
    SortDescending = true
}
```

---

## Complete Example

```csharp
var platform = await XiansPlatform.InitializeAsync(options);
var agent = platform.Agents.Register("OrderBot");

var workflow = await agent.Workflows.DefineBuiltIn();

workflow.OnUserMessage(async (context) =>
{
    var message = context.Message.Text.ToLower();

    if (message.Contains("create order"))
    {
        // Create new order document
        var order = new Document
        {
            Type = "order",
            Key = $"order-{Guid.NewGuid()}",
            Content = JsonSerializer.SerializeToElement(new
            {
                CustomerId = context.Message.ParticipantId,
                Status = "Pending",
                Items = new[] { "Product A", "Product B" },
                Total = 149.99,
                CreatedAt = DateTime.UtcNow
            }),
            Metadata = new Dictionary<string, object>
            {
                ["customerId"] = context.Message.ParticipantId,
                ["status"] = "pending",
                ["total"] = 149.99
            }
        };

        var saved = await context.SaveDocumentAsync(order, new DocumentOptions
        {
            TtlMinutes = 10080  // 7 days
        });

        await context.ReplyAsync($"✅ Order created: {saved.Id}");
    }
    else if (message.Contains("my orders"))
    {
        // Query user's orders
        var orders = await context.QueryDocumentsAsync(new DocumentQuery
        {
            Type = "order",
            MetadataFilters = new Dictionary<string, object>
            {
                ["customerId"] = context.Message.ParticipantId
            },
            Limit = 10,
            SortBy = "CreatedAt",
            SortDescending = true
        });

        if (orders.Any())
        {
            var orderList = string.Join("\n", orders.Select(o =>
            {
                var content = o.Content!.Value;
                return $"- {o.Key}: {content.GetProperty("Status").GetString()} (${content.GetProperty("Total").GetDouble()})";
            }));

            await context.ReplyAsync($"Your orders:\n{orderList}");
        }
        else
        {
            await context.ReplyAsync("You don't have any orders yet.");
        }
    }
    else if (message.Contains("cancel order"))
    {
        var orderId = ExtractOrderId(message);
        
        // Get and update order
        var order = await context.GetDocumentAsync(orderId);
        if (order != null)
        {
            var content = order.Content!.Value.Deserialize<Dictionary<string, JsonElement>>();
            content!["Status"] = JsonSerializer.SerializeToElement("Cancelled");
            
            order.Content = JsonSerializer.SerializeToElement(content);
            await context.UpdateDocumentAsync(order);
            
            await context.ReplyAsync("Order cancelled successfully.");
        }
    }
});

await platform.RunAsync();
```

---

## Best Practices

### 1. Use Semantic Keys

```csharp
// ✅ Good - Meaningful keys
Key = $"user-{userId}-preferences"
Key = $"session-{sessionId}"
Key = $"cache-{cacheKey}"

// ❌ Avoid - Random IDs as keys (defeats the purpose)
Key = Guid.NewGuid().ToString()
```

### 2. Set Appropriate TTLs

```csharp
// Session data - short TTL
TtlMinutes = 30

// User data - no expiration
TtlMinutes = null

// Cache - moderate TTL
TtlMinutes = 60
```

### 3. Use Metadata for Filters

```csharp
// ✅ Good - Queryable metadata
Metadata = new Dictionary<string, object>
{
    ["userId"] = userId,
    ["status"] = "active"
}

// ❌ Avoid - Putting queryable data only in Content
Content = JsonSerializer.SerializeToElement(new { UserId = userId, Status = "active" })
```

### 4. Handle Concurrent Updates

```csharp
// Get latest version
var doc = await agent.Documents.GetAsync(docId);

// Modify
doc.Content = UpdateContent(doc.Content);

// Update with optimistic concurrency
var updated = await agent.Documents.UpdateAsync(doc);

if (!updated)
{
    // Document was deleted or modified - handle conflict
}
```

### 5. Clean Up Test Data

```csharp
// Always use unique prefixes for test data
var testType = $"test-{Guid.NewGuid().ToString()[..8]}-data";

// Clean up after tests
foreach (var id in createdIds)
{
    await agent.Documents.DeleteAsync(id);
}
```

---

## Comparison with Knowledge

| Feature | Documents | Knowledge |
|---------|-----------|-----------|
| **Purpose** | Structured data storage | Agent prompts & configs |
| **Format** | Any JSON object | String content |
| **Querying** | ✅ Rich queries | ❌ Get/List only |
| **TTL** | ✅ Supported | ❌ No expiration |
| **Keys** | ✅ Semantic keys | Name only |
| **Use Case** | Dynamic data | Static configs |
| **System-Scoped** | ✅ From workflow context | ✅ Supported |

**When to use each:**
- **Documents** - Dynamic data that changes (user data, sessions, analytics)
- **Knowledge** - Static configuration (prompts, instructions, templates)

---

## Performance Tips

### 1. Use Queries Efficiently

```csharp
// ✅ Good - Specific filters
var query = new DocumentQuery
{
    Type = "order",
    MetadataFilters = new Dictionary<string, object> { ["userId"] = userId },
    Limit = 10
};

// ❌ Avoid - Fetching everything
var query = new DocumentQuery { Limit = 10000 };
```

### 2. Pagination for Large Datasets

```csharp
int pageSize = 50;
int page = 0;
List<Document> allDocs = new();

while (true)
{
    var results = await agent.Documents.QueryAsync(new DocumentQuery
    {
        Type = "logs",
        Limit = pageSize,
        Skip = page * pageSize
    });

    if (!results.Any()) break;
    
    allDocs.AddRange(results);
    page++;
}
```

### 3. Bulk Deletes

```csharp
// ✅ Good - Single bulk operation
await agent.Documents.DeleteManyAsync(oldIds);

// ❌ Avoid - Individual deletes
foreach (var id in oldIds)
{
    await agent.Documents.DeleteAsync(id);
}
```

---

## Summary

| Operation | Agent-Level | Workflow-Level |
|-----------|-------------|----------------|
| **Save** | `agent.Documents.SaveAsync()` | `context.SaveDocumentAsync()` |
| **Get** | `agent.Documents.GetAsync()` | `context.GetDocumentAsync()` |
| **Query** | `agent.Documents.QueryAsync()` | `context.QueryDocumentsAsync()` |
| **Update** | `agent.Documents.UpdateAsync()` | `context.UpdateDocumentAsync()` |
| **Delete** | `agent.Documents.DeleteAsync()` | `context.DeleteDocumentAsync()` |

**Key Points:**
- 📝 Store any JSON data with metadata
- 🔑 Use semantic keys for easier retrieval
- 🔍 Rich querying with filters and pagination
- ⏰ TTL support for automatic cleanup
- 🏢 Tenant-scoped only (not for system-scoped agents)
- ⚡ Works in both workflow and activity contexts

---

**See also:**
- [Knowledge Guide](Knowledge.md) - Store agent prompts and configs
- [Messaging Guide](Messaging.md) - Send messages to users
- [A2A Communication](A2A.md) - Agent-to-agent messaging
- [System-Scoped Agents](SystemScopedAgents.md) - Multi-tenant architecture

