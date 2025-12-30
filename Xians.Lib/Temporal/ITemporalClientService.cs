using Temporalio.Client;

namespace Xians.Lib.Temporal;

/// <summary>
/// Interface for Temporal client service with resilient connection management.
/// </summary>
public interface ITemporalClientService : IDisposable
{
    /// <summary>
    /// Gets the Temporal client, automatically connecting if necessary.
    /// </summary>
    /// <returns>A connected Temporal client instance.</returns>
    Task<ITemporalClient> GetClientAsync();

    /// <summary>
    /// Checks if the current connection is healthy.
    /// </summary>
    /// <returns>True if the connection is healthy; otherwise, false.</returns>
    bool IsConnectionHealthy();

    /// <summary>
    /// Forces a reconnection on the next GetClientAsync call.
    /// </summary>
    Task ForceReconnectAsync();

    /// <summary>
    /// Disconnects from the Temporal server.
    /// </summary>
    Task DisconnectAsync();
}



