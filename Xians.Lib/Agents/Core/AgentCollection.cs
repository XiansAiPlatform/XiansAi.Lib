using Microsoft.Extensions.Logging;
using Xians.Lib.Common;
using Xians.Lib.Http;
using Xians.Lib.Temporal;
using Xians.Lib.Agents.Workflows;
using Xians.Lib.Common.Caching;
using Xians.Lib.Common.Infrastructure;

namespace Xians.Lib.Agents.Core;

/// <summary>
/// Manages the collection of registered agents.
/// </summary>
public class AgentCollection
{
    private readonly XiansOptions _options;
    private IHttpClientService? _httpService;
    private ITemporalClientService? _temporalService;
    private Xians.Lib.Common.Caching.CacheService? _cacheService;
    private WorkflowDefinitionUploader? _uploader;

    internal AgentCollection(XiansOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Sets the HTTP and Temporal services for agent operations.
    /// </summary>
    internal void SetServices(IHttpClientService? httpService, ITemporalClientService? temporalService, Xians.Lib.Common.Caching.CacheService? cacheService)
    {
        _httpService = httpService;
        _temporalService = temporalService;
        _cacheService = cacheService;
        
        // Create uploader if HTTP service is available
        if (_httpService != null)
        {
            var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<WorkflowDefinitionUploader>();
            _uploader = new WorkflowDefinitionUploader(_httpService, logger);
        }
    }

    /// <summary>
    /// Registers a new agent with the platform.
    /// </summary>
    /// <param name="registration">The registration information for the agent.</param>
    /// <returns>The registered XiansAgent instance.</returns>
    public XiansAgent Register(XiansAgentRegistration registration)
    {
        if (string.IsNullOrWhiteSpace(registration.Name))
        {
            throw new ArgumentException("Agent name is required", nameof(registration));
        }
        
        // Create agent-specific options that merge global options with registration settings
        var agentOptions = CreateAgentSpecificOptions(registration);
        
        return new XiansAgent(
            registration.Name, 
            registration.IsTemplate,
            registration.Description,
            registration.Summary,
            registration.Version,
            registration.Author,
            _uploader,
            _temporalService,
            _httpService,
            agentOptions,
            _cacheService);
    }

    /// <summary>
    /// Creates agent-specific options by merging global options with registration settings.
    /// </summary>
    /// <param name="registration">The agent registration with specific settings.</param>
    /// <returns>XiansOptions configured for the specific agent.</returns>
    private XiansOptions CreateAgentSpecificOptions(XiansAgentRegistration registration)
    {
        // Create a copy of global options for this agent
        var agentOptions = new XiansOptions
        {
            ServerUrl = _options.ServerUrl,
            ApiKey = _options.ApiKey,
            TemporalConfiguration = _options.TemporalConfiguration,
            Cache = _options.Cache,
            ConsoleLogLevel = _options.ConsoleLogLevel,
            ServerLogLevel = _options.ServerLogLevel,
            // Only override global EnableTasks if registration explicitly specifies a value
            EnableTasks = registration.EnableTasks ?? _options.EnableTasks
        };

        return agentOptions;
    }
}
