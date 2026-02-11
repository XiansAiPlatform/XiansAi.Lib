using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using DotNetEnv;
using Xians.Lib.Common.Testing;
using Xians.Lib.Agents.Core;

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

    /// <summary>
    /// Set to true when server connection fails during InitializeAsync.
    /// Tests should check this and return early to skip gracefully.
    /// </summary>
    protected bool SkipDueToServerUnavailable { get; set; }

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
    /// Wraps async initialization and catches server connection failures.
    /// When the server is unreachable, sets SkipDueToServerUnavailable and returns false.
    /// </summary>
    protected async Task<bool> TryInitializeAsync(Func<Task> initialize)
    {
        try
        {
            await initialize();
            return true;
        }
        catch (HttpRequestException ex) when (
            ex.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused })
        {
            SkipDueToServerUnavailable = true;
            Console.WriteLine($"⚠ Skipping RealServer tests: Server unreachable ({ServerUrl})");
            return false;
        }
        catch (HttpRequestException ex) when (
            ex.Message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase))
        {
            SkipDueToServerUnavailable = true;
            Console.WriteLine($"⚠ Skipping RealServer tests: Server unreachable ({ServerUrl})");
            return false;
        }
    }

    /// <summary>
    /// Helper method to create XiansOptions for testing.
    /// Server logging is not enabled by default, so no explicit configuration needed.
    /// </summary>
    protected XiansOptions CreateTestOptions()
    {
        return new XiansOptions
        {
            ServerUrl = ServerUrl!,
            ApiKey = ApiKey!
        };
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



