using Microsoft.Extensions.Logging;
using System.Text;

// NOTE: This implementation requires the Amazon.BedrockAgentRuntime NuGet package
// Add this to your .csproj: <PackageReference Include="Amazon.BedrockAgentRuntime" Version="..." />

#if ENABLE_AWS_BEDROCK
using Amazon.BedrockAgentRuntime;
using Amazon.BedrockAgentRuntime.Model;
using Amazon.Runtime;
#endif

namespace XiansAi.Flow.Router.Orchestration.AWSBedrock;

/// <summary>
/// AWS Bedrock Agent Runtime implementation of the AI orchestrator.
/// 
/// NOTE: This orchestrator requires the Amazon.BedrockAgentRuntime NuGet package.
/// To enable it, add the package and define ENABLE_AWS_BEDROCK in your build configuration.
/// 
/// Example usage:
/// <code>
/// var config = new AWSBedrockConfig
/// {
///     AccessKeyId = "your-access-key",
///     SecretAccessKey = "your-secret-key",
///     Region = "us-east-1",
///     AgentId = "your-agent-id",
///     AgentAliasId = "your-agent-alias-id"
/// };
/// 
/// using var orchestrator = new AWSBedrockOrchestrator();
/// var result = await orchestrator.RouteAsync(request);
/// </code>
/// </summary>
public class AWSBedrockOrchestrator : IAIOrchestrator
{
    private readonly ILogger _logger;

#if ENABLE_AWS_BEDROCK
    private AmazonBedrockAgentRuntimeClient? _client;
#endif

    public AWSBedrockOrchestrator()
    {
        _logger = Globals.LogFactory.CreateLogger<AWSBedrockOrchestrator>();
    }

    public Task<string?> RouteAsync(OrchestratorRequest request)
    {
#if ENABLE_AWS_BEDROCK
        return RouteAsyncImpl(request);
    }

    private async Task<string?> RouteAsyncImpl(OrchestratorRequest request)
    {
        if (request.Config is not AWSBedrockConfig bedrockConfig)
            throw new ArgumentException("Config must be AWSBedrockConfig for AWSBedrockOrchestrator", nameof(request.Config));

        try
        {
            // Initialize client if not already done
            if (_client == null)
            {
                var credentials = new BasicAWSCredentials(bedrockConfig.AccessKeyId, bedrockConfig.SecretAccessKey);
                var config = new AmazonBedrockAgentRuntimeConfig
                {
                    RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(bedrockConfig.Region)
                };
                _client = new AmazonBedrockAgentRuntimeClient(credentials, config);
            }

            // Apply incoming message interception
            var messageThread = request.Interceptor != null
                ? await request.Interceptor.InterceptIncomingMessageAsync(request.MessageThread)
                : request.MessageThread;

            // Create session ID from workflow ID
            var sessionId = SanitizeSessionId(messageThread.WorkflowId);

            // Build input text (combine system prompt context if needed)
            var inputText = messageThread.LatestMessage?.Content ?? string.Empty;

            // Create invoke request
            var invokeRequest = new InvokeAgentRequest
            {
                SessionId = sessionId,
                AgentId = bedrockConfig.AgentId,
                AgentAliasId = bedrockConfig.AgentAliasId,
                InputText = inputText,
                EnableTrace = bedrockConfig.EnableTrace
            };

            var response = await _client.InvokeAgentAsync(invokeRequest);

            if (response.Completion == null)
            {
                throw new InvalidOperationException("Completion is undefined in the Bedrock response.");
            }

            var responseBuilder = new StringBuilder();

            await foreach (var item in response.Completion)
            {
                if (item is PayloadPart payloadPart)
                {
                    var chunk = Encoding.UTF8.GetString(payloadPart.Bytes.ToArray());
                    responseBuilder.Append(chunk);
                }
            }

            var result = responseBuilder.ToString();

            _logger.LogDebug("Bedrock Agent Response: {Response}", result);

            // Apply outgoing message interception
            var finalResponse = request.Interceptor != null
                ? await request.Interceptor.InterceptOutgoingMessageAsync(messageThread, result)
                : result;

            // Handle skip response flag
            if (messageThread.SkipResponse)
            {
                messageThread.SkipResponse = false;
                return null;
            }

            return finalResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking AWS Bedrock agent for workflow {WorkflowType}", request.MessageThread.WorkflowType);
            throw;
        }
#else
         return Task.FromException<string?>(new NotSupportedException(
             "AWS Bedrock orchestrator is not available. " +
             "Add the Amazon.BedrockAgentRuntime NuGet package and define ENABLE_AWS_BEDROCK to enable this orchestrator."));
#endif
    }

    public Task<string?> CompletionAsync(string prompt, string? systemInstruction, OrchestratorConfig config)
    {
#if ENABLE_AWS_BEDROCK
        return CompletionAsyncImpl(prompt, systemInstruction, config);
#else
         return Task.FromException<string?>(new NotSupportedException(
            "AWS Bedrock orchestrator is not available. " +
            "Add the Amazon.BedrockAgentRuntime NuGet package and define ENABLE_AWS_BEDROCK to enable this orchestrator."));
#endif
    }

#if ENABLE_AWS_BEDROCK
    private async Task<string?> CompletionAsyncImpl(string prompt, string? systemInstruction, OrchestratorConfig config)
    {
        if (config is not AWSBedrockConfig bedrockConfig)
            throw new ArgumentException("Config must be AWSBedrockConfig for AWSBedrockOrchestrator", nameof(config));

        try
        {
            // Initialize client if not already done
            if (_client == null)
            {
                var credentials = new BasicAWSCredentials(bedrockConfig.AccessKeyId, bedrockConfig.SecretAccessKey);
                var clientConfig = new AmazonBedrockAgentRuntimeConfig
                {
                    RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(bedrockConfig.Region)
                };
                _client = new AmazonBedrockAgentRuntimeClient(credentials, clientConfig);
            }

            // For completion, create a unique session ID
            var sessionId = Guid.NewGuid().ToString();

            // Combine system instruction with prompt if provided
            var inputText = string.IsNullOrEmpty(systemInstruction)
                ? prompt
                : $"{systemInstruction}\n\nUser: {prompt}";

            // Create invoke request
            var request = new InvokeAgentRequest
            {
                SessionId = sessionId,
                AgentId = bedrockConfig.AgentId,
                AgentAliasId = bedrockConfig.AgentAliasId,
                InputText = inputText,
                EnableTrace = bedrockConfig.EnableTrace
            };

            var response = await _client.InvokeAgentAsync(request);

            if (response.Completion == null)
            {
                throw new InvalidOperationException("Completion is undefined in the Bedrock response.");
            }

            var responseBuilder = new StringBuilder();

            await foreach (var item in response.Completion)
            {
                if (item is PayloadPart payloadPart)
                {
                    var chunk = Encoding.UTF8.GetString(payloadPart.Bytes.ToArray());
                    responseBuilder.Append(chunk);
                }
            }

            var result = responseBuilder.ToString();

            _logger.LogDebug("Bedrock Completion Response: {Response}", result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Bedrock completion for prompt: {Prompt}", prompt);
            throw;
        }
    }
#endif

    private static string SanitizeSessionId(string? workflowId)
    {
        if (string.IsNullOrEmpty(workflowId))
            return Guid.NewGuid().ToString();

        // AWS session IDs typically have restrictions similar to other identifiers
        // Keep only alphanumeric and hyphens
        var sanitized = System.Text.RegularExpressions.Regex.Replace(workflowId, @"[^a-zA-Z0-9-]", "-");
        
        // Ensure it's not too long (typical limit is 256 characters)
        if (sanitized.Length > 256)
            sanitized = sanitized.Substring(0, 256);
        
        return sanitized;
    }

    public void Dispose()
    {
#if ENABLE_AWS_BEDROCK
        _client?.Dispose();
#endif
    }
}

