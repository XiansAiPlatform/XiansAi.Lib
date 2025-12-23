using Microsoft.Extensions.Logging;
using Xians.Lib.Common;
using Xians.Lib.Http;
using Xians.Lib.Temporal;

namespace Xians.Lib.Agents;

/// <summary>
/// Main entry point for the Xians platform integration.
/// </summary>
public class XiansPlatform
{
    /// <summary>
    /// Gets the agent collection for managing agents.
    /// </summary>
    public AgentCollection Agents { get; private set; }

    private readonly XiansOptions _options;
    private readonly IHttpClientService _httpService;
    private readonly ITemporalClientService _temporalService;

    private XiansPlatform(XiansOptions options, IHttpClientService httpService, ITemporalClientService temporalService)
    {
        _options = options;
        _httpService = httpService;
        _temporalService = temporalService;
        Agents = new AgentCollection(options);
        
        // Set HTTP and Temporal services for agent operations
        Agents.SetServices(httpService, temporalService);
    }

    /// <summary>
    /// Initializes the Xians platform with the specified options asynchronously.
    /// Fetches Temporal configuration from the server if not provided.
    /// </summary>
    /// <param name="options">Configuration options for the platform.</param>
    /// <returns>An initialized XiansPlatform instance.</returns>
    public static async Task<XiansPlatform> InitializeAsync(XiansOptions options)
    {
        // Validate configuration
        options.Validate();

        // Create HTTP service
        var httpLogger = Common.LoggerFactory.CreateLogger<HttpClientService>();
        var httpService = ServiceFactory.CreateHttpClientService(options, httpLogger);
        
        // Create Temporal service
        var temporalLogger = Common.LoggerFactory.CreateLogger<TemporalClientService>();
        ITemporalClientService temporalService;
        
        if (options.TemporalConfiguration != null)
        {
            // Use provided Temporal configuration
            temporalService = ServiceFactory.CreateTemporalClientService(options.TemporalConfiguration, temporalLogger);
        }
        else
        {
            // Fetch Temporal configuration from server
            var serverSettings = await SettingsService.GetSettingsAsync(httpService);
            var temporalConfig = serverSettings.ToTemporalConfiguration();
            temporalService = ServiceFactory.CreateTemporalClientService(temporalConfig, temporalLogger);
        }
        
        var platform = new XiansPlatform(options, httpService, temporalService);
        
        return platform;
    }

    /// <summary>
    /// Initializes the Xians platform with the specified options synchronously.
    /// Note: This is a synchronous wrapper. Use InitializeAsync when possible.
    /// </summary>
    /// <param name="options">Configuration options for the platform.</param>
    /// <returns>An initialized XiansPlatform instance.</returns>
    public static XiansPlatform Initialize(XiansOptions options)
    {
        return InitializeAsync(options).GetAwaiter().GetResult();
    }
}
