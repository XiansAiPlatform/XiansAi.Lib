using Microsoft.Extensions.Logging;
using Agentri.Server;
using DotNetEnv;


namespace Agentri.SDK.Tests.IntegrationTests;

[Collection("SecureApi Tests")]
public class SystemActivitiesTests
{
    private readonly ILoggerFactory _loggerFactory;
    //private readonly SystemActivities _systemActivities;
    private readonly ThreadHistoryService _threadHistoryService;
    private readonly string _certificateBase64;
    private readonly string _serverUrl;
    private readonly ILogger<SystemActivitiesTests> _logger;

    /*
    dotnet test --filter "FullyQualifiedName~SystemActivitiesTests"
    */
    public SystemActivitiesTests()
    {
        // Reset SecureApi to ensure clean state
        SecureApi.Reset();

        // Load environment variables
        Env.Load();

        // Get values from environment for SecureApi
        _certificateBase64 = Environment.GetEnvironmentVariable("APP_SERVER_API_KEY") ?? 
            throw new InvalidOperationException("APP_SERVER_API_KEY environment variable is not set");
        _serverUrl = Environment.GetEnvironmentVariable("APP_SERVER_URL") ?? 
            throw new InvalidOperationException("APP_SERVER_URL environment variable is not set");

        // Set up logger
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = _loggerFactory.CreateLogger<SystemActivitiesTests>();

        // Set the global LogFactory
        typeof(Globals).GetProperty("LogFactory")?.SetValue(null, _loggerFactory);

        // Initialize SecureApi with real credentials
        SecureApi.InitializeClient(_certificateBase64, _serverUrl, forceReinitialize: true);

        // Create the system activities instance
        _threadHistoryService = new ThreadHistoryService();
        //_systemActivities = new SystemActivities();
    }

} 