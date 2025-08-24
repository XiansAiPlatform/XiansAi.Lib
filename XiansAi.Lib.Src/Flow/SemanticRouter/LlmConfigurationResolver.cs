using Server;

namespace XiansAi.Flow.Router;

/// <summary>
/// Configuration resolver for LLM settings with fallback hierarchy:
/// RouterOptions -> Environment Variables -> Server Settings
/// </summary>
internal class LlmConfigurationResolver
{
    private readonly FlowServerSettings _settings;
    private readonly string? _envProvider;
    private readonly string? _envApiKey;
    private readonly string? _envEndpoint;
    private readonly string? _envDeploymentName;
    private readonly string? _envModelName;

    public LlmConfigurationResolver(FlowServerSettings settings)
    {
        _settings = settings;
        _envProvider = Environment.GetEnvironmentVariable("LLM_PROVIDER");
        _envApiKey = Environment.GetEnvironmentVariable("LLM_API_KEY");
        _envEndpoint = Environment.GetEnvironmentVariable("LLM_ENDPOINT");
        _envDeploymentName = Environment.GetEnvironmentVariable("LLM_DEPLOYMENT_NAME");
        _envModelName = Environment.GetEnvironmentVariable("LLM_MODEL_NAME");
    }

    public string GetApiKey(RouterOptions options) =>
        !string.IsNullOrEmpty(options.ApiKey) ? options.ApiKey :
        !string.IsNullOrEmpty(_envApiKey) ? _envApiKey :
        !string.IsNullOrEmpty(_settings.ApiKey) ? _settings.ApiKey :
        throw new InvalidOperationException("LLM API Key is not available");

    public string GetProviderName(RouterOptions options) =>
        !string.IsNullOrEmpty(options.ProviderName) ? options.ProviderName :
        !string.IsNullOrEmpty(_envProvider) ? _envProvider :
        !string.IsNullOrEmpty(_settings.ProviderName) ? _settings.ProviderName :
        throw new InvalidOperationException("LLM Provider is not available");

    public string GetDeploymentName(RouterOptions options) =>
        !string.IsNullOrWhiteSpace(options.DeploymentName) ? options.DeploymentName :
        !string.IsNullOrWhiteSpace(_envDeploymentName) ? _envDeploymentName :
        _settings.AdditionalConfig?.TryGetValue("DeploymentName", out var deploymentName) == true && 
        !string.IsNullOrWhiteSpace(deploymentName) ? deploymentName :
        throw new InvalidOperationException("LLM DeploymentName is not available");

    public string GetEndpoint(RouterOptions options) =>
        !string.IsNullOrWhiteSpace(options.Endpoint) ? options.Endpoint :
        !string.IsNullOrWhiteSpace(_envEndpoint) ? _envEndpoint :
        !string.IsNullOrWhiteSpace(_settings.BaseUrl) ? _settings.BaseUrl :
        throw new InvalidOperationException("LLM BaseUrl is not available");

    public string GetModelName(RouterOptions options) =>
        !string.IsNullOrWhiteSpace(options.ModelName) ? options.ModelName :
        !string.IsNullOrWhiteSpace(_envModelName) ? _envModelName :
        !string.IsNullOrWhiteSpace(_settings.ModelName) ? _settings.ModelName :
        throw new InvalidOperationException("LLM Model Name is not available");
}
