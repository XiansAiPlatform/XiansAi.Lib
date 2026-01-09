using DotNetEnv;
using Xians.Lib.Common.Testing;

namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// Base class for real server integration tests.
/// Provides common setup for loading .env credentials and automatic test cleanup.
/// </summary>
public abstract class RealServerTestBase : IDisposable
{
    protected readonly bool RunRealServerTests;
    protected readonly string? ServerUrl;
    protected readonly string? ApiKey;
    private bool _disposed;

    protected RealServerTestBase()
    {
        // Clean static state before test to prevent contamination
        TestCleanup.ResetAllStaticState();
        
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

    /// <summary>
    /// Cleans up static state after test to ensure isolation.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Override this to add custom cleanup in derived test classes.
    /// Always call base.Dispose(disposing) to ensure static state cleanup.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            TestCleanup.ResetAllStaticState();
        }

        _disposed = true;
    }
}



