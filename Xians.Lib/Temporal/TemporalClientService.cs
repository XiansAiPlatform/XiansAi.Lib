using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Xians.Lib.Common;
using Xians.Lib.Configuration.Models;
using Xians.Lib.Common.Infrastructure;

namespace Xians.Lib.Temporal;

/// <summary>
/// Implements a resilient Temporal client service with automatic retry and reconnection.
/// </summary>
public class TemporalClientService : ITemporalClientService
{
    private readonly ILogger<TemporalClientService> _logger;
    private readonly TemporalConfiguration _config;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly TemporalClientFactory _clientFactory;
    private readonly TemporalConnectionHealth _connectionHealth;
    private readonly RetryPolicy _retryPolicy;
    
    private ITemporalClient? _client;
    private bool _isInitialized;
    private bool _disposed;

    public TemporalClientService(TemporalConfiguration config, ILogger<TemporalClientService> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Validate configuration
        _config.Validate();

        // Initialize helper classes
        _clientFactory = new TemporalClientFactory(_config, _logger);
        _connectionHealth = new TemporalConnectionHealth(_logger);
        _retryPolicy = new RetryPolicy(
            _config.MaxRetryAttempts, 
            _config.RetryDelaySeconds,
            _logger);

        _logger.LogDebug("Temporal client service initialized (lazy connection)");
    }

    /// <summary>
    /// Gets the Temporal client, automatically connecting if necessary.
    /// </summary>
    public async Task<ITemporalClient> GetClientAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TemporalClientService));

        if (_isInitialized && _client != null && _connectionHealth.IsConnectionHealthy(_client))
            return _client;

        await _semaphore.WaitAsync();
        try
        {
            // Double-check pattern with health check
            if (_isInitialized && _client != null && _connectionHealth.IsConnectionHealthy(_client))
                return _client;
            
            _client = await _retryPolicy.ExecuteAsync(() => _clientFactory.CreateClientAsync());
            _isInitialized = true;
            
            _logger.LogInformation(
                "Temporal client connection established to namespace '{Namespace}' successfully", 
                _client.Options.Namespace);
            
            return _client;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create or reconnect Temporal client connection after all retry attempts");
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Checks if the current connection is healthy.
    /// </summary>
    public bool IsConnectionHealthy()
    {
        // Return false if already disposed
        if (_disposed)
        {
            return false;
        }
        
        if (!_connectionHealth.IsConnectionHealthy(_client))
        {
            // Use semaphore to prevent race condition with GetClientAsync
            _semaphore.Wait();
            try
            {
                // Double-check pattern
                if (!_connectionHealth.IsConnectionHealthy(_client))
                {
                    // Mark as unhealthy and reset state for reconnection
                    _isInitialized = false;
                    _client = null;
                    return false;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        return true;
    }

    /// <summary>
    /// Forces a reconnection on the next GetClientAsync call.
    /// </summary>
    public async Task ForceReconnectAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            _logger.LogInformation("Forcing Temporal client reconnection");
            _client = null;
            _isInitialized = false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Disconnects from the Temporal server.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_disposed || _client == null) 
            return;

        try
        {
            await _semaphore.WaitAsync();
            try
            {
                if (_client != null && !_disposed)
                {
                    _logger.LogInformation("Disconnecting from Temporal server");
                    _client = null;
                    _isInitialized = false;
                }
            }
            finally
            {
                if (!_disposed)
                {
                    _semaphore.Release();
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // Semaphore was already disposed, ignore
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the TemporalClientService and optionally releases the managed resources.
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
                // Disconnect from Temporal server
                DisconnectAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during Temporal client disposal");
            }

            try
            {
                _semaphore?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing semaphore");
            }
            
            _logger.LogInformation("Temporal client service disposed successfully");
        }

        _disposed = true;
    }

    /// <summary>
    /// Finalizer to ensure resources are properly cleaned up if Dispose is not called.
    /// </summary>
    ~TemporalClientService() => Dispose(false);
}
