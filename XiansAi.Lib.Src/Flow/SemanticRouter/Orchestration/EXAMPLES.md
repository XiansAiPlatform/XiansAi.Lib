# AI Orchestrator Examples

This document provides practical examples of using different AI orchestrators in various scenarios.

## Table of Contents
1. [Basic Examples](#basic-examples)
2. [Advanced Scenarios](#advanced-scenarios)
3. [Comparison Examples](#comparison-examples)
4. [Integration Patterns](#integration-patterns)

## Basic Examples

### Example 1: Simple Q&A with Semantic Kernel

```csharp
using XiansAi.Flow.Router;
using XiansAi.Flow.Router.Orchestration;

public class SimpleQAExample
{
    public async Task<string> AskQuestion(string question)
    {
        var config = new SemanticKernelConfig
        {
            ProviderName = "openai",
            ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
            ModelName = "gpt-4",
            Temperature = 0.3,
            MaxTokens = 1000
        };

        var response = await SemanticRouterHub.CompletionWithOrchestratorAsync(
            prompt: question,
            systemInstruction: "You are a knowledgeable assistant. Provide concise, accurate answers.",
            config: config
        );

        return response ?? "No response received";
    }
}

// Usage
var example = new SimpleQAExample();
var answer = await example.AskQuestion("What is dependency injection?");
Console.WriteLine(answer);
```

### Example 2: AWS Bedrock Agent Integration

```csharp
public class BedrockAgentExample
{
    public async Task<string> ProcessCustomerInquiry(string inquiry)
    {
        var config = new AWSBedrockConfig
        {
            AccessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"),
            SecretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY"),
            Region = "us-east-1",
            AgentId = "ABCD1234",
            AgentAliasId = "ALIAS5678",
            EnableTrace = true,
            Temperature = 0.5,
            MaxTokens = 2000
        };

        var response = await SemanticRouterHub.CompletionWithOrchestratorAsync(
            prompt: inquiry,
            systemInstruction: "You are a customer service agent. Be helpful and professional.",
            config: config
        );

        return response ?? "Unable to process inquiry";
    }
}

// Usage
var agent = new BedrockAgentExample();
var response = await agent.ProcessCustomerInquiry("I need help with my order #12345");
Console.WriteLine(response);
```

### Example 3: Azure AI Foundry with RAG

```csharp
public class AzureRAGExample
{
    public async Task<string> SearchProductKnowledgeBase(string query)
    {
        var config = new AzureAIFoundryConfig
        {
            ProjectEndpoint = "https://my-project.cognitiveservices.azure.com/",
            ModelDeploymentName = "gpt-4",
            AzureAISearchConnectionId = "search-conn-123",
            SearchIndexName = "product-catalog",
            SearchTopK = 5,
            SearchFilter = "category eq 'electronics' and inStock eq true",
            SearchQueryType = "Semantic",
            AgentName = "Product Support Agent",
            Temperature = 0.2
        };

        var response = await SemanticRouterHub.CompletionWithOrchestratorAsync(
            prompt: query,
            systemInstruction: "You are a product expert. Use the search results to provide accurate product information.",
            config: config
        );

        return response ?? "No results found";
    }
}

// Usage
var rag = new AzureRAGExample();
var info = await rag.SearchProductKnowledgeBase("What are the best noise-cancelling headphones?");
Console.WriteLine(info);
```

## Advanced Scenarios

### Example 4: Multi-Turn Conversation with Semantic Kernel

```csharp
public class ConversationExample
{
    public async Task<string> RouteConversation(
        MessageThread messageThread,
        List<Type> capabilities)
    {
        var config = new SemanticKernelConfig
        {
            ProviderName = "azureopenai",
            ApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY"),
            Endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT"),
            DeploymentName = "gpt-4-32k",
            Temperature = 0.7,
            MaxTokens = 4000,
            HistorySizeToFetch = 20,
            TokenLimit = 80000,
            TargetTokenCount = 50000
        };

        var request = new OrchestratorRequest
        {
            MessageThread = messageThread,
            SystemPrompt = @"You are an AI assistant with access to various tools. 
                           Help the user accomplish their tasks efficiently.",
            Config = config,
            CapabilityTypes = capabilities
        };

        return await SemanticRouterHub.RouteWithOrchestratorAsync(request);
    }
}

// Usage
var conversation = new ConversationExample();
var capabilities = new List<Type> 
{ 
    typeof(WebSearchPlugin), 
    typeof(EmailPlugin), 
    typeof(CalendarPlugin) 
};

var response = await conversation.RouteConversation(messageThread, capabilities);
```

### Example 5: Custom Interceptor with AWS Bedrock

```csharp
public class ContentModerationInterceptor : IChatInterceptor
{
    public async Task<MessageThread> InterceptIncomingMessageAsync(MessageThread messageThread)
    {
        // Moderate incoming content
        var content = messageThread.LatestMessage?.Content ?? "";
        if (ContainsInappropriateContent(content))
        {
            messageThread.LatestMessage.Content = "[Content Moderated]";
        }
        return messageThread;
    }

    public async Task<string?> InterceptOutgoingMessageAsync(MessageThread messageThread, string response)
    {
        // Add disclaimer to AI responses
        return $"{response}\n\n_This response was generated by AI and should be verified._";
    }

    private bool ContainsInappropriateContent(string content)
    {
        // Implement moderation logic
        return false;
    }
}

public class ModeratedBedrockExample
{
    public async Task<string> ProcessWithModeration(MessageThread messageThread)
    {
        var config = new AWSBedrockConfig
        {
            AccessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"),
            SecretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY"),
            Region = "us-west-2",
            AgentId = "AGENT123",
            AgentAliasId = "ALIAS456",
            Temperature = 0.3
        };

        var request = new OrchestratorRequest
        {
            MessageThread = messageThread,
            SystemPrompt = "You are a helpful assistant.",
            Config = config,
            Interceptor = new ContentModerationInterceptor()
        };

        return await SemanticRouterHub.RouteWithOrchestratorAsync(request);
    }
}
```

### Example 6: Dynamic Orchestrator Selection

```csharp
public class DynamicOrchestratorExample
{
    public async Task<string> ProcessQuery(string query, string preferredPlatform)
    {
        OrchestratorConfig config = preferredPlatform.ToLower() switch
        {
            "aws" => new AWSBedrockConfig
            {
                AccessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"),
                SecretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY"),
                Region = "us-east-1",
                AgentId = "AGENT123",
                AgentAliasId = "ALIAS456"
            },
            "azure" => new AzureAIFoundryConfig
            {
                ProjectEndpoint = Environment.GetEnvironmentVariable("AZURE_PROJECT_ENDPOINT"),
                ModelDeploymentName = "gpt-4"
            },
            _ => new SemanticKernelConfig
            {
                ProviderName = "openai",
                ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
                ModelName = "gpt-4"
            }
        };

        return await SemanticRouterHub.CompletionWithOrchestratorAsync(
            prompt: query,
            systemInstruction: "You are a helpful assistant.",
            config: config
        );
    }
}

// Usage
var dynamic = new DynamicOrchestratorExample();
var response = await dynamic.ProcessQuery("What's the weather?", "aws");
```

## Comparison Examples

### Example 7: Performance Comparison

```csharp
public class PerformanceComparisonExample
{
    public async Task CompareOrchestrators(string prompt)
    {
        var systemInstruction = "You are a helpful assistant.";
        var results = new Dictionary<string, (string Response, TimeSpan Duration)>();

        // Test Semantic Kernel
        var skConfig = new SemanticKernelConfig
        {
            ProviderName = "openai",
            ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
            ModelName = "gpt-4"
        };

        var sw = Stopwatch.StartNew();
        var skResponse = await SemanticRouterHub.CompletionWithOrchestratorAsync(
            prompt, systemInstruction, skConfig);
        sw.Stop();
        results["Semantic Kernel"] = (skResponse, sw.Elapsed);

        // Test AWS Bedrock
        var bedrockConfig = new AWSBedrockConfig
        {
            AccessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"),
            SecretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY"),
            Region = "us-east-1",
            AgentId = "AGENT123",
            AgentAliasId = "ALIAS456"
        };

        sw.Restart();
        var bedrockResponse = await SemanticRouterHub.CompletionWithOrchestratorAsync(
            prompt, systemInstruction, bedrockConfig);
        sw.Stop();
        results["AWS Bedrock"] = (bedrockResponse, sw.Elapsed);

        // Test Azure AI Foundry
        var azureConfig = new AzureAIFoundryConfig
        {
            ProjectEndpoint = Environment.GetEnvironmentVariable("AZURE_PROJECT_ENDPOINT"),
            ModelDeploymentName = "gpt-4"
        };

        sw.Restart();
        var azureResponse = await SemanticRouterHub.CompletionWithOrchestratorAsync(
            prompt, systemInstruction, azureConfig);
        sw.Stop();
        results["Azure AI Foundry"] = (azureResponse, sw.Elapsed);

        // Display results
        foreach (var (orchestrator, (response, duration)) in results)
        {
            Console.WriteLine($"\n{orchestrator}:");
            Console.WriteLine($"  Duration: {duration.TotalMilliseconds}ms");
            Console.WriteLine($"  Response: {response?.Substring(0, Math.Min(100, response.Length ?? 0))}...");
        }
    }
}
```

## Integration Patterns

### Example 8: Factory Pattern with Configuration

```csharp
public interface IOrchestratorConfigProvider
{
    OrchestratorConfig GetConfig(string scenario);
}

public class ConfigurationBasedProvider : IOrchestratorConfigProvider
{
    private readonly IConfiguration _configuration;

    public ConfigurationBasedProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public OrchestratorConfig GetConfig(string scenario)
    {
        var section = _configuration.GetSection($"Orchestrators:{scenario}");
        var type = section.GetValue<string>("Type");

        return type?.ToLower() switch
        {
            "semantickernel" => new SemanticKernelConfig
            {
                ProviderName = section.GetValue<string>("ProviderName"),
                ApiKey = section.GetValue<string>("ApiKey"),
                ModelName = section.GetValue<string>("ModelName"),
                Temperature = section.GetValue<double>("Temperature", 0.3)
            },
            "bedrock" => new AWSBedrockConfig
            {
                AccessKeyId = section.GetValue<string>("AccessKeyId"),
                SecretAccessKey = section.GetValue<string>("SecretAccessKey"),
                Region = section.GetValue<string>("Region"),
                AgentId = section.GetValue<string>("AgentId"),
                AgentAliasId = section.GetValue<string>("AgentAliasId")
            },
            "azurefoundry" => new AzureAIFoundryConfig
            {
                ProjectEndpoint = section.GetValue<string>("ProjectEndpoint"),
                ModelDeploymentName = section.GetValue<string>("ModelDeploymentName"),
                SearchIndexName = section.GetValue<string>("SearchIndexName")
            },
            _ => throw new NotSupportedException($"Orchestrator type '{type}' not supported")
        };
    }
}

// appsettings.json
{
  "Orchestrators": {
    "CustomerService": {
      "Type": "Bedrock",
      "AccessKeyId": "...",
      "SecretAccessKey": "...",
      "Region": "us-east-1",
      "AgentId": "AGENT123",
      "AgentAliasId": "ALIAS456"
    },
    "ProductSearch": {
      "Type": "AzureFoundry",
      "ProjectEndpoint": "https://...",
      "ModelDeploymentName": "gpt-4",
      "SearchIndexName": "products"
    },
    "General": {
      "Type": "SemanticKernel",
      "ProviderName": "openai",
      "ApiKey": "...",
      "ModelName": "gpt-4",
      "Temperature": 0.7
    }
  }
}

// Usage
var provider = new ConfigurationBasedProvider(configuration);
var config = provider.GetConfig("CustomerService");
var response = await SemanticRouterHub.CompletionWithOrchestratorAsync(
    prompt, systemInstruction, config);
```

### Example 9: Retry and Fallback Pattern

```csharp
public class ResilientOrchestratorExample
{
    private readonly List<OrchestratorConfig> _configs;

    public ResilientOrchestratorExample()
    {
        _configs = new List<OrchestratorConfig>
        {
            // Primary: AWS Bedrock
            new AWSBedrockConfig
            {
                AccessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"),
                SecretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY"),
                Region = "us-east-1",
                AgentId = "AGENT123",
                AgentAliasId = "ALIAS456"
            },
            // Fallback: Semantic Kernel with OpenAI
            new SemanticKernelConfig
            {
                ProviderName = "openai",
                ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
                ModelName = "gpt-4"
            }
        };
    }

    public async Task<string> ProcessWithFallback(string prompt, string systemInstruction)
    {
        Exception? lastException = null;

        foreach (var config in _configs)
        {
            try
            {
                var response = await SemanticRouterHub.CompletionWithOrchestratorAsync(
                    prompt, systemInstruction, config);

                if (!string.IsNullOrEmpty(response))
                {
                    Console.WriteLine($"Success with {config.OrchestratorType}");
                    return response;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed with {config.OrchestratorType}: {ex.Message}");
                lastException = ex;
                // Continue to next orchestrator
            }
        }

        throw new InvalidOperationException(
            "All orchestrators failed", lastException);
    }
}
```

### Example 10: Async Batch Processing

```csharp
public class BatchProcessingExample
{
    public async Task<Dictionary<string, string>> ProcessBatchQueries(
        List<string> queries, 
        OrchestratorConfig config)
    {
        var results = new Dictionary<string, string>();
        var semaphore = new SemaphoreSlim(5); // Max 5 concurrent requests

        var tasks = queries.Select(async query =>
        {
            await semaphore.WaitAsync();
            try
            {
                var response = await SemanticRouterHub.CompletionWithOrchestratorAsync(
                    prompt: query,
                    systemInstruction: "You are a helpful assistant.",
                    config: config
                );

                lock (results)
                {
                    results[query] = response ?? "No response";
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return results;
    }
}

// Usage
var batch = new BatchProcessingExample();
var queries = new List<string>
{
    "What is C#?",
    "Explain async/await",
    "What are design patterns?",
    "How does garbage collection work?",
    "What is LINQ?"
};

var config = new SemanticKernelConfig
{
    ProviderName = "openai",
    ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
    ModelName = "gpt-4"
};

var results = await batch.ProcessBatchQueries(queries, config);
foreach (var (query, answer) in results)
{
    Console.WriteLine($"Q: {query}");
    Console.WriteLine($"A: {answer}\n");
}
```

## Summary

These examples demonstrate:
- **Basic usage** of each orchestrator type
- **Advanced patterns** like interceptors, dynamic selection, and fallback strategies
- **Integration patterns** for real-world applications
- **Performance comparison** techniques
- **Resilience** and error handling

Choose the orchestrator that best fits your needs:
- **Semantic Kernel**: Maximum flexibility and control
- **AWS Bedrock**: AWS-native, managed infrastructure
- **Azure AI Foundry**: Enterprise RAG with Azure ecosystem

For more information, see the main [README.md](./README.md) documentation.


