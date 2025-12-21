using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Xians.Lib.Configuration;

namespace Xians.Lib.Http;


/// <summary>
/// Implements a resilient HTTP client service with automatic retry, health checking, and reconnection.
/// </summary>
public class HttpClientService : IHttpClientService
{
    private readonly ILogger<HttpClientService> _logger;
    private readonly ServerConfiguration _config;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    
    private HttpClient? _client;
    private X509Certificate2? _clientCertificate;
    private bool _isInitialized;
    private bool _isHealthy = true;
    private bool _disposed;
    private DateTime _lastHealthCheck = DateTime.MinValue;

    public HttpClientService(ServerConfiguration config, ILogger<HttpClientService> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Validate configuration
        _config.Validate();

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
            if (!IsConnectionHealthy())
            {
                _logger.LogWarning("HTTP client connection is unhealthy, will attempt reconnection on next operation");
                _isInitialized = false;
                _isHealthy = false;
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
        if (_isInitialized && IsConnectionHealthy())
            return _client!;

        await _semaphore.WaitAsync();
        try
        {
            // Double-check pattern with health check
            if (_isInitialized && IsConnectionHealthy())
                return _client!;
            
            // Recreate client with retry logic
            await RecreateClientWithRetryAsync();
            _isInitialized = true;
            _isHealthy = true;
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

        // Use cached health status if recent
        var healthCheckInterval = TimeSpan.FromMinutes(_config.HealthCheckIntervalMinutes);
        if (DateTime.UtcNow - _lastHealthCheck < healthCheckInterval && _isHealthy)
            return true;

        try
        {
            _lastHealthCheck = DateTime.UtcNow;
            
            if (_client == null)
            {
                _isHealthy = false;
                return false;
            }
            
            // Perform lightweight health check
            using var request = new HttpRequestMessage(HttpMethod.Head, "/health");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var response = await _client.SendAsync(
                request, 
                HttpCompletionOption.ResponseHeadersRead, 
                cts.Token);
            
            _isHealthy = response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound;
            
            if (!_isHealthy)
            {
                _logger.LogWarning("Health check failed with status: {StatusCode}", response.StatusCode);
            }
            
            return _isHealthy;
        }
        catch (Exception ex)
        {
            _isHealthy = false;
            _logger.LogWarning(ex, "Health check failed due to exception");
            return false;
        }
    }

    /// <summary>
    /// Executes an operation with retry logic.
    /// </summary>
    public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt < _config.MaxRetryAttempts)
        {
            attempt++;
            
            try
            {
                if (attempt > 1)
                {
                    var delay = TimeSpan.FromMilliseconds(
                        _config.RetryDelaySeconds * 1000 * Math.Pow(2, attempt - 2));
                    _logger.LogInformation(
                        "Retrying operation (attempt {Attempt}/{MaxAttempts}) after {Delay}ms", 
                        attempt, _config.MaxRetryAttempts, delay.TotalMilliseconds);
                    await Task.Delay(delay);
                }

                var result = await operation();
                
                // Reset health status on success
                _isHealthy = true;
                
                if (attempt > 1)
                {
                    _logger.LogInformation("Operation succeeded on attempt {Attempt}", attempt);
                }
                
                return result;
            }
            catch (Exception ex) when (IsTransientException(ex) && attempt < _config.MaxRetryAttempts)
            {
                lastException = ex;
                _isHealthy = false;
                _logger.LogWarning(ex, 
                    "Operation failed on attempt {Attempt}/{MaxAttempts}: {Message}", 
                    attempt, _config.MaxRetryAttempts, ex.Message);
            }
        }

        _logger.LogError(lastException, "Operation failed after {MaxAttempts} attempts", _config.MaxRetryAttempts);
        throw lastException ?? new InvalidOperationException("Operation failed after all retry attempts");
    }

    /// <summary>
    /// Executes an operation with retry logic (no return value).
    /// </summary>
    public async Task ExecuteWithRetryAsync(Func<Task> operation)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await operation();
            return true;
        });
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
            _isHealthy = false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private bool IsConnectionHealthy()
    {
        if (!_isInitialized || _client == null)
            return false;

        // Use cached result if recent
        var healthCheckInterval = TimeSpan.FromMinutes(_config.HealthCheckIntervalMinutes);
        if (DateTime.UtcNow - _lastHealthCheck < healthCheckInterval && _isHealthy)
            return _isHealthy;

        try
        {
            _lastHealthCheck = DateTime.UtcNow;
            
            // Quick validation - check if we can create a request
            using var testRequest = new HttpRequestMessage(HttpMethod.Head, "/");
            _isHealthy = _client.BaseAddress != null && !_disposed;
            
            return _isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Connection health check failed");
            _isHealthy = false;
            _isInitialized = false;
            return false;
        }
    }

    private async Task RecreateClientWithRetryAsync()
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt < _config.MaxRetryAttempts)
        {
            attempt++;
            
            try
            {
                if (attempt > 1)
                {
                    var delay = TimeSpan.FromMilliseconds(
                        _config.RetryDelaySeconds * 1000 * Math.Pow(2, attempt - 2));
                    _logger.LogInformation(
                        "Retrying client recreation (attempt {Attempt}/{MaxAttempts}) after {Delay}ms", 
                        attempt, _config.MaxRetryAttempts, delay.TotalMilliseconds);
                    await Task.Delay(delay);
                }

                _logger.LogInformation(
                    "Attempting to recreate HTTP client (attempt {Attempt}/{MaxAttempts})", 
                    attempt, _config.MaxRetryAttempts);

                // Dispose old resources before recreating
                if (_client != null)
                {
                    _client.Dispose();
                    _client = null;
                }
                
                if (_clientCertificate != null)
                {
                    _clientCertificate.Dispose();
                    _clientCertificate = null;
                }
                
                // Recreate client
                CreateClient();
                
                _logger.LogInformation("HTTP client recreation succeeded on attempt {Attempt}", attempt);
                return;
            }
            catch (Exception ex) when (IsTransientException(ex) && attempt < _config.MaxRetryAttempts)
            {
                lastException = ex;
                _logger.LogWarning(ex, 
                    "Client recreation failed on attempt {Attempt}/{MaxAttempts}: {Message}", 
                    attempt, _config.MaxRetryAttempts, ex.Message);
            }
        }

        _logger.LogError(lastException, 
            "Client recreation failed after {MaxAttempts} attempts", _config.MaxRetryAttempts);
        throw lastException ?? new InvalidOperationException("Failed to recreate client after all retry attempts");
    }

    private void CreateClient()
    {
        // Configure SocketsHttpHandler with optimized settings
        var socketsHandler = new SocketsHttpHandler
        {
            MaxConnectionsPerServer = _config.MaxConnectionsPerServer,
            PooledConnectionLifetime = TimeSpan.FromMinutes(_config.PooledConnectionLifetimeMinutes),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(_config.PooledConnectionIdleTimeoutMinutes),
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | 
                                     System.Security.Authentication.SslProtocols.Tls13,
                RemoteCertificateValidationCallback = (sender, cert, chain, errors) =>
                {
                    if (errors == System.Net.Security.SslPolicyErrors.None)
                        return true;
                    
                    _logger.LogWarning("SSL certificate validation failed: {Errors}", errors);
                    return false;
                }
            }
        };

        _client = new HttpClient(socketsHandler)
        {
            BaseAddress = new Uri(_config.ServerUrl),
            Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds)
        };

        // Configure for faster shutdown - close connections after requests
        _client.DefaultRequestHeaders.ConnectionClose = true;

        try
        {
            // Dispose old certificate before creating new one
            _clientCertificate?.Dispose();
            _clientCertificate = null;
            
            // Parse API key as Base64-encoded certificate (required)
            // This matches XiansAi.Lib.Src behavior - no fallback to simple strings
            var apiKeyBytes = Convert.FromBase64String(_config.ApiKey);
            
            #pragma warning disable SYSLIB0057
            _clientCertificate = new X509Certificate2(apiKeyBytes);
            #pragma warning restore SYSLIB0057

            // Validate certificate expiration
            if (_clientCertificate.NotAfter < DateTime.UtcNow)
            {
                _logger.LogError("Client certificate expired on {ExpirationDate}", _clientCertificate.NotAfter);
                throw new InvalidOperationException(
                    $"Client certificate has expired on {_clientCertificate.NotAfter:yyyy-MM-dd}");
            }

            if (_clientCertificate.NotBefore > DateTime.UtcNow)
            {
                _logger.LogError("Client certificate not valid until {ValidFrom}", _clientCertificate.NotBefore);
                throw new InvalidOperationException(
                    $"Client certificate is not yet valid until {_clientCertificate.NotBefore:yyyy-MM-dd}");
            }

            // Export the certificate as Base64 and add it to the request headers for authentication
            // Note: This is application-specific auth. For true mTLS, consider using ClientCertificates on the handler
            var exportedCertBytes = _clientCertificate.Export(X509ContentType.Cert);
            var exportedCertBase64 = Convert.ToBase64String(exportedCertBytes);
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {exportedCertBase64}");
            
            _logger.LogDebug("Client certificate configured for authentication");
        }
        catch (Exception ex)
        {
            _client?.Dispose();
            _logger.LogError(ex, "Failed to configure HTTP client authentication. Ensure API_KEY is a valid Base64-encoded X.509 certificate.");
            throw new InvalidOperationException(
                "Failed to configure HTTP client authentication. API_KEY must be a Base64-encoded X.509 certificate.", 
                ex);
        }
    }

    private static bool IsTransientException(Exception ex)
    {
        return ex switch
        {
            HttpRequestException httpEx => IsTransientHttpException(httpEx),
            TaskCanceledException => true,
            SocketException => true,
            TimeoutException => true,
            _ => false
        };
    }

    private static bool IsTransientHttpException(HttpRequestException httpEx)
    {
        var message = httpEx.Message.ToLower();
        return message.Contains("timeout") || 
               message.Contains("connection") || 
               message.Contains("network") ||
               message.Contains("dns") ||
               message.Contains("ssl") ||
               message.Contains("certificate");
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


