using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

namespace Server;

/// <summary>
/// Defines a client for secure API communications.
/// </summary>
public interface ISecureApiClient
{
    /// <summary>
    /// Gets the underlying HTTP client configured with security settings.
    /// </summary>
    HttpClient Client { get; }

    /// <summary>
    /// Tests the connection to the server with retry logic.
    /// </summary>
    Task TestConnection();

    /// <summary>
    /// Performs a health check on the connection.
    /// </summary>
    Task<bool> IsHealthyAsync();
}

/// <summary>
/// Implements a secure API client that uses certificate-based authentication.
/// This class is implemented as a singleton to ensure a single HTTP client is used throughout the application.
/// </summary>
public class SecureApi : ISecureApiClient, IDisposable
{
    private HttpClient? _client;
    private X509Certificate2? _clientCertificate;
    private readonly ILogger<SecureApi> _logger;
    private bool _disposed;
    
    // Connection resilience properties
    private readonly int _maxRetryAttempts = 3;
    private readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(2);
    private DateTime _lastHealthCheck = DateTime.MinValue;
    private readonly TimeSpan _healthCheckInterval = TimeSpan.FromMinutes(1);
    private bool _isHealthy = true;
    private bool _isInitialized = false;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    
    // Lazy-loaded singleton instance that requires explicit initialization before use
    private static SecureApi? _instance;
    private static readonly object _lock = new object();
    private static string? _currentServerUrl;
    private static string? _currentCertificate;

    /// <summary>
    /// Gets the configured HTTP client for making secure API requests.
    /// This now includes automatic health checking and reconnection for long-term resilience.
    /// </summary>
    public HttpClient Client
    {
        get
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SecureApi), "The SecureApi instance has been disposed. Please reinitialize the client.");
            }

            // Always check health before returning client - this enables automatic recovery
            if (!IsConnectionHealthy())
            {
                _logger.LogWarning("SecureApi connection is unhealthy, will attempt reconnection on next operation");
                // Mark for reconnection - actual reconnection happens in operations
                _isInitialized = false;
                _isHealthy = false;
            }

            return _client!;
        }
    }

    /// <summary>
    /// Performs a non-blocking health check on the current connection.
    /// If unhealthy, marks the connection for recreation.
    /// </summary>
    private bool IsConnectionHealthy()
    {
        if (!_isInitialized || _client == null)
        {
            return false;
        }

        // Use cached result if recent
        if (DateTime.UtcNow - _lastHealthCheck < _healthCheckInterval && _isHealthy)
        {
            return _isHealthy;
        }

        try
        {
            _lastHealthCheck = DateTime.UtcNow;
            
            // Quick connection validation - check if we can create a request
            using var testRequest = new HttpRequestMessage(HttpMethod.Head, "/");
            // Don't actually send it, just validate the client state
            _isHealthy = _client.BaseAddress != null && !_disposed;
            
            return _isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SecureApi health check failed");
            _isHealthy = false;
            _isInitialized = false; // Mark for reconnection
            return false;
        }
    }

    /// <summary>
    /// Gets a healthy HTTP client, automatically reconnecting if necessary.
    /// This method enables automatic recovery from long-term server outages.
    /// </summary>
    public async Task<HttpClient> GetHealthyClientAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SecureApi));

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
            _logger.LogInformation("SecureApi client recreated successfully after connection recovery");
            return _client!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recreate SecureApi client after all retry attempts");
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Recreates the HTTP client with retry logic for connection recovery.
    /// </summary>
    private async Task RecreateClientWithRetryAsync()
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt < _maxRetryAttempts)
        {
            attempt++;
            
            try
            {
                if (attempt > 1)
                {
                    var delay = TimeSpan.FromMilliseconds(_retryDelay.TotalMilliseconds * Math.Pow(2, attempt - 2));
                    _logger.LogInformation("Retrying SecureApi client recreation (attempt {Attempt}/{MaxAttempts}) after {Delay}ms", 
                        attempt, _maxRetryAttempts, delay.TotalMilliseconds);
                    await Task.Delay(delay);
                }

                _logger.LogInformation("Attempting to recreate SecureApi client (attempt {Attempt}/{MaxAttempts})", 
                    attempt, _maxRetryAttempts);

                // Dispose old client if it exists
                _client?.Dispose();
                
                // Recreate client with same configuration
                await CreateClientAsync(_currentCertificate!, _currentServerUrl!);
                
                // Test the new connection
                await TestConnectionInternal();
                
                _logger.LogInformation("SecureApi client recreation succeeded on attempt {Attempt}", attempt);
                return;
            }
            catch (Exception ex) when (IsTransientException(ex) && attempt < _maxRetryAttempts)
            {
                lastException = ex;
                _logger.LogWarning(ex, "SecureApi client recreation failed on attempt {Attempt}/{MaxAttempts}: {Message}", 
                    attempt, _maxRetryAttempts, ex.Message);
            }
        }

        _logger.LogError(lastException, "SecureApi client recreation failed after {MaxAttempts} attempts", _maxRetryAttempts);
        throw lastException ?? new InvalidOperationException("Failed to recreate SecureApi client after all retry attempts");
    }

    /// <summary>
    /// Creates the HTTP client with the specified configuration.
    /// </summary>
    private Task CreateClientAsync(string certificateBase64, string serverUrl)
    {
        // Configure HttpClient with optimized settings for console applications
        var handler = new SocketsHttpHandler()
        {
            // Connection pool settings for better resource management
            MaxConnectionsPerServer = 10,
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        };

        _client = new HttpClient(handler) { 
            BaseAddress = new Uri(serverUrl),
            // Reduce timeout for faster shutdown - 30 seconds should be sufficient
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Configure for faster shutdown
        _client.DefaultRequestHeaders.ConnectionClose = true; // Close connections after requests
        
        try
        {
            // Convert the Base64 string to a certificate
            var certificateBytes = Convert.FromBase64String(certificateBase64);
            
            // Suppress warning about X509Certificate2 constructor being obsolete in .NET Core 
            #pragma warning disable SYSLIB0057
            _clientCertificate?.Dispose(); // Dispose old certificate
            _clientCertificate = new X509Certificate2(certificateBytes);
            #pragma warning restore SYSLIB0057

            // Export the certificate as Base64 and add it to the request headers for authentication
            var exportedCertBytes = _clientCertificate.Export(X509ContentType.Cert);
            var exportedCertBase64 = Convert.ToBase64String(exportedCertBytes);
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {exportedCertBase64}");
            
        }
        catch (Exception ex)
        {
            _client?.Dispose();
            throw new InvalidOperationException("Failed to create SecureApi client", ex);
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Internal connection test without retry logic (used during client recreation).
    /// </summary>
    private async Task TestConnectionInternal()
    {
        var settings = await SettingsService.GetSettingsFromServer();
        if (settings == null)
        {
            throw new InvalidOperationException("Failed to get settings from server");
        }
        if (settings.FlowServerCertBase64 == null || settings.FlowServerUrl == null)
        {
            throw new InvalidOperationException("Failed to get settings from server: FlowServerCertBase64 or FlowServerUrl is null");
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        if (_disposed)
        {
            return false;
        }

        // Use cached health status if recent
        if (DateTime.UtcNow - _lastHealthCheck < _healthCheckInterval && _isHealthy)
        {
            return true;
        }

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
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, 
                CancellationToken.None);
            
            _isHealthy = response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound;
            
            if (!_isHealthy)
            {
                _logger.LogWarning("SecureApi health check failed with status: {StatusCode}", response.StatusCode);
            }
            
            return _isHealthy;
        }
        catch (Exception ex)
        {
            _isHealthy = false;
            _logger.LogWarning(ex, "SecureApi health check failed due to exception");
            return false;
        }
    }

    public async Task TestConnection()
    {
        await ExecuteWithRetryAsync(async () =>
        {
            var settings = await SettingsService.GetSettingsFromServer();
            if (settings == null)
            {
                throw new InvalidOperationException("Failed to get settings from server");
            }
            if (settings.FlowServerCertBase64 == null || settings.FlowServerUrl == null)
            {
                throw new InvalidOperationException("Failed to get settings from server: FlowServerCertBase64 or FlowServerUrl is null");
            }
        });
    }

    /// <summary>
    /// Executes an operation with retry logic for transient failures.
    /// </summary>
    internal async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt < _maxRetryAttempts)
        {
            attempt++;
            
            try
            {
                if (attempt > 1)
                {
                    var delay = TimeSpan.FromMilliseconds(_retryDelay.TotalMilliseconds * Math.Pow(2, attempt - 2));
                    _logger.LogInformation("Retrying SecureApi operation (attempt {Attempt}/{MaxAttempts}) after {Delay}ms", 
                        attempt, _maxRetryAttempts, delay.TotalMilliseconds);
                    await Task.Delay(delay);
                }

                var result = await operation();
                
                // Reset health status on success
                _isHealthy = true;
                
                if (attempt > 1)
                {
                    _logger.LogInformation("SecureApi operation succeeded on attempt {Attempt}", attempt);
                }
                
                return result;
            }
            catch (Exception ex) when (IsTransientException(ex) && attempt < _maxRetryAttempts)
            {
                lastException = ex;
                _isHealthy = false;
                _logger.LogWarning(ex, "SecureApi operation failed on attempt {Attempt}/{MaxAttempts}: {Message}", 
                    attempt, _maxRetryAttempts, ex.Message);
            }
        }

        _logger.LogError(lastException, "SecureApi operation failed after {MaxAttempts} attempts", _maxRetryAttempts);
        throw lastException ?? new InvalidOperationException("Operation failed after all retry attempts");
    }

    /// <summary>
    /// Executes an operation with retry logic for operations that don't return a value.
    /// </summary>
    internal async Task ExecuteWithRetryAsync(Func<Task> operation)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await operation();
            return true;
        });
    }

    /// <summary>
    /// Determines if an exception represents a transient failure that should be retried.
    /// </summary>
    private static bool IsTransientException(Exception ex)
    {
        return ex switch
        {
            HttpRequestException httpEx => IsTransientHttpException(httpEx),
            TaskCanceledException => true, // Timeout
            SocketException => true,
            TimeoutException => true,
            _ => false
        };
    }

    private static bool IsTransientHttpException(HttpRequestException httpEx)
    {
        // Check for common transient HTTP errors
        var message = httpEx.Message.ToLower();
        return message.Contains("timeout") || 
               message.Contains("connection") || 
               message.Contains("network") ||
               message.Contains("dns") ||
               message.Contains("ssl") ||
               message.Contains("certificate");
    }
    
    /// <summary>
    /// Gets the singleton instance of the SecureApi client.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if accessed before initialization.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    public static ISecureApiClient Instance
    {
        get
        {
            if (_instance == null)
            {
                throw new InvalidOperationException("SecureApi must be initialized before use");
            }
            if (_instance._disposed)
            {
                throw new ObjectDisposedException(nameof(SecureApi), "The SecureApi instance has been disposed. Please reinitialize the client.");
            }
            return _instance;
        }
    }

    /// <summary>
    /// Indicates whether the client is properly initialized and ready to use.
    /// </summary>
    public static bool IsReady => _instance?.Client != null && !_instance._disposed;

    /// <summary>
    /// Initializes a new instance of the SecureApi class.
    /// </summary>
    /// <param name="certificateBase64">The client certificate in Base64 encoded format.</param>
    /// <param name="serverUrl">The base URL of the server API.</param>
    /// <param name="logger">Logger for recording operational information.</param>
    /// <exception cref="ArgumentNullException">Thrown if any required parameter is null or empty.</exception>
    private SecureApi(string certificateBase64, string serverUrl, ILogger<SecureApi> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        if (string.IsNullOrEmpty(certificateBase64))
            throw new ArgumentNullException(nameof(certificateBase64));
            
        if (string.IsNullOrEmpty(serverUrl))
            throw new ArgumentNullException(nameof(serverUrl));

        try
        {
            // Use the async creation method for consistency
            CreateClientAsync(certificateBase64, serverUrl).GetAwaiter().GetResult();
            _isInitialized = true;
            _logger.LogInformation("Secure API connection to server initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SecureApi");
            throw;
        }
    }

    /// <summary>
    /// Initializes the singleton instance of the SecureApi client.
    /// This method must be called before using the Instance property.
    /// </summary>
    /// <param name="serverApiKey">The client certificate in Base64 encoded format.</param>
    /// <param name="serverUrl">The base URL of the server API.</param>
    /// <param name="forceReinitialize">Whether to force reinitialization even if already initialized.</param>
    /// <returns>The configured HTTP client instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if already initialized with different parameters.</exception>
    public static HttpClient InitializeClient(string serverApiKey, string serverUrl, bool forceReinitialize = false)
    {
        // Validate parameters first
        if (string.IsNullOrEmpty(serverApiKey))
            throw new ArgumentNullException(nameof(serverApiKey), "Server API key is required. Please set the APP_SERVER_API_KEY environment variable.");
            
        if (string.IsNullOrEmpty(serverUrl))
            throw new ArgumentNullException(nameof(serverUrl), "Server URL is required. Please set the APP_SERVER_URL environment variable.");

        lock (_lock)
        {
            if (_instance != null && !forceReinitialize)
            {
                // Check if the existing instance has the same configuration
                if (_currentServerUrl == serverUrl && _currentCertificate == serverApiKey)
                {
                    return _instance.Client;
                }
                throw new InvalidOperationException("SecureApi is already initialized with different parameters");
            }

            // Reset existing instance if forcing reinitialization
            if (forceReinitialize)
            {
                _instance?.Dispose();
                _instance = null;
            }

            var logger = Globals.LogFactory.CreateLogger<SecureApi>();
            _instance = new SecureApi(serverApiKey, serverUrl, logger);
            _currentServerUrl = serverUrl;
            _currentCertificate = serverApiKey;
            return _instance.Client;
        }
    }

    /// <summary>
    /// Resets the singleton instance. This is primarily for testing purposes.
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _instance?.Dispose();
            _instance = null;
            _currentServerUrl = null;
            _currentCertificate = null;
        }
    }

    /// <summary>
    /// Disposes the managed and unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the SecureApi and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            try
            {
                // Dispose managed resources
                _client?.Dispose();
                _clientCertificate?.Dispose();
                _semaphore?.Dispose();
                _logger.LogInformation("SecureApi disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error occurred during SecureApi disposal");
            }
        }

        _disposed = true;
    }

    /// <summary>
    /// Finalizer to ensure resources are properly cleaned up if Dispose is not called.
    /// </summary>
    ~SecureApi() => Dispose(false);
}

/// <summary>
/// Extension methods for SecureApi HttpClient to provide resilient operations.
/// </summary>
public static class SecureApiExtensions
{
    /// <summary>
    /// Sends an HTTP request with automatic retry logic for transient failures and connection recovery.
    /// </summary>
    public static async Task<HttpResponseMessage> SendWithRetryAsync(this HttpClient client, 
        HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var secureApi = SecureApi.Instance as SecureApi;
        if (secureApi == null)
        {
            return await client.SendAsync(request, cancellationToken);
        }

        return await secureApi.ExecuteWithRetryAsync(async () =>
        {
            // Ensure we have a healthy client before sending
            var healthyClient = await secureApi.GetHealthyClientAsync();
            var clonedRequest = await CloneHttpRequestMessageAsync(request);
            return await healthyClient.SendAsync(clonedRequest, cancellationToken);
        });
    }

    /// <summary>
    /// Performs a GET request with automatic retry logic.
    /// </summary>
    public static async Task<HttpResponseMessage> GetWithRetryAsync(this HttpClient client, 
        string requestUri, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        return await client.SendWithRetryAsync(request, cancellationToken);
    }

    /// <summary>
    /// Performs a POST request with automatic retry logic.
    /// </summary>
    public static async Task<HttpResponseMessage> PostWithRetryAsync(this HttpClient client, 
        string requestUri, HttpContent content, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri) { Content = content };
        return await client.SendWithRetryAsync(request, cancellationToken);
    }

    private static Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Content = original.Content,
            Version = original.Version
        };

        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in original.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        }

        return Task.FromResult(clone);
    }
} 