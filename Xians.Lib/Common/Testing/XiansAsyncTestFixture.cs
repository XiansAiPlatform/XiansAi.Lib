namespace Xians.Lib.Common.Testing;

/// <summary>
/// Base class for Xians async test fixtures (xUnit IAsyncLifetime).
/// Provides automatic cleanup with async initialization support.
/// </summary>
/// <remarks>
/// Usage with xUnit IAsyncLifetime:
/// <code>
/// public class MyAsyncTests : XiansAsyncTestFixture, IAsyncLifetime
/// {
///     public async Task InitializeAsync()
///     {
///         await base.InitializeAsync();
///         // Your async setup here
///     }
///     
///     public async Task DisposeAsync()
///     {
///         // Your async cleanup here
///         await base.DisposeAsync();
///     }
///     
///     [Fact]
///     public async Task MyTest()
///     {
///         // Test code - static state is clean
///     }
/// }
/// </code>
/// </remarks>
public abstract class XiansAsyncTestFixture
{
    private bool _disposed;

    /// <summary>
    /// Initializes the fixture with clean static state.
    /// Call this from your InitializeAsync implementation.
    /// </summary>
    protected virtual Task InitializeAsync()
    {
        // Ensure clean state before test
        TestCleanup.ResetAllStaticState();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Cleans up static state after test.
    /// Call this from your DisposeAsync implementation.
    /// </summary>
    protected virtual Task DisposeAsync()
    {
        if (_disposed)
            return Task.CompletedTask;

        // Clean up static state after test
        TestCleanup.ResetAllStaticState();
        
        _disposed = true;
        return Task.CompletedTask;
    }
}
