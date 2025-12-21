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

    private XiansPlatform(XiansOptions options)
    {
        _options = options;
        Agents = new AgentCollection(options);
    }

    /// <summary>
    /// Initializes the Xians platform with the specified options.
    /// </summary>
    /// <param name="options">Configuration options for the platform.</param>
    /// <returns>An initialized XiansPlatform instance.</returns>
    public static XiansPlatform Initialize(XiansOptions options)
    {
        // Validate configuration
        options.Validate();

        var platform = new XiansPlatform(options);
        
        // TODO: Initialize HTTP and Temporal services using ServiceFactory
        // var httpService = ServiceFactory.CreateHttpClientService(options);
        // var temporalService = await ServiceFactory.CreateTemporalClientServiceAsync(httpService);
        // platform.Agents.SetServices(httpService, temporalService);
        
        return platform;
    }
}

