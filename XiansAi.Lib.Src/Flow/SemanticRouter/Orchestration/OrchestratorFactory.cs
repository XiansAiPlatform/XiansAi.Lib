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
    /// Converts legacy RouterOptions to SemanticKernelConfig for backward compatibility.
    /// </summary>
    /// <param name="options">Legacy RouterOptions</param>
    /// <returns>SemanticKernelConfig with equivalent settings</returns>
    public static SemanticKernelConfig ConvertFromRouterOptions(RouterOptions options)
    {
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

