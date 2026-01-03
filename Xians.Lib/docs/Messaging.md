# User Messaging

## Overview

The messaging system enables agents to communicate with users through two primary patterns:

1. **Reactive Messaging** - Responding to user messages via `UserMessageContext`
2. **Proactive Messaging** - Agent-initiated messages via `UserMessaging`

**Quick comparison:**

| Pattern | When to Use | API |
|---------|-------------|-----|
| **Reactive** | User sends message, agent replies | `context.ReplyAsync()` |
| **Proactive** | Agent initiates conversation | `UserMessaging.SendChatAsync()` |

---

## Reactive Messaging (Responding to Users)

When a user sends a message, your workflow receives a `UserMessageContext` containing the message and methods to reply.

### Basic Usage

```csharp
var agent = platform.Agents.Register("CustomerSupport");
var workflow = await agent.Workflows.DefineBuiltIn();

workflow.OnUserMessage(async (context) =>
{
    // Read the user's message
    var userMessage = context.Message.Text;
    
    // Reply to the user
    await context.ReplyAsync($"You said: {userMessage}");
});
```

### UserMessageContext Properties

| Property | Type | Description |
|----------|------|-------------|
| `Message` | `CurrentMessage` | The current message with text, data, and messaging operations |
| `Metadata` | `Dictionary<string, string>?` | Optional metadata for the message |

### CurrentMessage Properties (via context.Message)

| Property | Type | Description |
|----------|------|-------------|
| `Text` | `string` | The user's message text |
| `ParticipantId` | `string` | User's unique identifier |
| `RequestId` | `string` | Tracking ID for this request |
| `Scope` | `string?` | Message scope (e.g., "support", "sales") |
| `Hint` | `string?` | Processing hint from the platform |
| `ThreadId` | `string?` | Conversation thread identifier |
| `TenantId` | `string` | Tenant that initiated the workflow |
| `Data` | `object?` | Structured data attached to message |
| `Authorization` | `string?` | Authorization token, if provided |

### Sending Replies

#### Simple Text Reply

```csharp
workflow.OnUserMessage(async (context) =>
{
    await context.ReplyAsync("Hello! How can I help you today?");
});
```

#### Reply with Data

Send structured data alongside text (useful for UI rendering):

```csharp
workflow.OnUserMessage(async (context) =>
{
    var orderDetails = new
    {
        OrderId = "ORD-12345",
        Status = "Shipped",
        TrackingNumber = "1Z999AA10123456784"
    };
    
    await context.ReplyWithDataAsync("Your order has shipped!", orderDetails);
});
```

### Accessing Chat History

Retrieve previous messages in the conversation:

```csharp
workflow.OnUserMessage(async (context) =>
{
    // Get last 10 messages
    var history = await context.GetChatHistoryAsync(page: 1, pageSize: 10);
    
    foreach (var msg in history)
    {
        Console.WriteLine($"[{msg.Direction}] {msg.Text}");
    }
    
    // Build context-aware response
    var hasAskedBefore = history.Any(m => m.Text.Contains("order status"));
    
    await context.ReplyAsync(hasAskedBefore 
        ? "Checking your order again..." 
        : "Let me look up your order status.");
});
```

### Accessing Knowledge

Read agent knowledge (prompts, configs, instructions):

```csharp
workflow.OnUserMessage(async (context) =>
{
    // Get system prompt
    var systemPrompt = await context.GetKnowledgeAsync("system-prompt");
    
    if (systemPrompt != null)
    {
        // Use prompt content
        var instructions = systemPrompt.Content;
    }
    
    // List all knowledge
    var allKnowledge = await context.ListKnowledgeAsync();
    
    // Update knowledge
    await context.UpdateKnowledgeAsync("conversation-summary", 
        $"User asked about: {context.Message.Text}");
    
    await context.ReplyAsync("I've noted your question.");
});
```

### Multiple Replies

You can send multiple messages in a single handler:

```csharp
workflow.OnUserMessage(async (context) =>
{
    await context.ReplyAsync("Let me check that for you...");
    
    // Perform some work
    var result = await LongRunningOperation();
    
    await context.ReplyAsync($"Found it! {result}");
});
```

---

## Proactive Messaging (Agent-Initiated)

Use `UserMessaging` when the agent needs to send messages **without** a user message to reply to. Common scenarios:

- 🔔 **Notifications**: "Your order has shipped!"
- ⏰ **Scheduled updates**: Daily reports, reminders
- 📊 **Status changes**: Alert on workflow completion
- 🤖 **Background processing results**: "Analysis complete"

### Basic Usage

```csharp
using Xians.Lib.Agents.Messaging;

// Inside a workflow or activity
await UserMessaging.SendChatAsync(
    participantId: "user-123",
    text: "Your order has shipped!");
```

### API Reference

#### SendChatAsync

Send a chat message to a user:

```csharp
await UserMessaging.SendChatAsync(
    participantId: "user-123",          // Required: User ID
    text: "Hello!",                      // Required: Message content
    data: optionalDataObject,            // Optional: Structured data
    scope: "notifications",              // Optional: Message scope
    hint: "email"                        // Optional: Processing hint
);
```

#### SendDataAsync

Send a data message (structured data as primary content):

```csharp
await UserMessaging.SendDataAsync(
    participantId: "user-123",
    text: "Order update",
    data: new { OrderId = "123", Status = "Shipped" },
    scope: "orders"
);
```

#### SendChatAsAsync / SendDataAsAsync

Send messages while impersonating a different workflow:

```csharp
// A background workflow sends as the main chat workflow
await UserMessaging.SendChatAsAsync(
    workflowType: "MyAgent:CustomerChat",   // Workflow to impersonate
    participantId: "user-123",
    text: "Your background task completed!"
);
```

### Use Cases & Examples

#### Scheduled Notifications

```csharp
[Workflow("MyAgent:DailyDigest")]
public class DailyDigestWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        // This workflow runs on a schedule, not triggered by user message
        var users = await GetSubscribedUsers();
        
        foreach (var userId in users)
        {
            var digest = await BuildDailyDigest(userId);
            
            await UserMessaging.SendChatAsync(
                participantId: userId,
                text: $"📬 Your daily digest:\n{digest}",
                scope: "digest"
            );
        }
    }
}
```

#### Background Task Completion

```csharp
[Workflow("MyAgent:ReportGenerator")]
public class ReportGeneratorWorkflow
{
    [WorkflowRun]
    public async Task GenerateReport(ReportRequest request)
    {
        // Long-running report generation
        var report = await GenerateLargeReport(request);
        
        // Notify user when complete
        await UserMessaging.SendChatAsync(
            participantId: request.RequestedBy,
            text: $"✅ Your report is ready!",
            data: new { ReportId = report.Id, DownloadUrl = report.Url }
        );
    }
}
```

#### From Activities (AI Tools)

```csharp
public class NotificationActivities
{
    [Activity]
    public async Task SendOrderNotification(string userId, string orderId)
    {
        // Works from activities too!
        await UserMessaging.SendChatAsync(
            participantId: userId,
            text: $"Order {orderId} has been confirmed!",
            scope: "orders"
        );
    }
}
```

#### Cross-Workflow Notifications

```csharp
// A processing workflow notifies via the main chat workflow
[Workflow("MyAgent:OrderProcessor")]
public class OrderProcessorWorkflow
{
    [WorkflowRun]
    public async Task ProcessOrder(Order order)
    {
        await ProcessPayment(order);
        await ShipOrder(order);
        
        // Send notification "as" the main customer workflow
        // This ensures the message appears in the right conversation
        await UserMessaging.SendChatAsAsync(
            workflowType: "MyAgent:CustomerChat",
            participantId: order.CustomerId,
            text: $"Great news! Your order #{order.Id} has shipped! 🚚"
        );
    }
}
```

---

## Message Types

### Chat vs Data Messages

| Type | Use Case | API |
|------|----------|-----|
| **Chat** | Human-readable text | `ReplyAsync()`, `SendChatAsync()` |
| **Data** | Structured data | `ReplyWithDataAsync()`, `SendDataAsync()` |

**Chat messages** are displayed directly to users.  
**Data messages** may trigger special UI rendering or be processed by client apps.

---

## Message Scopes

Scopes help organize and route messages:

```csharp
// User message comes with a scope
workflow.OnUserMessage(async (context) =>
{
    if (context.Message.Scope == "support")
    {
        // Handle support requests
    }
    else if (context.Message.Scope == "sales")
    {
        // Handle sales inquiries
    }
});

// Proactive messages can specify scope
await UserMessaging.SendChatAsync("user-123", "Hello!", scope: "notifications");
```

Common scopes:
- `support` - Customer support conversations
- `sales` - Sales inquiries
- `notifications` - System notifications
- `orders` - Order-related messages
- `alerts` - Urgent alerts

---

## Error Handling

### In Reactive Handlers

Exceptions bubble up and can be handled by the workflow:

```csharp
workflow.OnUserMessage(async (context) =>
{
    try
    {
        var result = await ProcessRequest(context.Message.Text);
        await context.ReplyAsync(result);
    }
    catch (BusinessException ex)
    {
        await context.ReplyAsync($"Sorry, I couldn't process that: {ex.Message}");
    }
    catch (Exception ex)
    {
        await context.ReplyAsync("Something went wrong. Please try again.");
        throw; // Re-throw for logging/retry
    }
});
```

### In Proactive Messaging

```csharp
try
{
    await UserMessaging.SendChatAsync("user-123", "Notification");
}
catch (InvalidOperationException ex)
{
    // Not in workflow/activity context
    _logger.LogError(ex, "Failed to send proactive message");
}
catch (HttpRequestException ex)
{
    // Network/API error
    _logger.LogError(ex, "Failed to send message to platform");
}
```

---

## Context Requirements

### UserMessageContext

- ✅ Available in `OnUserMessage` handlers
- ✅ Contains user's message and metadata
- ✅ Provides reply methods

### UserMessaging

| Context | Available | Notes |
|---------|-----------|-------|
| **Workflow** | ✅ | Via Temporal activity |
| **Activity** | ✅ | Direct HTTP call |
| **Outside** | ❌ | Throws `InvalidOperationException` |

```csharp
// ❌ This will throw - not in workflow/activity context
public void SomeMethod()
{
    await UserMessaging.SendChatAsync("user-123", "Hello");
    // InvalidOperationException: UserMessaging can only be used 
    // within a Temporal workflow or activity context.
}

// ✅ This works - inside activity
[Activity]
public async Task NotifyUser(string userId, string message)
{
    await UserMessaging.SendChatAsync(userId, message);
}
```

---

## Multi-Tenancy

Both messaging patterns are tenant-aware:

### Reactive Messaging

Tenant is automatically extracted from the workflow context:

```csharp
workflow.OnUserMessage(async (context) =>
{
    // TenantId is available from context
    var tenant = context.Message.TenantId;
    
    // All operations are scoped to this tenant
    var knowledge = await context.GetKnowledgeAsync("tenant-config");
    
    await context.ReplyAsync($"Hello from tenant {tenant}!");
});
```

### Proactive Messaging

Tenant is extracted from `XiansContext`:

```csharp
// Inside a workflow/activity
var tenant = XiansContext.TenantId;

// Messages are automatically scoped to current tenant
await UserMessaging.SendChatAsync("user-123", "Hello!");
```

---

## Complete Example

```csharp
var platform = XiansPlatform.Create(options);
var agent = platform.Agents.Register("OrderBot");

// Main chat workflow - responds to user messages
var chatWorkflow = await agent.Workflows.DefineBuiltIn("Chat");

chatWorkflow.OnUserMessage(async (context) =>
{
    var message = context.Message.Text.ToLower();
    
    if (message.Contains("order status"))
    {
        var orders = await GetUserOrders(context.Message.ParticipantId);
        
        await context.ReplyWithDataAsync(
            "Here are your recent orders:",
            new { Orders = orders }
        );
    }
    else if (message.Contains("subscribe"))
    {
        await SubscribeToNotifications(context.Message.ParticipantId);
        await context.ReplyAsync("You're now subscribed to order updates! 🔔");
    }
    else
    {
        await context.ReplyAsync("I can help with order status and notifications.");
    }
});

// Background workflow - sends proactive notifications
var notificationWorkflow = await agent.Workflows.DefineCustom<OrderNotificationWorkflow>();

[Workflow("OrderBot:OrderNotification")]
public class OrderNotificationWorkflow
{
    [WorkflowRun]
    public async Task NotifyOrderShipped(ShipmentEvent evt)
    {
        // Proactive notification - no user message triggered this
        await UserMessaging.SendChatAsync(
            participantId: evt.CustomerId,
            text: $"📦 Great news! Your order #{evt.OrderId} has shipped!\n" +
                  $"Tracking: {evt.TrackingNumber}",
            data: new { TrackingUrl = evt.TrackingUrl },
            scope: "<guid>"
        );
    }
}

await platform.RunAsync();
```

---

## Summary

| Pattern | Class | Method | Use Case |
|---------|-------|--------|----------|
| **Reply to user** | `UserMessageContext` | `ReplyAsync()` | User sent message |
| **Reply with data** | `UserMessageContext` | `ReplyWithDataAsync()` | Include structured data |
| **Proactive chat** | `UserMessaging` | `SendChatAsync()` | Agent initiates |
| **Proactive data** | `UserMessaging` | `SendDataAsync()` | Agent sends data |
| **Send as workflow** | `UserMessaging` | `SendChatAsAsync()` | Impersonate workflow |

**Key Points:**
- 📨 **Reactive**: Use `context.ReplyAsync()` in `OnUserMessage` handlers
- 📤 **Proactive**: Use `UserMessaging.SendChatAsync()` from workflows/activities
- 🏢 **Multi-tenant**: Both patterns are tenant-aware
- ⚡ **Retries**: Built-in retry policy for reliability

---

**See also:**
- [A2A Communication](A2A.md) - Agent-to-agent messaging
- [Knowledge Guide](Knowledge.md) - Store agent knowledge
- [System-Scoped Agents](SystemScopedAgents.md) - Multi-tenant architecture
- [Getting Started](GettingStarted.md) - General Xians.Lib setup
