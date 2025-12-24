# Knowledge SDK - Developer Guide

## Overview

The Knowledge SDK provides agents with the ability to manage knowledge items (instructions, documents, configuration, etc.) that are automatically scoped to the agent and tenant.

## Features

- ✅ **Automatic Scoping**: Knowledge is automatically scoped to agent name and tenant ID
- ✅ **Dual Access Patterns**: Access knowledge both inside and outside message handlers
- ✅ **Workflow-Safe**: Works seamlessly in Temporal workflows and activities
- ✅ **Type Support**: Store different types of knowledge (instructions, JSON, markdown, text)
- ✅ **CRUD Operations**: Get, Update, Delete, and List knowledge
- ✅ **Automatic Retries**: Built-in retry logic for network operations

## Architecture

The SDK provides two access patterns:

1. **Agent-Level Access** (`agent.Knowledge.*`) - For use outside message handlers
2. **Context-Level Access** (`context.*KnowledgeAsync()`) - For use inside message handlers

Both patterns automatically handle:
- Tenant isolation
- Agent scoping
- Workflow vs non-workflow execution
- HTTP communication with the server

## Usage

### 1. Agent-Level Access (Outside Message Handlers)

Use this pattern for initialization, background tasks, or any non-message context:

```csharp
using Xians.Lib.Agents;

// Initialize platform
var options = new XiansOptions
{
    ServerUrl = "https://api.xians.ai",
    ApiKey = "your-api-key"
};

var platform = await XiansPlatform.InitializeAsync(options);
var agent = await platform.Agents.RegisterAsync("my-agent");

// Get knowledge
var instruction = await agent.Knowledge.GetAsync("user-guide");
if (instruction != null)
{
    Console.WriteLine($"Content: {instruction.Content}");
    Console.WriteLine($"Type: {instruction.Type}");
}

// Update/Create knowledge
await agent.Knowledge.UpdateAsync(
    "configuration", 
    "{ \"theme\": \"dark\" }", 
    "json");

// Delete knowledge
bool deleted = await agent.Knowledge.DeleteAsync("old-instruction");

// List all knowledge for this agent
var allKnowledge = await agent.Knowledge.ListAsync();
foreach (var item in allKnowledge)
{
    Console.WriteLine($"{item.Name}: {item.Type}");
}
```

### 2. Context-Level Access (Inside Message Handlers)

Use this pattern when handling user messages in workflows:

```csharp
var workflow = agent.Workflows.GetOrCreate("my-agent:chat");

workflow.OnUserMessage(async (context) =>
{
    // Get knowledge
    var instruction = await context.GetKnowledgeAsync("greeting-template");
    
    if (instruction != null)
    {
        var greeting = instruction.Content.Replace("{name}", "User");
        await context.ReplyAsync(greeting);
    }
    
    // Save user preference as knowledge
    await context.UpdateKnowledgeAsync(
        $"user-{context.ParticipantId}-preference",
        context.Message.Text,
        "preference");
    
    // List knowledge (for admin commands)
    if (context.Message.Text == "!list-knowledge")
    {
        var knowledge = await context.ListKnowledgeAsync();
        await context.ReplyAsync($"Found {knowledge.Count} knowledge items");
    }
    
    // Delete knowledge
    if (context.Message.Text.StartsWith("!delete "))
    {
        var name = context.Message.Text.Substring(8);
        bool deleted = await context.DeleteKnowledgeAsync(name);
        await context.ReplyAsync(deleted ? "Deleted" : "Not found");
    }
});
```

### 3. Advanced: Knowledge Types

The SDK supports different knowledge types for better organization:

```csharp
// Instruction
await agent.Knowledge.UpdateAsync(
    "onboarding-steps", 
    "1. Welcome\n2. Setup\n3. Tutorial", 
    "instruction");

// JSON Configuration
await agent.Knowledge.UpdateAsync(
    "api-config",
    "{ \"endpoint\": \"https://api.example.com\" }",
    "json");

// Markdown Documentation
await agent.Knowledge.UpdateAsync(
    "help-doc",
    "# Help\n\nThis is a help document.",
    "markdown");

// Plain Text
await agent.Knowledge.UpdateAsync(
    "note",
    "Remember to check logs daily",
    "text");
```

### 4. Error Handling

All knowledge methods throw exceptions on errors:

```csharp
try
{
    var knowledge = await agent.Knowledge.GetAsync("config");
    if (knowledge == null)
    {
        // Knowledge not found - this is NOT an error
        Console.WriteLine("Knowledge does not exist");
    }
}
catch (HttpRequestException ex)
{
    // Network or server error
    Console.WriteLine($"Failed to fetch knowledge: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    // SDK configuration error (e.g., HTTP service not available)
    Console.WriteLine($"SDK error: {ex.Message}");
}
```

### 5. Knowledge Model

The `Knowledge` class contains:

```csharp
public class Knowledge
{
    public string? Id { get; set; }              // Server-assigned ID
    public required string Name { get; set; }     // Unique name (within agent/tenant)
    public string? Version { get; set; }          // Optional version
    public required string Content { get; set; }  // The actual knowledge content
    public string? Type { get; set; }             // Type: instruction, json, markdown, text
    public DateTime CreatedAt { get; set; }       // Server-assigned timestamp
    public string? Agent { get; set; }            // Agent name (auto-populated)
    public string? TenantId { get; set; }         // Tenant ID (auto-populated)
}
```

## Best Practices

### 1. Naming Conventions

Use clear, hierarchical naming:

```csharp
// Good
"user-onboarding-step-1"
"config.api.endpoint"
"template.greeting.morning"

// Avoid
"x"
"temp"
"abc123"
```

### 2. Knowledge Lifecycle

```csharp
// Initialize knowledge on agent startup
var workflow = agent.Workflows.GetOrCreate("my-agent:chat");
await workflow.RunAsync();

// Before running, set up default knowledge
var defaultInstruction = await agent.Knowledge.GetAsync("default-instruction");
if (defaultInstruction == null)
{
    await agent.Knowledge.UpdateAsync(
        "default-instruction",
        "Welcome! I'm here to help.",
        "instruction");
}
```

### 3. User-Specific Knowledge

Store user preferences with user ID in the name:

```csharp
workflow.OnUserMessage(async (context) =>
{
    var userId = context.ParticipantId;
    var prefKey = $"user-{userId}-theme";
    
    // Save preference
    await context.UpdateKnowledgeAsync(prefKey, "dark", "preference");
    
    // Retrieve preference
    var pref = await context.GetKnowledgeAsync(prefKey);
});
```

### 4. Versioning Knowledge

Use the `Version` field for version tracking:

```csharp
var knowledge = await agent.Knowledge.GetAsync("api-schema");
if (knowledge != null && knowledge.Version != "2.0")
{
    // Update to new version
    await agent.Knowledge.UpdateAsync(
        "api-schema",
        newSchemaContent,
        "json");
}
```

### 5. Large Content

For large content, consider:
- Breaking into smaller knowledge items
- Using external storage and storing references
- Compressing content before storing

```csharp
// Instead of one large item:
// BAD: await agent.Knowledge.UpdateAsync("all-docs", hugContent);

// Break into smaller items:
// GOOD:
await agent.Knowledge.UpdateAsync("docs-intro", introContent);
await agent.Knowledge.UpdateAsync("docs-api", apiContent);
await agent.Knowledge.UpdateAsync("docs-examples", examplesContent);
```

## Scoping & Isolation

### Automatic Scoping

Knowledge is automatically scoped to:
1. **Agent Name**: Only the agent that created it can access it
2. **Tenant ID**: Enforces multi-tenancy isolation

```csharp
// Agent "bot-1" in tenant "acme-corp"
await agent.Knowledge.UpdateAsync("config", "data");
// Stored as: name="config", agent="bot-1", tenantId="acme-corp"

// Agent "bot-2" in tenant "acme-corp" CANNOT access this knowledge
// Agent "bot-1" in tenant "other-corp" CANNOT access this knowledge
```

### System-Scoped Agents

For system-scoped agents, tenant context is determined by the workflow:

```csharp
// System-scoped agent serves multiple tenants
var agent = await platform.Agents.RegisterSystemScopedAsync("global-bot");

var workflow = agent.Workflows.GetOrCreate("global-bot:chat");
workflow.OnUserMessage(async (context) =>
{
    // context.TenantId automatically set from the workflow initiator
    var knowledge = await context.GetKnowledgeAsync("greeting");
    // Retrieves greeting for THIS tenant only
});
```

## Performance Considerations

### Caching

The SDK does NOT implement caching. The server may cache knowledge:
- Cache TTL: ~5 minutes (server-side)
- Cache invalidation: Automatic on updates/deletes

### Rate Limits

Be mindful of rate limits:
- Recommended: Max 100 requests/minute per agent
- Consider batching operations when possible

### Async Operations

All knowledge operations are async:

```csharp
// ✅ Good - use await
var knowledge = await agent.Knowledge.GetAsync("config");

// ❌ Bad - blocking
var knowledge = agent.Knowledge.GetAsync("config").Result;
```

## Migration from XiansAi.Lib.Src

If migrating from the old `XiansAi.Lib.Src`:

**Old Pattern:**
```csharp
using XiansAi.Knowledge;

var knowledge = await KnowledgeHub.Fetch("instruction-name");
await KnowledgeHub.Update("name", "type", "content");
```

**New Pattern:**
```csharp
using Xians.Lib.Agents;

// Outside workflow
var knowledge = await agent.Knowledge.GetAsync("instruction-name");
await agent.Knowledge.UpdateAsync("name", "content", "type");

// Inside workflow
var knowledge = await context.GetKnowledgeAsync("instruction-name");
await context.UpdateKnowledgeAsync("name", "content", "type");
```

## Troubleshooting

### "HTTP service is not available"

**Cause**: Agent wasn't registered through `XiansPlatform`

**Fix**:
```csharp
// ✅ Correct
var platform = await XiansPlatform.InitializeAsync(options);
var agent = await platform.Agents.RegisterAsync("my-agent");
await agent.Knowledge.GetAsync("test");

// ❌ Wrong - don't create XiansAgent manually
```

### Knowledge Not Found (Returns null)

**Causes**:
1. Knowledge doesn't exist (expected behavior)
2. Wrong agent name
3. Wrong tenant context

**Debug**:
```csharp
var knowledge = await agent.Knowledge.GetAsync("test");
if (knowledge == null)
{
    Console.WriteLine($"Knowledge 'test' not found for agent '{agent.Name}'");
    
    // Check what exists
    var all = await agent.Knowledge.ListAsync();
    Console.WriteLine($"Available: {string.Join(", ", all.Select(k => k.Name))}");
}
```

### "Failed to fetch knowledge" Exception

**Causes**:
1. Network error
2. Server error
3. Authentication failed
4. Permission denied

**Fix**: Check server logs and ensure API key is valid

## API Reference

### KnowledgeCollection Methods

```csharp
// Get knowledge by name
Task<Knowledge?> GetAsync(string knowledgeName)

// Update or create knowledge
Task<bool> UpdateAsync(string knowledgeName, string content, string? type = null)

// Delete knowledge
Task<bool> DeleteAsync(string knowledgeName)

// List all knowledge for this agent
Task<List<Knowledge>> ListAsync()
```

### UserMessageContext Knowledge Methods

```csharp
// Get knowledge (workflow-safe)
Task<Knowledge?> GetKnowledgeAsync(string knowledgeName)

// Update knowledge (workflow-safe)
Task<bool> UpdateKnowledgeAsync(string knowledgeName, string content, string? type = null)

// Delete knowledge (workflow-safe)
Task<bool> DeleteKnowledgeAsync(string knowledgeName)

// List knowledge (workflow-safe)
Task<List<Knowledge>> ListKnowledgeAsync()
```

## See Also

- [Server API Documentation](./KnowledgeAPI.md) - Required server endpoints
- [Agent Documentation](./README.md) - General agent SDK guide
- [Workflow Documentation](./WorkerRegistration.md) - Workflow patterns

