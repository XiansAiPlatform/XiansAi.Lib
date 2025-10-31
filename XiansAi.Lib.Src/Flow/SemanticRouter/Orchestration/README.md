# AI Orchestrator Plugin Model

The AI Orchestrator plugin model provides a flexible abstraction layer for integrating multiple AI orchestration platforms into the XiansAI system. This allows you to choose the best AI platform for your needs while maintaining a consistent API.

## Supported Orchestrators

### 1. Semantic Kernel (Default)
- **Provider**: Microsoft Semantic Kernel
- **Supports**: OpenAI, Azure OpenAI
- **Features**: Function calling, plugin system, chat history management
- **Status**: ✅ Fully Implemented

### 2. AWS Bedrock
- **Provider**: Amazon Bedrock Agent Runtime
- **Features**: Pre-built agents, managed infrastructure, AWS integration
- **Status**: ✅ Implemented (requires Amazon.BedrockAgentRuntime package)

### 3. Azure AI Foundry
- **Provider**: Azure AI Foundry Persistent Agents
- **Features**: Persistent agents, Azure AI Search integration, managed threads
- **Status**: ✅ Implemented (requires Azure.AI.Agents.Persistent package)

## Architecture

```
┌─────────────────────┐
│  SemanticRouterHub  │  ← Public API
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ OrchestratorFactory │  ← Factory for creating orchestrators
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  IAIOrchestrator    │  ← Interface
└──────────┬──────────┘
           │
     ┌─────┴────────────────┬──────────────────┐
     │                      │                  │
     ▼                      ▼                  ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│  Semantic    │  │  AWS Bedrock │  │  Azure AI    │
│  Kernel      │  │  Orchestrator│  │  Foundry     │
│ Orchestrator │  │              │  │ Orchestrator │
└──────────────┘  └──────────────┘  └──────────────┘
```

## Usage Examples

### 1. Semantic Kernel (OpenAI)

```csharp
using XiansAi.Flow.Router.Orchestration;
using XiansAi.Flow.Router.Orchestration.SemanticKernel;

// Create configuration
var config = new SemanticKernelConfig
{
    ProviderName = "openai",
    ApiKey = "your-api-key",
    ModelName = "gpt-4",
    Temperature = 0.7,
    MaxTokens = 2000
};

// Simple completion
var response = await SemanticRouterHub.CompletionWithOrchestratorAsync(
    prompt: "What is the meaning of life?",
    systemInstruction: "You are a philosophical assistant.",
    config: config
);
```

### 2. Semantic Kernel (Azure OpenAI)

```csharp
var config = new SemanticKernelConfig
{
    ProviderName = "azureopenai",
    ApiKey = "your-azure-api-key",
    Endpoint = "https://your-resource.openai.azure.com/",
    DeploymentName = "gpt-4-deployment",
    Temperature = 0.3,
    MaxTokens = 4000,
    TokenLimit = 80000,
    TargetTokenCount = 50000
};

// Create orchestration request for routing
var request = new OrchestratorRequest
{
    MessageThread = messageThread,
    SystemPrompt = "You are a helpful customer service agent.",
    Config = config,
    CapabilityTypes = new List<Type> { typeof(MyCustomPlugin) },
    Interceptor = new MyCustomInterceptor()
};

var response = await SemanticRouterHub.RouteWithOrchestratorAsync(request);
```

### 3. AWS Bedrock

```csharp
using XiansAi.Flow.Router.Orchestration;
using XiansAi.Flow.Router.Orchestration.AWSBedrock;

var config = new AWSBedrockConfig
{
    AccessKeyId = "your-access-key-id",
    SecretAccessKey = "your-secret-access-key",
    Region = "us-east-1",
    AgentId = "your-agent-id",
    AgentAliasId = "your-agent-alias-id",
    EnableTrace = true,
    Temperature = 0.5
};

// Simple completion
var response = await SemanticRouterHub.CompletionWithOrchestratorAsync(
    prompt: "Analyze this customer feedback",
    systemInstruction: "You are a sentiment analysis expert.",
    config: config
);

// Routing with message thread
var request = new OrchestratorRequest
{
    MessageThread = messageThread,
    SystemPrompt = "You are a sales assistant.",
    Config = config
};

var routedResponse = await SemanticRouterHub.RouteWithOrchestratorAsync(request);
```

### 4. Azure AI Foundry

```csharp
using XiansAi.Flow.Router.Orchestration;
using XiansAi.Flow.Router.Orchestration.AzureAIFoundry;

var config = new AzureAIFoundryConfig
{
    ProjectEndpoint = "https://your-project.cognitiveservices.azure.com/",
    ModelDeploymentName = "gpt-4",
    AzureAISearchConnectionId = "your-search-connection-id",
    SearchIndexName = "products-index",
    SearchTopK = 5,
    SearchFilter = "category eq 'electronics'",
    SearchQueryType = "Simple",
    AgentName = "Product Support Agent",
    Temperature = 0.3
};

// The orchestrator automatically handles RAG with Azure AI Search
var response = await SemanticRouterHub.CompletionWithOrchestratorAsync(
    prompt: "What are the specs of the latest laptop?",
    systemInstruction: "You are a product specialist.",
    config: config
);
```

## Enabling Optional Orchestrators

### AWS Bedrock

1. Add NuGet package to your `.csproj`:
   ```xml
   <PackageReference Include="Amazon.BedrockAgentRuntime" Version="3.7.x" />
   ```

2. Define the build constant:
   ```xml
   <PropertyGroup>
     <DefineConstants>$(DefineConstants);ENABLE_AWS_BEDROCK</DefineConstants>
   </PropertyGroup>
   ```

### Azure AI Foundry
1. Add NuGet package to your `.csproj`:
   ```xml
   <PackageReference Include="Azure.AI.Agents.Persistent" Version="1.0.x" />
   <PackageReference Include="Azure.Identity" Version="1.12.x" />
   ```

2. Define the build constant:
   ```xml
   <PropertyGroup>
     <DefineConstants>$(DefineConstants);ENABLE_AZURE_AI_FOUNDRY</DefineConstants>
   </PropertyGroup>
   ```

## Creating Custom Orchestrators

To create a custom orchestrator:

1. **Implement the Interface**:
```csharp
public class MyCustomOrchestrator : IAIOrchestrator
{
    public async Task<string?> RouteAsync(OrchestratorRequest request)
    {
        // Implement routing logic
    }

    public async Task<string?> CompletionAsync(string prompt, string? systemInstruction, OrchestratorConfig config)
    {
        // Implement completion logic
    }

    public void Dispose()
    {
        // Cleanup resources
    }
}
```

2. **Create Configuration Class**:
```csharp
public class MyCustomConfig : OrchestratorConfig
{
    public MyCustomConfig()
    {
        OrchestratorType = (OrchestratorType)100; // Use custom enum value
    }

    public string CustomProperty { get; set; }
}
```

3. **Register in Factory** (modify `OrchestratorFactory.cs`):
```csharp
case (OrchestratorType)100:
    return new MyCustomOrchestrator();
```

## Migration Guide

### From RouterOptions to OrchestratorConfig

Old code:
```csharp
var options = new RouterOptions
{
    ProviderName = "openai",
    ModelName = "gpt-4",
    ApiKey = "key"
};

await SemanticRouterHub.CompletionAsync(prompt, systemInstruction, options);
```

New code (backward compatible):
```csharp
// Still works - automatically converted to SemanticKernelConfig
var options = new RouterOptions
{
    ProviderName = "openai",
    ModelName = "gpt-4",
    ApiKey = "key"
};

await SemanticRouterHub.CompletionAsync(prompt, systemInstruction, options);

// Or use the new API directly
var config = new SemanticKernelConfig
{
    ProviderName = "openai",
    ModelName = "gpt-4",
    ApiKey = "key"
};

await SemanticRouterHub.CompletionWithOrchestratorAsync(prompt, systemInstruction, config);
```

## Configuration Hierarchy

All orchestrator configs support fallback resolution:

1. **Explicit Config** (highest priority)
2. **AgentContext.RouterOptions**
3. **Environment Variables**
4. **Server Settings** (lowest priority)

Environment variables:

- `LLM_PROVIDER` - Provider name
- `LLM_API_KEY` - API key
- `LLM_ENDPOINT` - Endpoint URL
- `LLM_DEPLOYMENT_NAME` - Deployment name (Azure OpenAI)
- `LLM_MODEL_NAME` - Model name (OpenAI)

## Best Practices

1. **Choose the Right Orchestrator**:
   - **Semantic Kernel**: Maximum flexibility, custom plugins, open-source models
   - **AWS Bedrock**: AWS-native deployments, managed infrastructure
   - **Azure AI Foundry**: Enterprise RAG scenarios, Azure integration

2. **Resource Management**:
   - Always dispose orchestrators when done (use `using` statements)
   - Azure AI Foundry creates persistent resources - cleanup is handled in `Dispose()`

3. **Error Handling**:
   - All orchestrators throw exceptions on errors
   - Use try-catch blocks for production code

4. **Token Management**:
   - Configure `TokenLimit` and `TargetTokenCount` for long conversations
   - Use `MaxTokensPerFunctionResult` to prevent large plugin results

5. **Performance**:
   - Semantic Kernel: Highly optimized for function calling
   - AWS Bedrock: Lower latency in AWS regions
   - Azure AI Foundry: Best for RAG with Azure AI Search

## Troubleshooting

### "Orchestrator type not supported"
- Ensure you've added the required NuGet package
- Ensure you've defined the appropriate build constant (`ENABLE_AWS_BEDROCK` or `ENABLE_AZURE_AI_FOUNDRY`)

### "Config must be XXXConfig"
- Verify your config type matches the `OrchestratorType` enum value

### Authentication failures
- Check API keys, credentials, and endpoints
- Verify Azure credentials with `DefaultAzureCredential`
- For AWS, ensure IAM permissions are correct

## Future Enhancements

- [ ] Support for Anthropic Claude via AWS Bedrock
- [ ] Google Vertex AI orchestrator
- [ ] OpenAI Assistants API orchestrator
- [ ] Temporal workflow integration for all orchestrators
- [ ] Streaming response support
- [ ] Multi-model routing strategies

