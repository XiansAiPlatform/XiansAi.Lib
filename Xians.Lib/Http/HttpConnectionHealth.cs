using System.Net;
using Microsoft.Extensions.Logging;

namespace Xians.Lib.Http;

/// <summary>
/// Manages health checking for HTTP client connections.
/// </summary>
internal class HttpConnectionHealth
{
    private readonly ILogger? _logger;
    private readonly int _healthCheckIntervalMinutes;
    
    private bool _isHealthy = true;
    private DateTime _lastHealthCheck = DateTime.MinValue;

    public HttpConnectionHealth(int healthCheckIntervalMinutes, ILogger? logger = null)
    {
        _healthCheckIntervalMinutes = healthCheckIntervalMinutes;
        _logger = logger;
    }

    public bool IsHealthy => _isHealthy;

    /// <summary>
    /// Performs a synchronous health check based on client state.
    /// </summary>
    public bool CheckConnectionHealth(HttpClient? client, bool isInitialized, bool isDisposed)
    {
        if (!isInitialized || client == null || isDisposed)
            return false;

        // Use cached result if recent
        var healthCheckInterval = TimeSpan.FromMinutes(_healthCheckIntervalMinutes);
        if (DateTime.UtcNow - _lastHealthCheck < healthCheckInterval && _isHealthy)
            return _isHealthy;

        _lastHealthCheck = DateTime.UtcNow;
        _isHealthy = client.BaseAddress != null && !isDisposed;
        
        return _isHealthy;
    }

    /// <summary>
    /// Performs an asynchronous health check by calling the /health endpoint.
    /// </summary>
    public async Task<bool> CheckHealthAsync(HttpClient? client)
    {
        if (client == null)
        {
            _isHealthy = false;
            return false;
        }

        // Use cached health status if recent
        var healthCheckInterval = TimeSpan.FromMinutes(_healthCheckIntervalMinutes);
        if (DateTime.UtcNow - _lastHealthCheck < healthCheckInterval && _isHealthy)
            return true;

        try
        {
            _lastHealthCheck = DateTime.UtcNow;
            
            // Perform lightweight health check
            using var request = new HttpRequestMessage(HttpMethod.Head, "/health");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var response = await client.SendAsync(
                request, 
                HttpCompletionOption.ResponseHeadersRead, 
                cts.Token);
            
            _isHealthy = response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound;
            
            if (!_isHealthy)
            {
                _logger?.LogWarning("Health check failed with status: {StatusCode}", response.StatusCode);
            }
            
            return _isHealthy;
        }
        catch (Exception ex)
        {
            _isHealthy = false;
            _logger?.LogWarning(ex, "Health check failed due to exception");
            return false;
        }
    }

    public void MarkUnhealthy()
    {
        _isHealthy = false;
    }

    public void MarkHealthy()
    {
        _isHealthy = true;
    }
}

