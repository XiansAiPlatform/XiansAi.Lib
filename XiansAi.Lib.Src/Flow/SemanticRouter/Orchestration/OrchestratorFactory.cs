using XiansAi.Flow.Router.Orchestration.SemanticKernel;
using XiansAi.Flow.Router.Orchestration.AWSBedrock;
using XiansAi.Flow.Router.Orchestration.AzureAIFoundry;

namespace XiansAi.Flow.Router.Orchestration;

/// <summary>
/// Factory for creating AI orchestrator instances based on configuration.
/// </summary>
public static class OrchestratorFactory
{
    /// <summary>
    /// Creates an orchestrator instance based on the configuration type.
    /// </summary>
    /// <param name="config">The orchestrator configuration</param>
    /// <returns>An orchestrator instance</returns>
    /// <exception cref="ArgumentException">Thrown when config type doesn't match orchestrator type</exception>
    /// <exception cref="NotSupportedException">Thrown when orchestrator type is not supported</exception>
    public static IAIOrchestrator Create(OrchestratorConfig config)
    {
        return config.OrchestratorType switch
        {
            OrchestratorType.SemanticKernel => CreateSemanticKernelOrchestrator(config),
            OrchestratorType.AWSBedrock => CreateAWSBedrockOrchestrator(config),
            OrchestratorType.AzureAIFoundry => CreateAzureAIFoundryOrchestrator(config),
            _ => throw new NotSupportedException($"Orchestrator type '{config.OrchestratorType}' is not supported")
        };
    }

    private static IAIOrchestrator CreateSemanticKernelOrchestrator(OrchestratorConfig config)
    {
        if (config is not SemanticKernelConfig)
            throw new ArgumentException("Config must be SemanticKernelConfig for SemanticKernel orchestrator type", nameof(config));

        return new SemanticKernelOrchestrator();
    }

    private static IAIOrchestrator CreateAWSBedrockOrchestrator(OrchestratorConfig config)
    {
        if (config is not AWSBedrockConfig)
            throw new ArgumentException("Config must be AWSBedrockConfig for AWSBedrock orchestrator type", nameof(config));

        return new AWSBedrockOrchestrator();
    }

    private static IAIOrchestrator CreateAzureAIFoundryOrchestrator(OrchestratorConfig config)
    {
        if (config is not AzureAIFoundryConfig)
            throw new ArgumentException("Config must be AzureAIFoundryConfig for AzureAIFoundry orchestrator type", nameof(config));

        return new AzureAIFoundryOrchestrator();
    }

    /// <summary>
    /// Converts legacy RouterOptions to OrchestratorConfig for backward compatibility.
    /// If a config is provided, merges base properties from RouterOptions into it.
    /// Otherwise, creates a SemanticKernelConfig from RouterOptions.
    /// </summary>
    /// <param name="options">Legacy RouterOptions</param>
    /// <param name="config">Optional existing config to merge properties into</param>
    /// <returns>OrchestratorConfig with equivalent settings</returns>
    public static OrchestratorConfig ConvertFromRouterOptions(RouterOptions options, OrchestratorConfig? config = null)
    {
        if (config is AWSBedrockConfig bedrockConfig)
        {
            // Merge base properties from RouterOptions into existing AWSBedrockConfig
            bedrockConfig.Temperature = options.Temperature;
            bedrockConfig.MaxTokens = options.MaxTokens;
            bedrockConfig.HistorySizeToFetch = options.HistorySizeToFetch;
            bedrockConfig.WelcomeMessage = options.WelcomeMessage;
            bedrockConfig.HTTPTimeoutSeconds = options.HTTPTimeoutSeconds;
            bedrockConfig.TokenLimit = options.TokenLimit;
            bedrockConfig.TargetTokenCount = options.TargetTokenCount;
            bedrockConfig.MaxTokensPerFunctionResult = options.MaxTokensPerFunctionResult;
            bedrockConfig.MaxConsecutiveCalls = options.MaxConsecutiveCalls;
            return bedrockConfig;
        }
        else if (config is AzureAIFoundryConfig azureConfig)
        {
            // Merge base properties from RouterOptions into existing AzureAIFoundryConfig
            azureConfig.Temperature = options.Temperature;
            azureConfig.MaxTokens = options.MaxTokens;
            azureConfig.HistorySizeToFetch = options.HistorySizeToFetch;
            azureConfig.WelcomeMessage = options.WelcomeMessage;
            azureConfig.HTTPTimeoutSeconds = options.HTTPTimeoutSeconds;
            azureConfig.TokenLimit = options.TokenLimit;
            azureConfig.TargetTokenCount = options.TargetTokenCount;
            azureConfig.MaxTokensPerFunctionResult = options.MaxTokensPerFunctionResult;
            azureConfig.MaxConsecutiveCalls = options.MaxConsecutiveCalls;
            return azureConfig;
        }
        else
        {
            // Default: Create SemanticKernelConfig from RouterOptions
            return new SemanticKernelConfig
            {
                OrchestratorType = OrchestratorType.SemanticKernel,
                ProviderName = options.ProviderName,
                ApiKey = options.ApiKey,
                ModelName = options.ModelName,
                DeploymentName = options.DeploymentName,
                Endpoint = options.Endpoint,
                Temperature = options.Temperature,
                MaxTokens = options.MaxTokens,
                HistorySizeToFetch = options.HistorySizeToFetch,
                WelcomeMessage = options.WelcomeMessage,
                HTTPTimeoutSeconds = options.HTTPTimeoutSeconds,
                TokenLimit = options.TokenLimit,
                TargetTokenCount = options.TargetTokenCount,
                MaxTokensPerFunctionResult = options.MaxTokensPerFunctionResult,
                MaxConsecutiveCalls = options.MaxConsecutiveCalls
            };
        }
    }
}

