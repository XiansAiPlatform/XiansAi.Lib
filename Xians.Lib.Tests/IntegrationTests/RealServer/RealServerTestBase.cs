using DotNetEnv;

namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// Base class for real server integration tests.
/// Provides common setup for loading .env credentials.
/// </summary>
public abstract class RealServerTestBase
{
    protected readonly bool RunRealServerTests;
    protected readonly string? ServerUrl;
    protected readonly string? ApiKey;

    protected RealServerTestBase()
    {
        // Load .env file
        try
        {
            Env.Load();
        }
        catch
        {
            // .env file may not exist
        }

        ServerUrl = Environment.GetEnvironmentVariable("SERVER_URL");
        ApiKey = Environment.GetEnvironmentVariable("API_KEY");
        
        // Only run if we have valid credentials
        RunRealServerTests = !string.IsNullOrEmpty(ServerUrl) && 
                             !string.IsNullOrEmpty(ApiKey);
    }
}


