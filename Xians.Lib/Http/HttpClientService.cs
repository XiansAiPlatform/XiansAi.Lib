using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Xians.Lib.Common;
using Xians.Lib.Configuration.Models;
using Xians.Lib.Common.Infrastructure;

namespace Xians.Lib.Http;

/// <summary>
/// Implements a resilient HTTP client service with automatic retry, health checking, and reconnection.
/// </summary>
public class HttpClientService : IHttpClientService
{
    private readonly ILogger<HttpClientService> _logger;
    private readonly ServerConfiguration _config;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly HttpClientFactory _clientFactory;
    private readonly HttpConnectionHealth _connectionHealth;
    private readonly RetryPolicy _retryPolicy;
    
    private HttpClient? _client;
    private X509Certificate2? _clientCertificate;
    private bool _isInitialized;
    private bool _disposed;

    public HttpClientService(ServerConfiguration config, ILogger<HttpClientService> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Validate configuration
        _config.Validate();

        // Initialize helper classes
        _clientFactory = new HttpClientFactory(_config, _logger);
        _connectionHealth = new HttpConnectionHealth(_config.HealthCheckIntervalMinutes, _logger);
        _retryPolicy = new RetryPolicy(_config.MaxRetryAttempts, _config.RetryDelaySeconds, _logger);

        // Initialize the client
        try
        {
            CreateClient();
            _isInitialized = true;
            _logger.LogInformation("HTTP client service initialized successfully for {ServerUrl}", _config.ServerUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize HTTP client service");
            throw;
        }
    }

    /// <summary>
    /// Gets the configured HTTP client.
    /// </summary>
    public HttpClient Client
    {
        get
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HttpClientService));

            // Check health before returning
            if (!_connectionHealth.CheckConnectionHealth(_client, _isInitialized, _disposed))
            {
                _logger.LogWarning("HTTP client connection is unhealthy, will attempt reconnection on next operation");
                _isInitialized = false;
                _connectionHealth.MarkUnhealthy();
            }

            return _client!;
        }
    }

    /// <summary>
    /// Gets a healthy HTTP client, automatically reconnecting if necessary.
    /// </summary>
    public async Task<HttpClient> GetHealthyClientAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(HttpClientService));

        // Check if current client is healthy
        if (_isInitialized && _connectionHealth.CheckConnectionHealth(_client, _isInitialized, _disposed))
            return _client!;

        await _semaphore.WaitAsync();
        try
        {
            // Double-check pattern with health check
            if (_isInitialized && _connectionHealth.CheckConnectionHealth(_client, _isInitialized, _disposed))
                return _client!;
            
            // Recreate client with retry logic
            await RecreateClientWithRetryAsync();
            _isInitialized = true;
            _connectionHealth.MarkHealthy();
            _logger.LogInformation("HTTP client recreated successfully after connection recovery");
            return _client!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recreate HTTP client after all retry attempts");
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Tests the connection to the server.
    /// </summary>
    public async Task TestConnectionAsync()
    {
        await ExecuteWithRetryAsync(async () =>
        {
            var client = await GetHealthyClientAsync();
            using var request = new HttpRequestMessage(HttpMethod.Head, "/");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException($"Connection test failed with status: {response.StatusCode}");
            }
        });
    }

    /// <summary>
    /// Performs an asynchronous health check.
    /// </summary>
    public async Task<bool> IsHealthyAsync()
    {
        if (_disposed)
            return false;

        return await _connectionHealth.CheckHealthAsync(_client);
    }

    /// <summary>
    /// Executes an operation with retry logic.
    /// </summary>
    public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
    {
        var result = await _retryPolicy.ExecuteAsync(operation);
        
        // Update health status on success
        _connectionHealth.MarkHealthy();
        
        return result;
    }

    /// <summary>
    /// Executes an operation with retry logic (no return value).
    /// </summary>
    public async Task ExecuteWithRetryAsync(Func<Task> operation)
    {
        await _retryPolicy.ExecuteAsync(operation);
        
        // Update health status on success
        _connectionHealth.MarkHealthy();
    }

    /// <summary>
    /// Forces a reconnection on the next operation.
    /// </summary>
    public async Task ForceReconnectAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            _logger.LogInformation("Forcing HTTP client reconnection");
            _client?.Dispose();
            _client = null;
            _isInitialized = false;
            _connectionHealth.MarkUnhealthy();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task RecreateClientWithRetryAsync()
    {
        await _retryPolicy.ExecuteAsync(() =>
        {
            _logger.LogInformation("Attempting to recreate HTTP client");

            // Dispose old resources before recreating
            _client?.Dispose();
            _client = null;
            
            _clientCertificate?.Dispose();
            _clientCertificate = null;
            
            // Recreate client
            CreateClient();
            
            _logger.LogInformation("HTTP client recreation succeeded");
            return Task.CompletedTask;
        });
    }

    private void CreateClient()
    {
        var (client, certificate) = _clientFactory.CreateClient();
        
        // Dispose old resources
        _clientCertificate?.Dispose();
        
        _client = client;
        _clientCertificate = certificate;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the HttpClientService and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) 
            return;

        if (disposing)
        {
            try
            {
                // Dispose managed resources
                _client?.Dispose();
                _client = null;
                
                _clientCertificate?.Dispose();
                _clientCertificate = null;
                
                _semaphore?.Dispose();
                
                _logger.LogInformation("HTTP client service disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error occurred during HTTP client service disposal");
            }
        }

        _disposed = true;
    }

    /// <summary>
    /// Finalizer to ensure resources are properly cleaned up if Dispose is not called.
    /// </summary>
    ~HttpClientService() => Dispose(false);
}
