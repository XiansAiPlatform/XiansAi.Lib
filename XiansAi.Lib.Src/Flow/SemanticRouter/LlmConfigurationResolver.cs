using System.Threading.Tasks;
using Server;
using XiansAi.Flow.Router.Orchestration.SemanticKernel;

namespace XiansAi.Flow.Router;

/// <summary>
/// Configuration resolver for LLM settings with fallback hierarchy:
/// SemanticKernelConfig -> AgentContext.RouterOptions -> Environment Variables -> Server Settings
/// </summary>
internal class LlmConfigurationResolver
{
    private readonly string? _envProvider;
    private readonly string? _envApiKey;
    private readonly string? _envEndpoint;
    private readonly string? _envDeploymentName;
    private readonly string? _envModelName;

    public LlmConfigurationResolver()
    {
        _envProvider = Environment.GetEnvironmentVariable("LLM_PROVIDER");
        _envApiKey = Environment.GetEnvironmentVariable("LLM_API_KEY");
        _envEndpoint = Environment.GetEnvironmentVariable("LLM_ENDPOINT");
        _envDeploymentName = Environment.GetEnvironmentVariable("LLM_DEPLOYMENT_NAME");
        _envModelName = Environment.GetEnvironmentVariable("LLM_MODEL_NAME");
    }

    public string GetApiKey(SemanticKernelConfig options)
    {
        if (!string.IsNullOrEmpty(options.ApiKey))
        {
            return options.ApiKey;
        }
        
        if (!string.IsNullOrEmpty(AgentContext.RouterOptions?.ApiKey))
        {
            return AgentContext.RouterOptions.ApiKey;
        }
        
        if (!string.IsNullOrEmpty(_envApiKey))
        {
            return _envApiKey;
        }
        
        var settings = GetSettings();
        if (!string.IsNullOrEmpty(settings.ApiKey))
        {
            return settings.ApiKey;
        }
        
        throw new InvalidOperationException("LLM API Key is not available");
    }

    public string GetProviderName(SemanticKernelConfig options)
    {
        if (!string.IsNullOrEmpty(options.ProviderName))
        {
            return options.ProviderName;
        }
        
        if (!string.IsNullOrEmpty(AgentContext.RouterOptions?.ProviderName))
        {
            return AgentContext.RouterOptions.ProviderName;
        }
        
        if (!string.IsNullOrEmpty(_envProvider))
        {
            return _envProvider;
        }
        
        var settings = GetSettings();
        if (!string.IsNullOrEmpty(settings.ProviderName))
        {
            return settings.ProviderName;
        }
        
        throw new InvalidOperationException("LLM Provider is not available");
    }

    public string GetDeploymentName(SemanticKernelConfig options)
    {
        if (!string.IsNullOrWhiteSpace(options.DeploymentName))
        {
            return options.DeploymentName;
        }
        
        if (!string.IsNullOrWhiteSpace(AgentContext.RouterOptions?.DeploymentName))
        {
            return AgentContext.RouterOptions.DeploymentName;
        }
        
        if (!string.IsNullOrWhiteSpace(_envDeploymentName))
        {
            return _envDeploymentName;
        }
        
        var settings = GetSettings();
        if (settings.AdditionalConfig?.TryGetValue("DeploymentName", out var deploymentName) == true && 
            !string.IsNullOrWhiteSpace(deploymentName))
        {
            return deploymentName;
        }
        
        throw new InvalidOperationException("LLM DeploymentName is not available");
    }

    public string GetEndpoint(SemanticKernelConfig options)
    {
        if (!string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return options.Endpoint;
        }
        
        if (!string.IsNullOrWhiteSpace(AgentContext.RouterOptions?.Endpoint))
        {
            return AgentContext.RouterOptions.Endpoint;
        }
        
        if (!string.IsNullOrWhiteSpace(_envEndpoint))
        {
            return _envEndpoint;
        }
        
        var settings = GetSettings();
        if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            return settings.BaseUrl;
        }
        
        throw new InvalidOperationException("LLM BaseUrl is not available");
    }

    public string GetModelName(SemanticKernelConfig options)
    {
        if (!string.IsNullOrWhiteSpace(options.ModelName))
        {
            return options.ModelName;
        }
        
        if (!string.IsNullOrWhiteSpace(AgentContext.RouterOptions?.ModelName))
        {
            return AgentContext.RouterOptions.ModelName;
        }
        
        if (!string.IsNullOrWhiteSpace(_envModelName))
        {
            return _envModelName;
        }
        
        var settings = GetSettings();
        if (!string.IsNullOrWhiteSpace(settings.ModelName))
        {
            return settings.ModelName;
        }
        
        throw new InvalidOperationException("LLM Model Name is not available");
    }

    private ServerSettings GetSettings()
    {
        return SettingsService.GetSettingsFromServer().Result;
    } 
    
}
