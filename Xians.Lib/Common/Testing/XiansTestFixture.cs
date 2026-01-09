namespace Xians.Lib.Common.Testing;

/// <summary>
/// Base class for Xians test fixtures that require clean static state.
/// Automatically handles cleanup in Dispose to ensure test isolation.
/// </summary>
/// <remarks>
/// Usage with xUnit:
/// <code>
/// public class MyTests : XiansTestFixture
/// {
///     [Fact]
///     public void MyTest()
///     {
///         // Test code - static state is clean
///     }
///     // Cleanup happens automatically
/// }
/// </code>
/// </remarks>
public abstract class XiansTestFixture : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Initializes a new test fixture with clean static state.
    /// </summary>
    protected XiansTestFixture()
    {
        // Ensure clean state before test
        TestCleanup.ResetAllStaticState();
    }

    /// <summary>
    /// Cleans up static state after test execution.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes resources and resets static state.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Clean up static state after test
            TestCleanup.ResetAllStaticState();
            
            // Allow derived classes to add cleanup
            OnDispose();
        }

        _disposed = true;
    }

    /// <summary>
    /// Override this to add custom cleanup logic in derived fixtures.
    /// Called before static state cleanup.
    /// </summary>
    protected virtual void OnDispose()
    {
        // Override in derived classes if needed
    }
}
