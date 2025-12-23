using Microsoft.Extensions.Logging;
using Temporalio.Client;

namespace Xians.Lib.Temporal;

/// <summary>
/// Manages health checking for Temporal client connections.
/// </summary>
internal class TemporalConnectionHealth
{
    private readonly ILogger? _logger;

    public TemporalConnectionHealth(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Checks if the current Temporal connection is healthy.
    /// </summary>
    public bool IsConnectionHealthy(ITemporalClient? client)
    {
        try
        {
            if (client == null)
                return false;

            return client.Connection.IsConnected;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Temporal connection health check failed");
            return false;
        }
    }
}

