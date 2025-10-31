# Migration Guide: RouterOptions to Orchestrator Plugin Model

This guide helps you migrate from the legacy `RouterOptions` API to the new orchestrator plugin model.

## Overview of Changes

### What Changed?
- **Before**: Tightly coupled to Semantic Kernel and OpenAI/Azure OpenAI
- **After**: Plugin-based architecture supporting multiple AI platforms

### Benefits
✅ Support for multiple AI orchestrators (Semantic Kernel, AWS Bedrock, Azure AI Foundry)  
✅ Cleaner separation of concerns  
✅ Easier to test and mock  
✅ Better extensibility for custom orchestrators  
✅ **Fully backward compatible** - existing code continues to work

## Migration Paths

### Path 1: No Changes Required (Backward Compatible)

Your existing code will continue to work without any changes:

```csharp
// This still works!
var options = new RouterOptions
{
    ProviderName = "openai",
    ModelName = "gpt-4",
    ApiKey = "your-key"
};

var response = await SemanticRouterHub.CompletionAsync(
    prompt: "Hello",
    systemInstruction: "Be helpful",
    routerOptions: options
);
```

### Path 2: Gradual Migration (Recommended)

Migrate to the new API gradually as you refactor:

#### Step 1: Replace RouterOptions with SemanticKernelConfig

**Before:**
```csharp
var options = new RouterOptions
{
    ProviderName = "azureopenai",
    DeploymentName = "gpt-4",
    Endpoint = "https://my-resource.openai.azure.com/",
    ApiKey = "key",
    Temperature = 0.7,
    MaxTokens = 2000
};
```

**After:**
```csharp
var config = new SemanticKernelConfig
{
    ProviderName = "azureopenai",
    DeploymentName = "gpt-4",
    Endpoint = "https://my-resource.openai.azure.com/",
    ApiKey = "key",
    Temperature = 0.7,
    MaxTokens = 2000
};
```

#### Step 2: Use New API Methods

**Before:**
```csharp
var response = await SemanticRouterHub.CompletionAsync(
    prompt, systemInstruction, options);
```

**After:**
```csharp
var response = await SemanticRouterHub.CompletionWithOrchestratorAsync(
    prompt, systemInstruction, config);
```

#### Step 3: Update Routing Calls

**Before:**
```csharp
var response = await SemanticRouterHub.RouteAsync(
    messageThread, systemPrompt, options);
```

**After:**
```csharp
var request = new OrchestratorRequest
{
    MessageThread = messageThread,
    SystemPrompt = systemPrompt,
    Config = config,
    CapabilityTypes = capabilities,
    Interceptor = interceptor,
    KernelModifiers = kernelModifiers
};

var response = await SemanticRouterHub.RouteWithOrchestratorAsync(request);
```

### Path 3: Full Migration to New Orchestrators

Take advantage of new orchestrator options:

#### Switch to AWS Bedrock

```csharp
// Old (OpenAI via Semantic Kernel)
var options = new RouterOptions
{
    ProviderName = "openai",
    ModelName = "gpt-4",
    ApiKey = "openai-key"
};

// New (AWS Bedrock)
var config = new AWSBedrockConfig
{
    AccessKeyId = "aws-access-key",
    SecretAccessKey = "aws-secret-key",
    Region = "us-east-1",
    AgentId = "agent-id",
    AgentAliasId = "alias-id",
    Temperature = 0.7  // Same temperature control
};
```

#### Switch to Azure AI Foundry with RAG

```csharp
// Old (Azure OpenAI via Semantic Kernel)
var options = new RouterOptions
{
    ProviderName = "azureopenai",
    DeploymentName = "gpt-4",
    Endpoint = "https://my-resource.openai.azure.com/",
    ApiKey = "key"
};

// New (Azure AI Foundry with built-in RAG)
var config = new AzureAIFoundryConfig
{
    ProjectEndpoint = "https://my-project.cognitiveservices.azure.com/",
    ModelDeploymentName = "gpt-4",
    AzureAISearchConnectionId = "search-connection-id",
    SearchIndexName = "knowledge-base",
    SearchTopK = 5
};
```

## Common Migration Scenarios

### Scenario 1: Simple Completion Service

**Before:**
```csharp
public class CompletionService
{
    private readonly RouterOptions _options;

    public CompletionService()
    {
        _options = new RouterOptions
        {
            ProviderName = "openai",
            ModelName = "gpt-4",
            ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
            Temperature = 0.3
        };
    }

    public async Task<string> Complete(string prompt)
    {
        return await SemanticRouterHub.CompletionAsync(
            prompt, 
            "You are a helpful assistant", 
            _options);
    }
}
```

**After:**
```csharp
public class CompletionService
{
    private readonly SemanticKernelConfig _config;

    public CompletionService()
    {
        _config = new SemanticKernelConfig
        {
            ProviderName = "openai",
            ModelName = "gpt-4",
            ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
            Temperature = 0.3
        };
    }

    public async Task<string> Complete(string prompt)
    {
        return await SemanticRouterHub.CompletionWithOrchestratorAsync(
            prompt, 
            "You are a helpful assistant", 
            _config);
    }
}
```

### Scenario 2: Multi-Turn Conversation

**Before:**
```csharp
public class ConversationHandler
{
    public async Task<string> HandleMessage(
        MessageThread thread,
        List<Type> capabilities,
        IChatInterceptor interceptor)
    {
        var options = new RouterOptions
        {
            ProviderName = "azureopenai",
            DeploymentName = "gpt-4",
            Endpoint = "...",
            ApiKey = "...",
            HistorySizeToFetch = 20,
            TokenLimit = 80000
        };

        // Note: Old API didn't have a clean way to pass capabilities and interceptor
        // This required accessing RunnerRegistry internally
        return await SemanticRouterHub.RouteAsync(thread, systemPrompt, options);
    }
}
```

**After:**
```csharp
public class ConversationHandler
{
    public async Task<string> HandleMessage(
        MessageThread thread,
        List<Type> capabilities,
        IChatInterceptor interceptor)
    {
        var config = new SemanticKernelConfig
        {
            ProviderName = "azureopenai",
            DeploymentName = "gpt-4",
            Endpoint = "...",
            ApiKey = "...",
            HistorySizeToFetch = 20,
            TokenLimit = 80000
        };

        var request = new OrchestratorRequest
        {
            MessageThread = thread,
            SystemPrompt = "You are a helpful assistant",
            Config = config,
            CapabilityTypes = capabilities,  // Now explicit!
            Interceptor = interceptor         // Now explicit!
        };

        return await SemanticRouterHub.RouteWithOrchestratorAsync(request);
    }
}
```

### Scenario 3: Configurable Provider

**Before:**
```csharp
public class ConfigurableService
{
    private readonly IConfiguration _configuration;

    public async Task<string> Process(string prompt)
    {
        var options = new RouterOptions
        {
            ProviderName = _configuration["AI:Provider"],
            ModelName = _configuration["AI:Model"],
            ApiKey = _configuration["AI:ApiKey"],
            Temperature = _configuration.GetValue<double>("AI:Temperature")
        };

        return await SemanticRouterHub.CompletionAsync(prompt, null, options);
    }
}
```

**After (same provider, cleaner):**
```csharp
public class ConfigurableService
{
    private readonly IConfiguration _configuration;

    public async Task<string> Process(string prompt)
    {
        var config = new SemanticKernelConfig
        {
            ProviderName = _configuration["AI:Provider"],
            ModelName = _configuration["AI:Model"],
            ApiKey = _configuration["AI:ApiKey"],
            Temperature = _configuration.GetValue<double>("AI:Temperature")
        };

        return await SemanticRouterHub.CompletionWithOrchestratorAsync(
            prompt, null, config);
    }
}
```

**After (multi-provider support):**
```csharp
public class ConfigurableService
{
    private readonly IConfiguration _configuration;

    public async Task<string> Process(string prompt)
    {
        OrchestratorConfig config = _configuration["AI:Type"] switch
        {
            "OpenAI" => new SemanticKernelConfig
            {
                ProviderName = "openai",
                ModelName = _configuration["AI:Model"],
                ApiKey = _configuration["AI:ApiKey"]
            },
            "Bedrock" => new AWSBedrockConfig
            {
                AccessKeyId = _configuration["AWS:AccessKey"],
                SecretAccessKey = _configuration["AWS:SecretKey"],
                Region = _configuration["AWS:Region"],
                AgentId = _configuration["Bedrock:AgentId"],
                AgentAliasId = _configuration["Bedrock:AliasId"]
            },
            _ => throw new NotSupportedException()
        };

        return await SemanticRouterHub.CompletionWithOrchestratorAsync(
            prompt, null, config);
    }
}
```

## API Mapping Reference

### Method Names

| Old API | New API | Notes |
|---------|---------|-------|
| `CompletionAsync(prompt, instruction, RouterOptions)` | `CompletionWithOrchestratorAsync(prompt, instruction, OrchestratorConfig)` | Old API still works |
| `RouteAsync(thread, prompt, RouterOptions)` | `RouteWithOrchestratorAsync(OrchestratorRequest)` | Old API still works |
| N/A | `OrchestratorFactory.Create(config)` | New factory pattern |

### Configuration Classes

| Old Class | New Class | Type |
|-----------|-----------|------|
| `RouterOptions` | `SemanticKernelConfig` | Drop-in replacement |
| N/A | `AWSBedrockConfig` | New orchestrator |
| N/A | `AzureAIFoundryConfig` | New orchestrator |

### Properties

All properties from `RouterOptions` are available in `SemanticKernelConfig`:

| Property | Available in SemanticKernelConfig |
|----------|-----------------------------------|
| `ProviderName` | ✅ Yes |
| `ApiKey` | ✅ Yes |
| `ModelName` | ✅ Yes |
| `DeploymentName` | ✅ Yes |
| `Endpoint` | ✅ Yes |
| `Temperature` | ✅ Yes (also in base class) |
| `MaxTokens` | ✅ Yes (also in base class) |
| `HistorySizeToFetch` | ✅ Yes (also in base class) |
| `WelcomeMessage` | ✅ Yes (also in base class) |
| `HTTPTimeoutSeconds` | ✅ Yes (also in base class) |
| `TokenLimit` | ✅ Yes (also in base class) |
| `TargetTokenCount` | ✅ Yes (also in base class) |
| `MaxTokensPerFunctionResult` | ✅ Yes (also in base class) |
| `MaxConsecutiveCalls` | ✅ Yes (also in base class) |

## Breaking Changes

### None! 🎉

The migration is **100% backward compatible**. All existing code using `RouterOptions` continues to work.

## Deprecation Timeline

| Version | Status |
|---------|--------|
| 2.10.x | `RouterOptions` API fully supported (current) |
| 3.0.x | `RouterOptions` API marked as legacy, new API recommended |
| 4.0.x | `RouterOptions` API still supported for backward compatibility |

**Note**: There are no plans to remove `RouterOptions` support. It will continue to work indefinitely.

## Testing Your Migration

### Unit Tests

**Before:**
```csharp
[Test]
public async Task TestCompletion()
{
    var options = new RouterOptions
    {
        ProviderName = "openai",
        ModelName = "gpt-4",
        ApiKey = "test-key"
    };

    var result = await SemanticRouterHub.CompletionAsync("test", null, options);
    Assert.IsNotNull(result);
}
```

**After:**
```csharp
[Test]
public async Task TestCompletion()
{
    var config = new SemanticKernelConfig
    {
        ProviderName = "openai",
        ModelName = "gpt-4",
        ApiKey = "test-key"
    };

    var result = await SemanticRouterHub.CompletionWithOrchestratorAsync(
        "test", null, config);
    Assert.IsNotNull(result);
}
```

### Mocking Orchestrators

With the new design, you can easily mock orchestrators:

```csharp
public class MockOrchestrator : IAIOrchestrator
{
    public Task<string?> RouteAsync(OrchestratorRequest request)
    {
        return Task.FromResult<string?>("Mock response");
    }

    public Task<string?> CompletionAsync(string prompt, string? systemInstruction, OrchestratorConfig config)
    {
        return Task.FromResult<string?>($"Mock response for: {prompt}");
    }

    public void Dispose() { }
}

[Test]
public async Task TestWithMock()
{
    var orchestrator = new MockOrchestrator();
    var request = new OrchestratorRequest
    {
        MessageThread = CreateTestThread(),
        SystemPrompt = "Test",
        Config = new SemanticKernelConfig()
    };

    var result = await orchestrator.RouteAsync(request);
    Assert.AreEqual("Mock response", result);
}
```

## Getting Help

If you encounter issues during migration:

1. Check the [README.md](./README.md) for detailed documentation
2. Review the [EXAMPLES.md](./EXAMPLES.md) for practical examples
3. The old API continues to work - no pressure to migrate immediately
4. File an issue if you find any compatibility problems

## Recommended Migration Strategy

1. **Phase 1**: Understand the new architecture (read documentation)
2. **Phase 2**: Continue using existing code (it still works!)
3. **Phase 3**: Gradually migrate new code to use new API
4. **Phase 4**: Refactor existing code during regular maintenance
5. **Phase 5**: (Optional) Explore new orchestrators (AWS, Azure)

## Summary

✅ **No breaking changes** - existing code works  
✅ **Gradual migration** - migrate at your own pace  
✅ **New capabilities** - access to multiple AI platforms  
✅ **Better architecture** - cleaner, more testable code  
✅ **Future-proof** - easy to add new orchestrators  

The orchestrator plugin model provides a solid foundation for the future while respecting your existing investments.


