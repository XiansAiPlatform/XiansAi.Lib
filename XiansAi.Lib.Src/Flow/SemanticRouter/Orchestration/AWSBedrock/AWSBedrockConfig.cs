namespace XiansAi.Flow.Router.Orchestration.AWSBedrock;

/// <summary>
/// Configuration for AWS Bedrock Agent Runtime orchestrator
/// </summary>
public class AWSBedrockConfig : OrchestratorConfig
{
    public AWSBedrockConfig()
    {
        OrchestratorType = OrchestratorType.AWSBedrock;
    }

    /// <summary>
    /// AWS access key ID for authentication
    /// </summary>
    public required string AccessKeyId { get; init; }

    /// <summary>
    /// AWS secret access key for authentication
    /// </summary>
    public required string SecretAccessKey { get; init; }

    /// <summary>
    /// AWS region (e.g., us-east-1, us-west-2)
    /// </summary>
    public required string Region { get; init; }

    /// <summary>
    /// Bedrock agent ID
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// Bedrock agent alias ID
    /// </summary>
    public required string AgentAliasId { get; init; }

    /// <summary>
    /// Enable trace logging for debugging
    /// </summary>
    public bool EnableTrace { get; set; } = true;
}

