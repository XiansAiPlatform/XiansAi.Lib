# Knowledge in Xians.Lib 📚

> **TL;DR**: Knowledge = stuff your agents remember (prompts, configs, docs). Agents read/write it in code. Humans manage it via UI portal. It's automatically scoped to agents and tenants.

## What Is Knowledge?

Think of Knowledge as your agent's **personal filing cabinet**. It stores:

- 📝 **Instructions** - "How to handle refund requests"
- ⚙️ **Configurations** - API keys, settings, feature flags
- 📄 **Documents** - User guides, FAQs, templates
- 💬 **Prompts** - AI prompts, conversation starters
- 📊 **Data** - JSON objects, lookup tables, preferences

### The Magic ✨

```
┌─────────────────────────────────────────────────┐
│                                                 │
│  👨‍💻 Developer                🤖 Agent           │
│     │                            │              │
│     │ Code:                      │              │
│     │ UpdateAsync("greeting",    │              │
│     │   "Hello!")                │              │
│     └────────────┬───────────────┘              │
│                  ▼                               │
│     ┌────────────────────────┐                  │
│     │   Knowledge Store      │                  │
│     │   ┌──────────────────┐ │                  │
│     │   │ greeting: Hello! │ │                  │
│     │   │ refund-policy: …│ │                  │
│     │   │ api-key: sk-...  │ │                  │
│     │   └──────────────────┘ │                  │
│     └────────────┬───────────┘                  │
│                  ▼                               │
│     👤 Human                                     │
│        │                                         │
│        │ UI Portal:                              │
│        │ • View all knowledge                    │
│        │ • Edit "greeting"                       │
│        │ • Add new knowledge                     │
│        └─────────────────────                    │
│                                                  │
└──────────────────────────────────────────────────┘
```

**Both agents AND humans** can manage the same knowledge:
- **Agents** use code (`agent.Knowledge.GetAsync()`)
- **Humans** use the UI portal (no code needed!)

## Quick Start

### 1. Store Knowledge

```csharp
var platform = await XiansPlatform.InitializeAsync(new XiansOptions
{
    ServerUrl = "https://api.xians.ai",
    ApiKey = "your-api-key"
});

var agent = platform.Agents.Register(new XiansAgentRegistration 
{ 
    Name = "SupportBot" 
});

// Store a greeting template
await agent.Knowledge.UpdateAsync(
    "greeting-template",
    "Hello {name}! How can I help you today?",
    "instruction"
);

// Store configuration
await agent.Knowledge.UpdateAsync(
    "api-config",
    "{ \"endpoint\": \"https://api.example.com\", \"timeout\": 30 }",
    "json"
);
```

### 2. Retrieve Knowledge

```csharp
// Get knowledge
var greeting = await agent.Knowledge.GetAsync("greeting-template");

if (greeting != null)
{
    var message = greeting.Content.Replace("{name}", "Alice");
    Console.WriteLine(message); // "Hello Alice! How can I help you today?"
}
```

### 3. Use in Message Handlers

```csharp
var workflow = agent.Workflows.GetOrCreate("support:chat");

workflow.OnUserMessage(async (context) =>
{
    // Fetch the refund policy
    var policy = await context.GetKnowledgeAsync("refund-policy");
    
    if (context.Message.Text.Contains("refund"))
    {
        await context.ReplyAsync(policy?.Content ?? "Let me check on that...");
    }
    
    // Save user preference
    await context.UpdateKnowledgeAsync(
        $"user-{context.Message.ParticipantId}-language",
        "Spanish",
        "preference"
    );
});
```

## Knowledge Types

Organize knowledge by type for clarity:

```csharp
// 📝 Instructions - How to do something
await agent.Knowledge.UpdateAsync(
    "onboarding-steps",
    "1. Welcome user\n2. Explain features\n3. Offer tutorial",
    "instruction"
);

// ⚙️ Configuration - Settings
await agent.Knowledge.UpdateAsync(
    "feature-flags",
    "{ \"darkMode\": true, \"betaFeatures\": false }",
    "json"
);

// 📄 Documentation - Reference material
await agent.Knowledge.UpdateAsync(
    "faq",
    "# FAQ\n\n## What is Xians?\nXians is...",
    "markdown"
);

// 💬 Prompts - AI instructions
await agent.Knowledge.UpdateAsync(
    "system-prompt",
    "You are a helpful customer support agent. Be friendly and concise.",
    "prompt"
);

// 📊 Data - Lookup tables
await agent.Knowledge.UpdateAsync(
    "shipping-rates",
    "{ \"US\": 5.99, \"CA\": 7.99, \"EU\": 9.99 }",
    "json"
);
```

## Access Patterns

### Pattern 1: Agent-Level (Outside Messages)

Use for setup, background tasks, or non-message contexts:

```csharp
// During agent initialization
var agent = platform.Agents.Register(new XiansAgentRegistration 
{ 
    Name = "ChatBot" 
});

// Set up default knowledge
var defaultGreeting = await agent.Knowledge.GetAsync("greeting");
if (defaultGreeting == null)
{
    await agent.Knowledge.UpdateAsync(
        "greeting",
        "Welcome to our service!",
        "instruction"
    );
}

// List all knowledge
var allKnowledge = await agent.Knowledge.ListAsync();
Console.WriteLine($"Agent has {allKnowledge.Count} knowledge items");
```

### Pattern 2: Context-Level (Inside Messages)

Use within message handlers for workflow-safe operations:

```csharp
workflow.OnUserMessage(async (context) =>
{
    // Get instruction
    var instruction = await context.GetKnowledgeAsync("how-to-help");
    
    // Update based on conversation
    if (context.Message.Text == "!save-settings")
    {
        await context.UpdateKnowledgeAsync(
            "user-settings",
            context.Message.Metadata,
            "json"
        );
    }
    
    // Delete temporary data
    if (context.Message.Text == "!clear")
    {
        await context.DeleteKnowledgeAsync("temp-data");
    }
});
```

## Scoping & Isolation 🔒

Knowledge is automatically scoped to prevent conflicts:

```
┌────────────────────────────────────────────┐
│  Tenant: Acme Corp                        │
│  ├─ Agent: SupportBot                     │
│  │  ├─ greeting: "Hi from Acme!"         │
│  │  └─ config: { ... }                   │
│  └─ Agent: SalesBot                       │
│     ├─ greeting: "Let's talk sales!"     │
│     └─ config: { ... }                   │
└────────────────────────────────────────────┘

┌────────────────────────────────────────────┐
│  Tenant: Contoso Inc                      │
│  └─ Agent: SupportBot                     │
│     ├─ greeting: "Hi from Contoso!"      │
│     └─ config: { ... }                   │
└────────────────────────────────────────────┘
```

**Each knowledge item is scoped by:**
1. **Tenant ID** - Multi-tenant isolation
2. **Agent Name** - Per-agent storage

```csharp
// SupportBot in Acme Corp
await agent.Knowledge.UpdateAsync("greeting", "Hi from Acme!");

// SupportBot in Contoso Inc (different tenant)
await agent.Knowledge.UpdateAsync("greeting", "Hi from Contoso!");

// These are SEPARATE knowledge items! ✅
```

## CRUD Operations

### Create / Update

```csharp
// UpdateAsync creates OR updates
await agent.Knowledge.UpdateAsync(
    "config",           // Name
    "{ ... }",         // Content
    "json"             // Type (optional)
);

// Throws exception on error
```

### Read

```csharp
// Get returns null if not found
var knowledge = await agent.Knowledge.GetAsync("config");

if (knowledge == null)
{
    Console.WriteLine("Knowledge not found");
}
else
{
    Console.WriteLine($"Content: {knowledge.Content}");
    Console.WriteLine($"Type: {knowledge.Type}");
    Console.WriteLine($"Created: {knowledge.CreatedAt}");
}
```

### Delete

```csharp
// Delete returns true if deleted, false if not found
bool deleted = await agent.Knowledge.DeleteAsync("old-config");

if (deleted)
{
    Console.WriteLine("Deleted successfully");
}
else
{
    Console.WriteLine("Not found (already deleted?)");
}
```

### List

```csharp
// Get all knowledge for this agent
var allKnowledge = await agent.Knowledge.ListAsync();

foreach (var item in allKnowledge)
{
    Console.WriteLine($"{item.Name} ({item.Type}): {item.Content.Length} chars");
}
```

## Real-World Examples

### Example 1: Dynamic Prompts

```csharp
// Admin updates prompt via UI or API
await agent.Knowledge.UpdateAsync(
    "system-prompt",
    "You are a friendly assistant. Be concise and helpful.",
    "prompt"
);

// Agent uses it in every conversation
workflow.OnUserMessage(async (context) =>
{
    var prompt = await context.GetKnowledgeAsync("system-prompt");
    
    // Use prompt with AI
    var response = await aiService.GenerateResponse(
        systemPrompt: prompt?.Content,
        userMessage: context.Message.Text
    );
    
    await context.ReplyAsync(response);
});
```

### Example 2: User Preferences

```csharp
workflow.OnUserMessage(async (context) =>
{
    var userId = context.Message.ParticipantId;
    var prefKey = $"user-{userId}-theme";
    
    if (context.Message.Text.StartsWith("/theme "))
    {
        var theme = context.Message.Text.Substring(7); // "dark" or "light"
        
        // Save preference
        await context.UpdateKnowledgeAsync(prefKey, theme, "preference");
        await context.ReplyAsync($"Theme set to {theme}");
    }
    else
    {
        // Load preference
        var pref = await context.GetKnowledgeAsync(prefKey);
        var theme = pref?.Content ?? "light";
        
        // Use theme in UI
        await context.ReplyAsync($"Current theme: {theme}");
    }
});
```

### Example 3: Configuration Management

```csharp
// DevOps team updates config via UI portal
// (no code deployment needed!)

// Agent reads latest config
var configKnowledge = await agent.Knowledge.GetAsync("api-config");
var config = JsonSerializer.Deserialize<ApiConfig>(configKnowledge.Content);

var client = new HttpClient
{
    BaseAddress = new Uri(config.Endpoint),
    Timeout = TimeSpan.FromSeconds(config.Timeout)
};
```

### Example 4: A/B Testing

```csharp
// Store multiple greeting variants
await agent.Knowledge.UpdateAsync("greeting-a", "Hello! Welcome!");
await agent.Knowledge.UpdateAsync("greeting-b", "Hi there! How can I help?");
await agent.Knowledge.UpdateAsync("greeting-c", "Hey! What's up?");

workflow.OnUserMessage(async (context) =>
{
    // Random A/B test
    var variant = new[] { "a", "b", "c" }[Random.Shared.Next(3)];
    var greeting = await context.GetKnowledgeAsync($"greeting-{variant}");
    
    await context.ReplyAsync(greeting?.Content);
    
    // Track which variant was used
    await context.UpdateKnowledgeAsync(
        $"session-{context.ConversationId}-variant",
        variant,
        "experiment"
    );
});
```

## Human + Agent Collaboration

The power of Knowledge is that **both humans and agents** can manage it:

```
┌─────────────────────────────────────────────────┐
│  Morning: Human updates knowledge via UI        │
│  ┌───────────────────────────────────────┐     │
│  │ UI Portal                              │     │
│  │ • Edit "refund-policy"                │     │
│  │ • Content: "New policy: 30-day..."    │     │
│  │ • Click "Save"                         │     │
│  └───────────────────────────────────────┘     │
│                                                  │
│  Afternoon: Agent reads updated knowledge       │
│  ┌───────────────────────────────────────┐     │
│  │ Agent Code                             │     │
│  │ var policy = await GetKnowledgeAsync(  │     │
│  │   "refund-policy");                    │     │
│  │ // Returns the NEW policy! ✅          │     │
│  └───────────────────────────────────────┘     │
│                                                  │
│  Evening: Agent creates new knowledge           │
│  ┌───────────────────────────────────────┐     │
│  │ Agent Code                             │     │
│  │ await UpdateKnowledgeAsync(            │     │
│  │   "common-question-1",                 │     │
│  │   "How do I reset password?");         │     │
│  └───────────────────────────────────────┘     │
│                                                  │
│  Next Day: Human sees new knowledge in UI       │
│  ┌───────────────────────────────────────┐     │
│  │ UI Portal                              │     │
│  │ • New item: "common-question-1"       │     │
│  │ • Created by: SupportBot              │     │
│  │ • Can edit or delete it               │     │
│  └───────────────────────────────────────┘     │
└──────────────────────────────────────────────────┘
```

## Performance & Caching

Knowledge reads are **cached automatically** for better performance:

```csharp
// First call: Hits server (~15ms)
var knowledge1 = await agent.Knowledge.GetAsync("config");

// Second call: From cache (~0.1ms) ⚡
var knowledge2 = await agent.Knowledge.GetAsync("config");

// After 5 minutes: Cache expires, hits server again
var knowledge3 = await agent.Knowledge.GetAsync("config");
```

**Cache behavior:**
- ✅ GET operations are cached (5 min default)
- 🧹 Cache auto-clears on UPDATE or DELETE
- 🔧 Configurable via `XiansOptions.Cache`

See [Caching Guide](./Caching.md) for details.

## Best Practices

### ✅ Do This

```csharp
// 1. Use clear, hierarchical names
await agent.Knowledge.UpdateAsync(
    "instructions.onboarding.step-1",
    "Welcome message..."
);

// 2. Set appropriate types
await agent.Knowledge.UpdateAsync(
    "config",
    jsonString,
    "json" // ← Helps UI and humans
);

// 3. Initialize default knowledge
var greeting = await agent.Knowledge.GetAsync("greeting");
if (greeting == null)
{
    await agent.Knowledge.UpdateAsync("greeting", "Hello!");
}

// 4. Use user-scoped names for preferences
var prefKey = $"user-{userId}-theme";
await agent.Knowledge.UpdateAsync(prefKey, "dark");

// 5. Handle null results
var knowledge = await agent.Knowledge.GetAsync("config");
var content = knowledge?.Content ?? "default-value";
```

### ❌ Don't Do This

```csharp
// 1. Vague names
await agent.Knowledge.UpdateAsync("x", "data"); // What is "x"?

// 2. Blocking calls
var knowledge = agent.Knowledge.GetAsync("config").Result; // Use await!

// 3. Storing secrets in plain text
await agent.Knowledge.UpdateAsync("password", "secret123"); // Use secure storage!

// 4. Extremely large content
await agent.Knowledge.UpdateAsync("data", hugeMegabyteString); // Break it up!

// 5. Assuming knowledge exists
var knowledge = await agent.Knowledge.GetAsync("config");
var value = knowledge.Content; // ❌ Null reference if not found!
```

## Naming Conventions

Use hierarchical dot notation for organization:

```csharp
// Instructions
"instructions.onboarding.welcome"
"instructions.onboarding.step-1"
"instructions.support.refund-policy"

// Configuration
"config.api.endpoint"
"config.api.timeout"
"config.features.darkmode"

// Templates
"templates.email.welcome"
"templates.email.password-reset"
"templates.chat.greeting"

// User data
"user-{userId}.preferences.theme"
"user-{userId}.preferences.language"
"user-{userId}.history.last-interaction"

// Prompts
"prompts.system"
"prompts.fallback"
"prompts.greeting"
```

## Troubleshooting

### "Knowledge not found" (Returns null)

```csharp
var knowledge = await agent.Knowledge.GetAsync("config");
if (knowledge == null)
{
    // Not an error - just doesn't exist yet
    Console.WriteLine("Knowledge 'config' not found");
    
    // See what exists
    var all = await agent.Knowledge.ListAsync();
    Console.WriteLine($"Available: {string.Join(", ", all.Select(k => k.Name))}");
}
```

### "HTTP service is not available"

**Cause:** Agent not initialized through `XiansPlatform`

**Fix:**
```csharp
// ✅ Correct
var platform = await XiansPlatform.InitializeAsync(options);
var agent = platform.Agents.Register(...);

// ❌ Wrong
var agent = new XiansAgent(...); // Don't do this!
```

### "403 Forbidden"

**Cause:** Agent not registered on server

**Fix:**
```csharp
// Register agent first
var agent = platform.Agents.Register(new XiansAgentRegistration 
{ 
    Name = "MyAgent" 
});

// Define workflow (triggers server registration)
var workflow = await agent.Workflows.DefineBuiltIn();

// Now knowledge operations work
await agent.Knowledge.UpdateAsync("test", "data");
```

## Knowledge Data Model

```csharp
public class Knowledge
{
    public string? Id { get; set; }              // Server-assigned ID
    public string Name { get; set; }             // Unique name (within agent/tenant)
    public string? Version { get; set; }         // Optional version
    public string Content { get; set; }          // The actual content
    public string? Type { get; set; }            // instruction, json, markdown, etc.
    public DateTime CreatedAt { get; set; }      // Server timestamp
    public string? Agent { get; set; }           // Agent name (auto-set)
    public string? TenantId { get; set; }        // Tenant ID (auto-set)
}
```

## API Reference

### Agent-Level API

```csharp
// Get knowledge by name
Task<Knowledge?> GetAsync(
    string knowledgeName,
    CancellationToken cancellationToken = default
)

// Create or update knowledge
Task UpdateAsync(
    string knowledgeName, 
    string content, 
    string? type = null,
    CancellationToken cancellationToken = default
)

// Delete knowledge
Task<bool> DeleteAsync(
    string knowledgeName,
    CancellationToken cancellationToken = default
)

// List all knowledge
Task<List<Knowledge>> ListAsync(
    CancellationToken cancellationToken = default
)
```

### Context-Level API (Inside Message Handlers)

```csharp
// Get knowledge (workflow-safe)
Task<Knowledge?> GetKnowledgeAsync(string knowledgeName)

// Update knowledge (workflow-safe)
Task<bool> UpdateKnowledgeAsync(
    string knowledgeName, 
    string content, 
    string? type = null
)

// Delete knowledge (workflow-safe)
Task<bool> DeleteKnowledgeAsync(string knowledgeName)

// List knowledge (workflow-safe)
Task<List<Knowledge>> ListKnowledgeAsync()
```

## Limits & Constraints

| Item | Limit | Notes |
|------|-------|-------|
| Name length | 256 chars | Keep names concise |
| Content size | 10 MB | Consider breaking up large content |
| Type length | 50 chars | Standard types: instruction, json, markdown, text |
| List operations | No pagination | All knowledge returned (for now) |
| Rate limit | 100 req/min | Per agent |

## Migration from XiansAi.Lib.Src

If you're migrating from the old SDK:

**Old Code:**
```csharp
using XiansAi.Knowledge;

var knowledge = await KnowledgeHub.Fetch("instruction");
await KnowledgeHub.Update("name", "type", "content");
```

**New Code:**
```csharp
using Xians.Lib.Agents;

// Outside workflows
var knowledge = await agent.Knowledge.GetAsync("instruction");
await agent.Knowledge.UpdateAsync("name", "content", "type");

// Inside workflows
var knowledge = await context.GetKnowledgeAsync("instruction");
await context.UpdateKnowledgeAsync("name", "content", "type");
```

## Summary

- 📚 **Shared Storage** - Both agents and humans manage knowledge
- 🔒 **Automatically Scoped** - Per-agent, per-tenant isolation
- ⚡ **Fast** - Cached by default (~150x speedup)
- 🔧 **Flexible** - Store any text: prompts, configs, docs, data
- 🎯 **Simple API** - Get, Update, Delete, List
- 🚀 **Production-Ready** - Tested and reliable

**Use cases:**
- Store AI prompts that humans can edit via UI
- Save user preferences and settings
- Manage configuration without redeploying code
- Share instructions between agent and support team
- Track conversation history and context

Knowledge makes your agents smarter and your humans more productive! 🎉

