# Local Mode for Unit Testing

This document explains how to use Xians.Lib in **Local Mode** for unit testing without requiring a live server.

## Overview

Local Mode allows you to run unit tests without:
- Making HTTP calls to the Xians server
- Connecting to Temporal
- Having network connectivity

All knowledge and data are resolved from **embedded resources** in your test assemblies.

## Quick Start

### 1. Setup Your Test Project

Add embedded resources to your test project (`.csproj`):

```xml
<ItemGroup>
  <!-- Embed knowledge files -->
  <EmbeddedResource Include="TestData\**\*.md" />
  <EmbeddedResource Include="TestData\**\*.json" />
  <EmbeddedResource Include="TestData\**\*.txt" />
</ItemGroup>
```

### 2. Create Knowledge Files

Follow the naming convention: `{AgentName}.Knowledge.{KnowledgeName}.{extension}`

Example file structure:
```
TestData/
├── MyAgent.Knowledge.system-prompt.md
├── MyAgent.Knowledge.instructions.txt
└── MyAgent.Knowledge.config.json
```

**Example: `TestData/MyAgent.Knowledge.system-prompt.md`**
```markdown
You are a helpful AI assistant for the MyAgent system.
Always be polite and professional.
```

### 3. Initialize Local Mode in Tests

**Option A: Using XiansContext (simplest)**
```csharp
using Xians.Lib.Agents.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class MyAgentTests
{
    [TestInitialize]
    public async Task Setup()
    {
        await XiansContext.StartLocalAsync(options =>
        {
            // Optional: specify which assemblies to search
            options.LocalModeAssemblies = new[] { typeof(MyAgentTests).Assembly };
        });
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Clean up registries between tests
        XiansContext.CleanupForTests();
    }
}
```

**Option B: Using XiansPlatform (more control)**
```csharp
[TestClass]
public class MyAgentTests
{
    private XiansPlatform? _platform;

    [TestInitialize]
    public void Setup()
    {
        _platform = XiansPlatform.StartLocal(options =>
        {
            options.LocalModeAssemblies = new[] { typeof(MyAgentTests).Assembly };
        });
    }

    [TestCleanup]
    public void Cleanup()
    {
        XiansContext.CleanupForTests();
    }
}
```

### 4. Write Tests

```csharp
[TestMethod]
public async Task TestKnowledgeRetrieval()
{
    // Register agent
    var agent = _platform!.Agents.Register("MyAgent", config => 
    {
        config.Name = "MyAgent";
    });

    // Get knowledge - loads from embedded resource: MyAgent.Knowledge.system-prompt.md
    var knowledge = await agent.Knowledge.GetAsync("system-prompt");

    // Assert
    Assert.IsNotNull(knowledge);
    Assert.AreEqual("system-prompt", knowledge.Name);
    Assert.AreEqual("markdown", knowledge.Type);
    Assert.IsTrue(knowledge.Content.Contains("helpful AI assistant"));
}

[TestMethod]
public async Task TestKnowledgeUpdate()
{
    var agent = _platform!.Agents.Register("MyAgent", config => 
    {
        config.Name = "MyAgent";
    });

    // Update knowledge (stored in-memory)
    await agent.Knowledge.UpdateAsync(
        "dynamic-prompt",
        "This is dynamically created knowledge",
        "text");

    // Retrieve it
    var knowledge = await agent.Knowledge.GetAsync("dynamic-prompt");

    // Assert
    Assert.IsNotNull(knowledge);
    Assert.AreEqual("This is dynamically created knowledge", knowledge.Content);
}

[TestMethod]
public async Task TestKnowledgeList()
{
    var agent = _platform!.Agents.Register("MyAgent", config => 
    {
        config.Name = "MyAgent";
    });

    // Add some knowledge
    await agent.Knowledge.UpdateAsync("prompt1", "Content 1", "text");
    await agent.Knowledge.UpdateAsync("prompt2", "Content 2", "text");

    // List knowledge
    var knowledgeList = await agent.Knowledge.ListAsync();

    // Assert
    Assert.IsTrue(knowledgeList.Count >= 2);
}
```

## Naming Conventions

### Knowledge Resources

Format: `{AgentName}.Knowledge.{KnowledgeName}.{extension}`

Examples:
- `MyAgent.Knowledge.system-prompt.md` → Name: `system-prompt`, Type: `markdown`
- `MyAgent.Knowledge.instructions.txt` → Name: `instructions`, Type: `text`
- `MyAgent.Knowledge.config.json` → Name: `config`, Type: `json`

### Supported Extensions

| Extension | Inferred Type |
|-----------|--------------|
| `.md`     | `markdown`   |
| `.txt`    | `text`       |
| `.json`   | `json`       |
| `.yaml`   | `yaml`       |
| `.yml`    | `yaml`       |

## How It Works

### Provider Pattern

Local Mode uses a provider pattern:

```
┌─────────────────┐
│ KnowledgeService│
└────────┬────────┘
         │
         ├──► IKnowledgeProvider
         │
         ├──► ServerKnowledgeProvider (HTTP calls)
         │
         └──► LocalKnowledgeProvider (Embedded resources)
```

When `LocalMode = true`:
1. **Get Operations**: Search embedded resources first, then in-memory store
2. **Update Operations**: Store in-memory only (not persisted)
3. **Delete Operations**: Remove from in-memory store
4. **List Operations**: Return in-memory items

When `LocalMode = false`:
1. All operations make HTTP calls to the server
2. Caching is used when available

## Advanced Usage

### Multiple Agents

```csharp
[TestMethod]
public async Task TestMultipleAgents()
{
    var agent1 = _platform!.Agents.Register("Agent1", config => {});
    var agent2 = _platform!.Agents.Register("Agent2", config => {});

    // Each agent can have its own knowledge
    // Agent1.Knowledge.prompt1.md
    // Agent2.Knowledge.prompt1.md
    
    var k1 = await agent1.Knowledge.GetAsync("prompt1");
    var k2 = await agent2.Knowledge.GetAsync("prompt1");
    
    // Both can exist independently
}
```

### System-Scoped Knowledge

```csharp
[TestMethod]
public async Task TestSystemKnowledge()
{
    var agent = _platform!.Agents.Register("MyAgent", config => 
    {
        config.SystemScoped = true;
    });

    // This will look for system-scoped embedded resources
    var knowledge = await agent.Knowledge.GetAsync("global-config");
    
    Assert.IsNotNull(knowledge);
    Assert.IsTrue(knowledge.SystemScoped);
}
```

### Custom Assembly Search

```csharp
var platform = XiansPlatform.StartLocal(options =>
{
    // Only search specific assemblies (improves performance)
    options.LocalModeAssemblies = new[]
    {
        typeof(MyAgentTests).Assembly,
        typeof(SharedTestData).Assembly
    };
});
```

## Best Practices

### 1. Organize Test Data

```
YourTestProject/
├── TestData/
│   ├── Agent1/
│   │   ├── Agent1.Knowledge.prompt.md
│   │   └── Agent1.Knowledge.config.json
│   └── Agent2/
│       └── Agent2.Knowledge.prompt.md
└── Tests/
    ├── Agent1Tests.cs
    └── Agent2Tests.cs
```

### 2. Use Cleanup

Always call `XiansContext.CleanupForTests()` in `[TestCleanup]` to prevent test contamination:

```csharp
[TestCleanup]
public void Cleanup()
{
    XiansContext.CleanupForTests();
}
```

### 3. Test Both Modes

Test your code in both Local Mode and Server Mode:

```csharp
[TestMethod]
public async Task TestAgainstRealServer()
{
    // Use real server for integration tests
    var platform = await XiansPlatform.InitializeAsync(new XiansOptions
    {
        ServerUrl = "https://your-server.com",
        ApiKey = "your-api-key"
    });
    
    // ... test with real server
}

[TestMethod]
public async Task TestLocalMode()
{
    // Use local mode for fast unit tests
    await XiansContext.StartLocalAsync();
    
    // ... test with embedded resources
}
```

## Troubleshooting

### Knowledge Not Found

**Problem**: `GetAsync` returns `null` even though you have an embedded resource.

**Solutions**:
1. Check naming convention: `{AgentName}.Knowledge.{KnowledgeName}.{ext}`
2. Verify file is marked as `<EmbeddedResource>` in `.csproj`
3. Check that the file is in the correct assembly
4. Verify agent name matches exactly (case-sensitive)

### Can't Find Embedded Resource

**Problem**: Error message says resource not found.

**Debug**:
```csharp
// List all embedded resources in your assembly
var assembly = typeof(MyAgentTests).Assembly;
var resources = assembly.GetManifestResourceNames();
foreach (var r in resources)
{
    Console.WriteLine(r);
}
```

Expected format: `YourNamespace.TestData.MyAgent.Knowledge.prompt.md`

### HttpClient Required Error

**Problem**: Error about HttpClient being null.

**Solution**: Ensure `LocalMode = true` is set:
```csharp
var platform = XiansPlatform.StartLocal(options =>
{
    // This should be true (default in StartLocal)
    options.LocalMode = true;
});
```

## Migration from Server Tests

If you have existing tests using a real server, here's how to migrate:

**Before (Server-based)**:
```csharp
[TestInitialize]
public async Task Setup()
{
    _platform = await XiansPlatform.InitializeAsync(new XiansOptions
    {
        ServerUrl = Environment.GetEnvironmentVariable("SERVER_URL")!,
        ApiKey = Environment.GetEnvironmentVariable("API_KEY")!
    });
}
```

**After (Local Mode)**:
```csharp
[TestInitialize]
public async Task Setup()
{
    _platform = XiansPlatform.StartLocal(options =>
    {
        options.LocalModeAssemblies = new[] { typeof(MyTests).Assembly };
    });
}

// Create embedded resources for each knowledge item previously on server
// MyAgent.Knowledge.system-prompt.md
// MyAgent.Knowledge.instructions.txt
```

## Future Extensions

Local Mode is designed to be extensible. Future providers may include:
- **DocumentProvider** - Mock document upload/download
- **SettingsProvider** - Mock server settings
- **WorkflowProvider** - Mock workflow execution

The same pattern will apply to all these features.
