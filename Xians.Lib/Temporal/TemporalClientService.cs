using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Xians.Lib.Configuration;

namespace Xians.Lib.Temporal;

/// <summary>
/// Implements a resilient Temporal client service with automatic retry and reconnection.
/// </summary>
public class TemporalClientService : ITemporalClientService
{
    private readonly ILogger<TemporalClientService> _logger;
    private readonly TemporalConfiguration _config;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    
    private ITemporalClient? _client;
    private bool _isInitialized;
    private bool _disposed;
    private DateTime _lastConnectionAttempt = DateTime.MinValue;

    public TemporalClientService(TemporalConfiguration config, ILogger<TemporalClientService> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Validate configuration
        _config.Validate();

        _logger.LogDebug("Temporal client service initialized (lazy connection)");
    }

    /// <summary>
    /// Gets the Temporal client, automatically connecting if necessary.
    /// </summary>
    public async Task<ITemporalClient> GetClientAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TemporalClientService));

        if (_isInitialized && _client != null && IsConnectionHealthy())
            return _client;

        await _semaphore.WaitAsync();
        try
        {
            // Double-check pattern with health check
            if (_isInitialized && _client != null && IsConnectionHealthy())
                return _client;
            
            _client = await CreateClientWithRetryAsync();
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
        try
        {
            if (_client == null)
                return false;

            return _client.Connection.IsConnected;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Temporal connection health check failed, will attempt reconnection");
            
            // Mark as unhealthy and reset state for reconnection
            _isInitialized = false;
            _client = null;
            return false;
        }
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
                    // ITemporalClient doesn't implement IDisposable
                    // Just set to null to allow GC to handle cleanup
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

    private async Task<ITemporalClient> CreateClientWithRetryAsync()
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt < _config.MaxRetryAttempts)
        {
            attempt++;
            
            try
            {
                // Respect retry delay between attempts
                if (attempt > 1)
                {
                    var timeSinceLastAttempt = DateTime.UtcNow - _lastConnectionAttempt;
                    var retryDelay = TimeSpan.FromSeconds(_config.RetryDelaySeconds);
                    
                    if (timeSinceLastAttempt < retryDelay)
                    {
                        var delayRemaining = retryDelay - timeSinceLastAttempt;
                        _logger.LogInformation(
                            "Waiting {Delay:F1}s before retry attempt {Attempt}", 
                            delayRemaining.TotalSeconds, attempt);
                        await Task.Delay(delayRemaining);
                    }
                }

                _lastConnectionAttempt = DateTime.UtcNow;
                _logger.LogDebug(
                    "Attempting to connect to Temporal server (attempt {Attempt}/{MaxAttempts})", 
                    attempt, _config.MaxRetryAttempts);
                
                return await CreateClientAsync();
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, 
                    "Connection attempt {Attempt}/{MaxAttempts} failed: {Message}", 
                    attempt, _config.MaxRetryAttempts, ex.Message);
                
                if (attempt == _config.MaxRetryAttempts)
                {
                    _logger.LogError(ex, "All {MaxAttempts} connection attempts failed", _config.MaxRetryAttempts);
                    break;
                }
            }
        }

        throw new InvalidOperationException(
            $"Failed to establish Temporal connection after {_config.MaxRetryAttempts} attempts", 
            lastException);
    }

    private async Task<ITemporalClient> CreateClientAsync()
    {
        var options = new TemporalClientConnectOptions(_config.ServerUrl)
        {
            Namespace = _config.Namespace,
            Tls = GetTlsConfig(),
            LoggerFactory = LoggerFactory.Create(builder =>
                builder
                    .AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ")
                    .SetMinimumLevel(LogLevel.Information))
        };

        _logger.LogDebug(
            "Connecting to Temporal server at {ServerUrl} with namespace {Namespace}", 
            _config.ServerUrl, _config.Namespace);

        return await TemporalClient.ConnectAsync(options);
    }

    private TlsOptions? GetTlsConfig()
    {
        if (!_config.IsTlsEnabled)
        {
            _logger.LogDebug("TLS is not enabled for Temporal connection");
            return null;
        }

        try
        {
            var cert = Convert.FromBase64String(_config.CertificateBase64!);
            var privateKey = Convert.FromBase64String(_config.PrivateKeyBase64!);
            
            _logger.LogDebug("TLS is enabled for Temporal connection");
            
            return new TlsOptions
            {
                ClientCert = cert,
                ClientPrivateKey = privateKey
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse TLS certificate or private key");
            throw new InvalidOperationException("Failed to configure TLS for Temporal connection", ex);
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


