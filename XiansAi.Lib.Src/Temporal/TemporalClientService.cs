using Microsoft.Extensions.Logging;
using Server;
using Temporalio.Client;

namespace Temporal;

public class TemporalClientService : IDisposable
{
    private static readonly ILogger _logger = Globals.LogFactory.CreateLogger<TemporalClientService>();
    private static readonly Lazy<TemporalClientService> _instance = new(() => new TemporalClientService());
    private ITemporalClient? _client;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _isInitialized;
    private bool _disposed;
    private DateTime _lastConnectionAttempt = DateTime.MinValue;
    private readonly TimeSpan _connectionRetryDelay = TimeSpan.FromSeconds(5);
    private readonly int _maxRetryAttempts = 3;

    private TemporalClientService() {}

    public static TemporalClientService Instance => _instance.Value;

    public async Task<ITemporalClient> GetClientAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TemporalClientService));

        if (_isInitialized && _client != null && IsConnectionHealthyAsync(_client)) 
            return _client;

        await _semaphore.WaitAsync();
        try
        {
            // Double-check pattern with health check
            if (_isInitialized && _client != null && IsConnectionHealthyAsync(_client)) 
                return _client;
            
            _client = await CreateClientWithRetryAsync();
            _isInitialized = true;
            _logger.LogInformation($"Temporal client connection established to namespace `{_client.Options.Namespace}` successfully");
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

    private bool IsConnectionHealthyAsync(ITemporalClient client)
    {
        try
        {
            return client.Connection.IsConnected;
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

    private async Task<ITemporalClient> CreateClientWithRetryAsync()
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt < _maxRetryAttempts)
        {
            attempt++;
            
            try
            {
                // Respect retry delay between attempts
                if (attempt > 1)
                {
                    var timeSinceLastAttempt = DateTime.UtcNow - _lastConnectionAttempt;
                    if (timeSinceLastAttempt < _connectionRetryDelay)
                    {
                        var delayRemaining = _connectionRetryDelay - timeSinceLastAttempt;
                        _logger.LogInformation($"Waiting {delayRemaining.TotalSeconds:F1}s before retry attempt {attempt}");
                        await Task.Delay(delayRemaining);
                    }
                }

                _lastConnectionAttempt = DateTime.UtcNow;
                _logger.LogDebug($"Attempting to connect to Temporal server (attempt {attempt}/{_maxRetryAttempts})");
                
                return await CreateClientAsync();
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, $"Connection attempt {attempt}/{_maxRetryAttempts} failed: {ex.Message}");
                
                if (attempt == _maxRetryAttempts)
                {
                    _logger.LogError(ex, $"All {_maxRetryAttempts} connection attempts failed");
                    break;
                }
            }
        }

        throw new InvalidOperationException($"Failed to establish Temporal connection after {_maxRetryAttempts} attempts", lastException);
    }

    private static async Task<ITemporalClient> CreateClientAsync()
    {
        var settings = await SettingsService.GetSettingsFromServer();

        var options = new TemporalClientConnectOptions(settings.FlowServerUrl)
        {
            Namespace = settings.FlowServerNamespace,
            Tls = getTlsConfig(settings), 
            LoggerFactory = LoggerFactory.Create(builder =>
                builder.
                    AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
                    SetMinimumLevel(LogLevel.Information)),
        };

        _logger.LogDebug($"Connecting to flow server at {settings.FlowServerUrl} with namespace {settings.FlowServerNamespace}");

        return await TemporalClient.ConnectAsync(options);
    }

    private static TlsOptions? getTlsConfig(ServerSettings settings)
    {
        if (settings.FlowServerCertBase64 == null || settings.FlowServerPrivateKeyBase64 == null)
        {
            return null;
        }
        var certBase64 = settings.FlowServerCertBase64;
        var cert = Convert.FromBase64String(certBase64);
        var privateKeyBase64 = settings.FlowServerPrivateKeyBase64;
        var privateKey = Convert.FromBase64String(privateKeyBase64);
        return new()
        {
            ClientCert = cert,
            ClientPrivateKey = privateKey,
        };
    }

    public async Task DisconnectAsync()
    {
        if (_disposed || _client == null) return;

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

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            DisconnectAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during temporal client disposal");
        }

        _semaphore?.Dispose();
        _disposed = true;
    }

    ~TemporalClientService()
    {
        Dispose();
    }

    /// <summary>
    /// Static method to ensure proper cleanup for command line applications
    /// </summary>
    public static async Task CleanupAsync()
    {
        if (_instance.IsValueCreated && !_instance.Value._disposed)
        {
            await _instance.Value.DisconnectAsync();
            _instance.Value.Dispose();
        }
    }

    /// <summary>
    /// Forces a reconnection on the next GetClientAsync call
    /// Useful for testing or when you know the connection has issues
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
}