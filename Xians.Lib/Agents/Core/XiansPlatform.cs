using Microsoft.Extensions.Logging;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Http;
using Xians.Lib.Temporal;
using Xians.Lib.Common.Caching;

namespace Xians.Lib.Agents.Core;

/// <summary>
/// Main entry point for the Xians platform integration.
/// </summary>
public class XiansPlatform
{
    /// <summary>
    /// Gets the agent collection for managing agents.
    /// </summary>
    public AgentCollection Agents { get; private set; }

    /// <summary>
    /// Gets the cache service for managing cached data.
    /// </summary>
    public Xians.Lib.Common.Caching.CacheService Cache { get; private set; }

    /// <summary>
    /// Gets the platform configuration options.
    /// Provides access to tenant ID, certificate info, and other settings.
    /// </summary>
    public XiansOptions Options { get; private set; }

    private readonly XiansOptions _options;
    private readonly IHttpClientService _httpService;
    private readonly ITemporalClientService _temporalService;

    private XiansPlatform(XiansOptions options, IHttpClientService httpService, ITemporalClientService temporalService)
    {
        _options = options;
        Options = options;
        _httpService = httpService;
        _temporalService = temporalService;
        
        // Initialize cache service
        var cacheLogger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<Xians.Lib.Common.Caching.CacheService>();
        Cache = new Xians.Lib.Common.Caching.CacheService(options.Cache, cacheLogger);
        
        Agents = new AgentCollection(options);
        
        // Set HTTP and Temporal services for agent operations
        Agents.SetServices(httpService, temporalService, Cache);
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
        var httpLogger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<HttpClientService>();
        var httpService = ServiceFactory.CreateHttpClientService(options, httpLogger);
        
        // Create Temporal service
        var temporalLogger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<TemporalClientService>();
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
        
        // Display initialization banner
        platform.DisplayInitializationBanner();
        
        return platform;
    }

    /// <summary>
    /// Displays a formatted initialization banner with platform connection details.
    /// </summary>
    private void DisplayInitializationBanner(bool showLogo = false)
    {
        var certInfo = _options.CertificateInfo;
        var serverUrl = _options.ServerUrl;
        
        if (showLogo) {
            // ASCII Art Banner
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
╔═════════════════════════════════════════════════════════════════════╗
║             ██╗  ██╗██╗ █████╗ ███╗   ██╗███████╗                   ║
║             ╚██╗██╔╝██║██╔══██╗████╗  ██║██╔════╝                   ║
║              ╚███╔╝ ██║███████║██╔██╗ ██║███████╗                   ║
║              ██╔██╗ ██║██╔══██║██║╚██╗██║╚════██║                   ║
║             ██╔╝ ██╗██║██║  ██║██║ ╚████║███████║                   ║
║             ╚═╝  ╚═╝╚═╝╚═╝  ╚═╝╚═╝  ╚═══╝╚══════╝                   ║
╚═════════════════════════════════════════════════════════════════════╝
    ");
            Console.ResetColor();
        }
        
        
        // Connection Details
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Platform Initialized Successfully");
        Console.ResetColor();
        Console.WriteLine();
        
        // Server Information
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("┌───────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ CONNECTION DETAILS                                            │");
        Console.WriteLine("├───────────────────────────────────────────────────────────────┤");
        Console.ResetColor();
        
        Console.Write("│ ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("Server URL      : ");
        Console.ResetColor();
        Console.WriteLine($"{serverUrl,-44}│");
        
        Console.Write("│ ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("Tenant ID       : ");
        Console.ResetColor();
        Console.WriteLine($"{certInfo.TenantId,-44}│");
        
        Console.Write("│ ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("User ID         : ");
        Console.ResetColor();
        Console.WriteLine($"{certInfo.UserId,-44}│");
        
        Console.Write("│ ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("Subject         : ");
        Console.ResetColor();
        var subject = certInfo.Subject.Length > 44 ? certInfo.Subject.Substring(0, 41) + "..." : certInfo.Subject;
        Console.WriteLine($"{subject,-44}│");
        
        Console.Write("│ ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("Certificate     : ");
        Console.ResetColor();
        var thumbprint = certInfo.Thumbprint.Substring(0, Math.Min(16, certInfo.Thumbprint.Length)) + "...";
        Console.WriteLine($"{thumbprint,-44}│");
        
        Console.Write("│ ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("Expires         : ");
        Console.ResetColor();
        var expiryColor = (certInfo.ExpiresAt - DateTime.UtcNow).TotalDays < 30 ? ConsoleColor.Red : ConsoleColor.Green;
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = expiryColor;
        var expiryText = certInfo.ExpiresAt.ToString("yyyy-MM-dd HH:mm:ss UTC");
        Console.Write(expiryText);
        Console.ForegroundColor = originalColor;
        Console.WriteLine($"{new string(' ', 44 - expiryText.Length)}│");
        
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("└───────────────────────────────────────────────────────────────┘");
        Console.ResetColor();
        Console.WriteLine();
    }
}
