# Agent-to-Agent (A2A) Communication

## What is A2A?

Enable your agents to talk to each other! A2A provides fast, in-memory messaging between agents in the same runtime - perfect for building sophisticated multi-agent workflows.

**In 3 lines of code:**
```csharp
var webWorkflow = XiansContext.CurrentAgent.GetBuiltInWorkflow("Web");
var client = new A2AClient(webWorkflow);
var response = await client.SendMessageAsync(new A2AMessage { Text = "Research Acme Corp" });
```

## Quick Start

### Setup Multiple Workflows

```csharp
var agent = platform.Agents.Register("ResearchAgent", systemScoped: true);

// Create workflows for different capabilities
var supervisorFlow = await agent.Workflows.DefineBuiltIn("Supervisor");
var webFlow = await agent.Workflows.DefineBuiltIn("Web");

// Supervisor orchestrates, Web executes
supervisorFlow.OnUserMessage(async (context) =>
{
    var webWorkflow = XiansContext.CurrentAgent.GetBuiltInWorkflow("Web");
    var client = new A2AClient(webWorkflow);
    
    var result = await client.SendMessageAsync(new A2AMessage
    {
        Text = "Research company: " + context.Message.Text
    });
    
    await context.ReplyAsync(result.Text);
});

webFlow.OnUserMessage(async (context) =>
{
    var research = await PerformWebSearch(context.Message.Text);
    await context.ReplyAsync(research);
});
```

## Core API

### A2AClient

```csharp
var client = new A2AClient(targetWorkflow);
var response = await client.SendMessageAsync(message);
```

### A2AMessage

```csharp
new A2AMessage
{
    Text = "Your request",           // Required
    Data = optionalDataObject,       // Optional structured data
    Metadata = metadataDict          // Optional (caller → handler only)
}
```

### A2AMessageContext

Detect A2A messages in your handler:

```csharp
workflow.OnUserMessage(async (context) =>
{
    if (context is A2AMessageContext a2a)
    {
        Console.WriteLine($"A2A from {a2a.SourceAgentName}");
    }
    
    await context.ReplyAsync("Done");
});
```

### XiansContext

Static access to agents from anywhere:

```csharp
var agent = XiansContext.CurrentAgent;              // Current agent
var other = XiansContext.GetAgent("WebAgent");      // Any agent
var workflow = agent.GetBuiltInWorkflow("Web");     // Any workflow
```

## Common Patterns

### Pattern 1: Sequential Agent Chain

```csharp
// Step 1: Research
var researchAgent = XiansContext.GetAgent("ResearchAgent");
var research = await new A2AClient(researchAgent.GetBuiltInWorkflow())
    .SendMessageAsync(new A2AMessage { Text = "Research Acme Corp" });

// Step 2: Analyze
var analysisAgent = XiansContext.GetAgent("AnalysisAgent");
var analysis = await new A2AClient(analysisAgent.GetBuiltInWorkflow())
    .SendMessageAsync(new A2AMessage 
    { 
        Text = "Analyze", 
        Data = research.Data 
    });

// Step 3: Report
await context.ReplyAsync(analysis.Text);
```

### Pattern 2: Parallel Agent Calls

```csharp
var tasks = new[]
{
    new A2AClient(newsAgent.GetBuiltInWorkflow())
        .SendMessageAsync(new A2AMessage { Text = "Latest news" }),
    new A2AClient(weatherAgent.GetBuiltInWorkflow())
        .SendMessageAsync(new A2AMessage { Text = "Weather" }),
    new A2AClient(stockAgent.GetBuiltInWorkflow())
        .SendMessageAsync(new A2AMessage { Text = "Stocks" })
};

var responses = await Task.WhenAll(tasks);
var summary = string.Join("\n", responses.Select(r => r.Text));
```

### Pattern 3: A2A from AI Tools

```csharp
[Description("Research a company")]
public static async Task<string> ResearchCompany(string companyName)
{
    // Works from activities/tools too!
    var webWorkflow = XiansContext.CurrentAgent.GetBuiltInWorkflow("Web");
    var client = new A2AClient(webWorkflow);
    
    var response = await client.SendMessageAsync(new A2AMessage
    {
        Text = $"Research: {companyName}"
    });
    
    return response.Text;
}
```

### Pattern 4: Conditional Routing

```csharp
XiansWorkflow? target = context.Message.Text.ToLower() switch
{
    var m when m.Contains("weather") => GetAgent("WeatherAgent").GetBuiltInWorkflow(),
    var m when m.Contains("news") => GetAgent("NewsAgent").GetBuiltInWorkflow(),
    _ => null
};

if (target != null)
{
    var response = await new A2AClient(target).SendMessageAsync(new A2AMessage 
    { 
        Text = context.Message.Text 
    });
    await context.ReplyAsync(response.Text);
}
```

## What You Need to Know

### ✅ What Works

- **Fast & Simple**: Direct function call, ~1-5ms latency
- **Context-Aware**: Works from workflows AND activities
- **Full Handler API**: Knowledge, data, everything works
- **Metadata**: Pass tracking info, priorities, routing hints

### ❌ Key Limitations

| Limitation | Impact | Workaround |
|------------|--------|------------|
| **No Recording** | A2A not in DB | Manually log if needed |
| **No History** | Empty chat history | Pass context in `Data` field |
| **No Temporal UI** | Can't see in Temporal | Use logs for debugging |
| **Same Runtime** | One process only | Use platform API for cross-process |
| **No Timeout** | Waits indefinitely | Wrap with `Task.WhenAny` if needed |

### 🔍 How Metadata Works

```csharp
// Caller sets metadata
var response = await client.SendMessageAsync(new A2AMessage
{
    Text = "Process",
    Metadata = new Dictionary<string, string>
    {
        ["priority"] = "high",
        ["requestId"] = "req-123"
    }
});

// Handler reads metadata (read-only)
if (context is A2AMessageContext a2a)
{
    var priority = a2a.Metadata?["priority"];  // "high"
}

// Response contains original request metadata
Console.WriteLine(response.Metadata["requestId"]);  // "req-123"
```

**Note:** Handler can READ metadata but cannot modify it. Response returns original request metadata.

## Advanced: Prevent Infinite Loops

```csharp
var depth = (context as A2AMessageContext)?.Metadata?.ContainsKey("depth") == true 
    ? int.Parse((context as A2AMessageContext).Metadata["depth"]) 
    : 0;

if (depth > 5)
{
    await context.ReplyAsync("Max depth reached");
    return;
}

var response = await client.SendMessageAsync(new A2AMessage
{
    Text = "Continue",
    Metadata = new Dictionary<string, string> { ["depth"] = (depth + 1).ToString() }
});
```

## Error Handling

```csharp
try
{
    var response = await client.SendMessageAsync(message);
}
catch (KeyNotFoundException)
{
    // Agent/workflow not found
}
catch (InvalidOperationException ex)
{
    // Handler failed: ex.Message contains the error
}
```

Handler exceptions bubble up to caller automatically.

## When to Use A2A

### ✅ Perfect For:
- Multiple workflows in one agent (coordinator pattern)
- Fast agent collaboration (same runtime)
- Workflow orchestration
- AI tool chains

### ❌ Not For:
- Cross-process communication → Use platform API
- Audit requirements → Use user messages
- Temporal observability → Use child workflows
- Long-running ops → Use background workflows

## Comparison Table

| Feature | A2A | User Messages | Child Workflows |
|---------|-----|---------------|-----------------|
| Speed | ⚡ 1-5ms | 🐌 100ms+ | 🐌 100ms+ |
| Recording | ❌ | ✅ | ✅ |
| History | ❌ | ✅ | ✅ |
| Temporal UI | ❌ | ✅ | ✅ |
| Same Runtime | Required | Optional | Optional |
| Complexity | ✅ Simple | ⚠️ Medium | ⚠️ Complex |

## Architecture

```
User Message
    ↓
SupervisorWorkflow.OnUserMessage
    ↓
    ├─→ A2AClient.SendMessageAsync()
    │       ↓
    │   Direct function call (in-memory)
    │       ↓
    │   WebWorkflow.OnUserMessage (handler)
    │       ↓
    │   context.ReplyAsync()
    │       ↓
    │   Response captured
    ├─← Return response
    ↓
await context.ReplyAsync() → User
```

**No signals, no queues, no network** - just a direct function call.

## Security

### Tenant Isolation

System-scoped agents: Tenant flows from source → target  
Tenant-scoped agents: Must be same tenant

### Access Control

**Built-in:** None. Any agent can call any other.

**Recommendation:** Validate in handler:
```csharp
if (context is A2AMessageContext a2a && a2a.SourceAgentName != "TrustedAgent")
{
    throw new UnauthorizedAccessException();
}
```

## Complete Example

```csharp
// Setup
var agent = platform.Agents.Register("CompanyResearch", systemScoped: true);
var coordFlow = await agent.Workflows.DefineBuiltIn("Coordinator");
var webFlow = await agent.Workflows.DefineBuiltIn("Web");
var analysisFlow = await agent.Workflows.DefineBuiltIn("Analysis");

// Coordinator orchestrates
coordFlow.OnUserMessage(async (context) =>
{
    // Step 1: Web research
    var webResult = await new A2AClient(XiansContext.CurrentAgent.GetBuiltInWorkflow("Web"))
        .SendMessageAsync(new A2AMessage 
        { 
            Text = "Research: " + context.Message.Text,
            Metadata = new Dictionary<string, string> { ["priority"] = "high" }
        });
    
    // Step 2: Analysis
    var analysisResult = await new A2AClient(XiansContext.CurrentAgent.GetBuiltInWorkflow("Analysis"))
        .SendMessageAsync(new A2AMessage 
        { 
            Text = "Analyze",
            Data = webResult.Data 
        });
    
    await context.ReplyAsync(analysisResult.Text);
});

// Web workflow
webFlow.OnUserMessage(async (context) =>
{
    if (context is A2AMessageContext a2a && a2a.Metadata?["priority"] == "high")
    {
        // Priority processing
    }
    
    var data = await FetchWebData(context.Message.Text);
    await context.ReplyWithDataAsync("Found data", data);
});

// Analysis workflow
analysisFlow.OnUserMessage(async (context) =>
{
    var webData = context.Data;  // From previous A2A call
    var analysis = await AnalyzeData(webData);
    await context.ReplyAsync(analysis);
});
```

## Summary

A2A = **Fast agent collaboration** for same-runtime scenarios.

**Think of it as:**
- 🚀 Function call between agents
- 📞 Request-response pattern
- 🎯 Orchestration tool

**Remember:**
- No recording (ephemeral)
- No history (stateless)
- Same runtime only
- Handler exceptions bubble up

**Perfect for:** Multi-workflow orchestration, agent coordination, AI tool chains

---

**See also:**
- [Design Details](Agent2Agent.md) - Architecture and implementation
- [Getting Started](GettingStarted.md) - General Xians.Lib setup
