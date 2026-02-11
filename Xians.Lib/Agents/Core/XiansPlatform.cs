using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Common.Security;
using Xians.Lib.Http;
using Xians.Lib.Temporal;
using Xians.Lib.Common.Caching;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

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
    public CacheService Cache { get; private set; }

    /// <summary>
    /// Gets the platform configuration options.
    /// Provides access to tenant ID, certificate info, and other settings.
    /// </summary>
    public XiansOptions Options { get; private set; }

    private readonly XiansOptions _options;
    private readonly IHttpClientService? _httpService;
    private readonly ITemporalClientService? _temporalService;

    private XiansPlatform(XiansOptions options, IHttpClientService? httpService, ITemporalClientService? temporalService)
    {
        _options = options;
        Options = options;
        _httpService = httpService;
        _temporalService = temporalService;
        
        // Initialize cache service
        var cacheLogger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<Xians.Lib.Common.Caching.CacheService>();
        Cache = new Xians.Lib.Common.Caching.CacheService(options.Cache, cacheLogger);
        
        Agents = new AgentCollection(options);
        
        // Set HTTP and Temporal services for agent operations (null in local mode)
        if (httpService != null && temporalService != null)
        {
            Agents.SetServices(httpService, temporalService, Cache);
        }
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

        // Configure logging levels if provided in options
        LoggerFactory.ConfigureLogLevels(
            options.ConsoleLogLevel,
            options.ServerLogLevel
        );

        // Create HTTP service
        var httpLogger = LoggerFactory.CreateLogger<HttpClientService>();
        var httpService = ServiceFactory.CreateHttpClientService(options, httpLogger);
    
        
        // Initialize logging services if ServerLogLevel was set
        // This enables server-side log uploads
        if (!Logging.LoggingServices.IsInitialized)
        {
            var serverLogLevel = options.ServerLogLevel ?? LoggerFactory.GetServerLogLevel();
            if (serverLogLevel != MsLogLevel.None)
            {
                Logging.LoggingServices.Initialize(httpService);
            }
        }
        
        // Create Temporal service
        var temporalLogger = LoggerFactory.CreateLogger<TemporalClientService>();
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
    /// Initializes the Xians platform in local/mock mode for unit testing.
    /// No server connection is established. All operations use embedded resources.
    /// </summary>
    /// <param name="configure">Optional configuration action to customize options.</param>
    /// <returns>An initialized XiansPlatform instance in local mode.</returns>
    /// <remarks>
    /// In LocalMode:
    /// - Knowledge methods load from embedded resources (convention: {AgentName}.Knowledge.{Name}.{ext})
    /// - No HTTP calls are made to the server
    /// - Updates/creates are stored in-memory only
    /// - Temporal workflows are not executed
    /// 
    /// Example usage:
    /// <code>
    /// var platform = XiansPlatform.StartLocal(options =>
    /// {
    ///     options.LocalModeAssemblies = new[] { typeof(MyTests).Assembly };
    /// });
    /// 
    /// var agent = platform.Agents.Register("MyAgent", config => {});
    /// var knowledge = await agent.Knowledge.GetAsync("system-prompt");
    /// </code>
    /// </remarks>
    public static XiansPlatform StartLocal(Action<XiansOptions>? configure = null)
    {
        var options = new XiansOptions
        {
            LocalMode = true,
            ServerUrl = "https://localhost:5000", // Dummy URL (not used in local mode)
            ApiKey = GenerateDummyCertificate("test-tenant", "test-user")
        };

        // Allow user to customize options
        configure?.Invoke(options);

        // Validate that LocalMode is still true (user shouldn't disable it)
        if (!options.LocalMode)
        {
            throw new InvalidOperationException(
                "LocalMode must remain enabled when using StartLocal. " +
                "Use InitializeAsync for server-based initialization.");
        }

        // Configure logging if not already set
        if (options.ConsoleLogLevel == null && options.ServerLogLevel == null)
        {
            LoggerFactory.ConfigureLogLevels(
                consoleLogLevel: MsLogLevel.Debug,
                serverLogLevel: MsLogLevel.None  // No server logging in local mode
            );
        }

        var platform = new XiansPlatform(options, httpService: null, temporalService: null);
        
        // Display local mode banner
        platform.DisplayLocalModeBanner();
        
        return platform;
    }

    /// <summary>
    /// Asynchronous version of StartLocal for consistency with InitializeAsync.
    /// </summary>
    public static Task<XiansPlatform> StartLocalAsync(Action<XiansOptions>? configure = null)
    {
        return Task.FromResult(StartLocal(configure));
    }

    /// <summary>
    /// Generates a dummy certificate for local mode testing.
    /// </summary>
    private static string GenerateDummyCertificate(string tenantId, string userId)
    {
        // Create a minimal test certificate with required fields
        // This is a base64-encoded dummy cert for local testing only
        return CertificateGenerator.GenerateTestCertificate(tenantId, userId);
    }

    /// <summary>
    /// Displays a banner for local mode initialization.
    /// </summary>
    private void DisplayLocalModeBanner()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("⚠ Platform Initialized in LOCAL MODE (Testing)");
        Console.ResetColor();
        Console.WriteLine();
        
        const int boxWidth = 63;
        
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"┌{new string('─', boxWidth)}┐");
        Console.WriteLine("│ LOCAL MODE - NO SERVER CONNECTION                            │");
        Console.WriteLine($"├{new string('─', boxWidth)}┤");
        Console.ResetColor();
        Console.WriteLine();
        
        Console.WriteLine("  • Knowledge loaded from embedded resources");
        Console.WriteLine("  • No HTTP calls to server");
        Console.WriteLine("  • In-memory storage only");
        Console.WriteLine("  • Workflows not executed");
        Console.WriteLine();
        
        Console.Write("  ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("Assemblies      : ");
        Console.ResetColor();
        var assemblyCount = _options.LocalModeAssemblies?.Length ?? 0;
        if (assemblyCount > 0)
        {
            Console.WriteLine($"{assemblyCount} specified");
            foreach (var asm in _options.LocalModeAssemblies!)
            {
                Console.WriteLine($"                    - {asm.GetName().Name}");
            }
        }
        else
        {
            Console.WriteLine("All loaded non-system assemblies");
        }
        
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"└{new string('─', boxWidth)}┘");
        Console.ResetColor();
        Console.WriteLine();
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
        
        // Fixed box width for better console compatibility
        const int boxWidth = 63;
        
        // Server Information
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"┌{new string('─', boxWidth)}┐");
        Console.WriteLine("│ CONNECTION DETAILS                                            │");
        Console.WriteLine($"├{new string('─', boxWidth)}┤");
        Console.ResetColor();
        Console.WriteLine();
        
        Console.Write("  ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("Server URL      : ");
        Console.ResetColor();
        Console.WriteLine(serverUrl);
        
        Console.Write("  ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("Tenant ID       : ");
        Console.ResetColor();
        Console.WriteLine(certInfo.TenantId);
        
        Console.Write("  ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("User ID         : ");
        Console.ResetColor();
        Console.WriteLine(certInfo.UserId);
        
        Console.Write("  ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("Subject         : ");
        Console.ResetColor();
        Console.WriteLine(certInfo.Subject);
        
        Console.Write("  ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("Certificate     : ");
        Console.ResetColor();
        var thumbprint = certInfo.Thumbprint.Substring(0, Math.Min(16, certInfo.Thumbprint.Length)) + "...";
        Console.WriteLine(thumbprint);
        
        Console.Write("  ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("Expires         : ");
        Console.ResetColor();
        var expiryColor = (certInfo.ExpiresAt - DateTime.UtcNow).TotalDays < 30 ? ConsoleColor.Red : ConsoleColor.Green;
        Console.ForegroundColor = expiryColor;
        var expiryText = certInfo.ExpiresAt.ToString("yyyy-MM-dd HH:mm:ss UTC");
        Console.WriteLine(expiryText);
        Console.ResetColor();
        
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"└{new string('─', boxWidth)}┘");
        Console.ResetColor();
        Console.WriteLine();
    }
}
