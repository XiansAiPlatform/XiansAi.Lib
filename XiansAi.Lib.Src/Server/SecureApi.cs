using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net;
using Temporalio.Workflows;

namespace Server;

/// <summary>
/// Defines a client for secure API communications with enhanced recovery capabilities.
/// </summary>
public interface ISecureApiClient
{
    /// <summary>
    /// Gets the underlying HTTP client configured with security settings.
    /// </summary>
    HttpClient Client { get; }

    /// <summary>
    /// Tests the connection to the server.
    /// </summary>
    Task TestConnection();

    /// <summary>
    /// Executes an HTTP request with conditional retry logic (only outside workflows).
    /// </summary>
    Task<HttpResponseMessage> ExecuteWithRetryAsync(Func<HttpClient, Task<HttpResponseMessage>> requestFunc, int maxRetries = 3);

    /// <summary>
    /// Checks if the connection is healthy and ready for requests.
    /// </summary>
    Task<bool> IsConnectionHealthy();

    /// <summary>
    /// Gets connection health status for observability.
    /// </summary>
    ConnectionHealthStatus GetConnectionHealthStatus();
}

/// <summary>
/// Connection health status for observability.
/// </summary>
public class ConnectionHealthStatus
{
    public bool IsHealthy { get; set; }
    public int FailureCount { get; set; }
    public DateTime LastFailureTime { get; set; }
    public bool IsCircuitBreakerOpen { get; set; }
    public TimeSpan CircuitBreakerTimeout { get; set; }
    public int TotalRequests { get; set; }
    public int FailedRequests { get; set; }
    public double SuccessRate => TotalRequests > 0 ? (double)(TotalRequests - FailedRequests) / TotalRequests : 1.0;
}

/// <summary>
/// Implements a secure API client that uses certificate-based authentication with enhanced recovery capabilities.
/// This class is implemented as a singleton to ensure a single HTTP client is used throughout the application.
/// </summary>
public class SecureApi : ISecureApiClient, IDisposable
{
    private readonly HttpClient _client;
    private readonly X509Certificate2 _clientCertificate;
    private readonly ILogger<SecureApi> _logger;
    private bool _disposed;
    
    // Connection recovery fields
    private DateTime _lastFailureTime = DateTime.MinValue;
    private int _failureCount = 0;
    private int _totalRequests = 0;
    private int _failedRequests = 0;
    private readonly object _healthLock = new object();
    
    // Configuration for recovery
    private const int MaxFailures = 5;
    private const int CircuitBreakerTimeoutSeconds = 60;
    private const int HealthCheckTimeoutSeconds = 10;
    private const int BaseRetryDelayMs = 1000;
    
    // Lazy-loaded singleton instance that requires explicit initialization before use
    private static SecureApi? _instance;
    private static readonly object _lock = new object();
    private static string? _currentServerUrl;
    private static string? _currentCertificate;

    /// <summary>
    /// Gets the configured HTTP client for making secure API requests.
    /// </summary>
    public HttpClient Client
    {
        get
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SecureApi), "The SecureApi instance has been disposed. Please reinitialize the client.");
            }
            return _client;
        }
    }

    /// <summary>
    /// Tests the connection to the server with enhanced error handling.
    /// </summary>
    public async Task TestConnection()
    {
        try
        {
            var settings = await SettingsService.GetSettingsFromServer();
            if (settings == null)
            {
                throw new Exception("Failed to get settings from server");
            }
            if (settings.FlowServerCertBase64 == null || settings.FlowServerUrl == null)
            {
                throw new Exception("Failed to get settings from server: FlowServerCertBase64 or FlowServerUrl is null");
            }
            
            // Perform a health check to ensure connection is working
            var isHealthy = await PerformHealthCheck();
            if (!isHealthy)
            {
                throw new Exception("Connection health check failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed");
            RecordFailure();
            throw;
        }
    }

    /// <summary>
    /// Checks if the connection is healthy and ready for requests.
    /// Simplified version that just checks circuit breaker state.
    /// </summary>
    public async Task<bool> IsConnectionHealthy()
    {
        lock (_healthLock)
        {
            // If circuit breaker is open, check if timeout has elapsed
            if (_failureCount >= MaxFailures)
            {
                var timeSinceLastFailure = DateTime.UtcNow - _lastFailureTime;
                if (timeSinceLastFailure > TimeSpan.FromSeconds(CircuitBreakerTimeoutSeconds))
                {
                    _logger.LogDebug("Circuit breaker timeout elapsed, resetting");
                    _failureCount = 0;
                }
                else
                {
                    _logger.LogDebug("Circuit breaker is open, {TimeRemaining} remaining", 
                        TimeSpan.FromSeconds(CircuitBreakerTimeoutSeconds) - timeSinceLastFailure);
                    return false;
                }
            }
        }

        // Only do health check if circuit breaker was recently reset
        if (_failureCount == 0)
        {
            try
            {
                var isHealthy = await PerformHealthCheck();
                if (isHealthy)
                {
                    RecordSuccess();
                    return true;
                }
                else
                {
                    RecordFailure();
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Health check failed");
                RecordFailure();
                return false;
            }
        }

        return true; // Circuit breaker closed, assume healthy
    }

    /// <summary>
    /// Gets connection health status for observability.
    /// </summary>
    public ConnectionHealthStatus GetConnectionHealthStatus()
    {
        lock (_healthLock)
        {
            var isCircuitBreakerOpen = _failureCount >= MaxFailures && 
                DateTime.UtcNow - _lastFailureTime <= TimeSpan.FromSeconds(CircuitBreakerTimeoutSeconds);
            
            return new ConnectionHealthStatus
            {
                IsHealthy = _failureCount < MaxFailures,
                FailureCount = _failureCount,
                LastFailureTime = _lastFailureTime,
                IsCircuitBreakerOpen = isCircuitBreakerOpen,
                CircuitBreakerTimeout = TimeSpan.FromSeconds(CircuitBreakerTimeoutSeconds),
                TotalRequests = _totalRequests,
                FailedRequests = _failedRequests
            };
        }
    }

    /// <summary>
    /// Performs a simple health check on the server connection.
    /// Uses a lightweight HEAD request to check if server is reachable.
    /// </summary>
    public async Task<bool> PerformHealthCheck()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(HealthCheckTimeoutSeconds));
            
            // Simple HEAD request to base URL - lightweight and fast
            // This just checks if the server is reachable, not business logic
            var request = new HttpRequestMessage(HttpMethod.Head, "");
            var response = await _client.SendAsync(request, cts.Token);
            
            // Any response (even 404) means server is reachable
            return response != null;
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException)
        {
            _logger.LogDebug("Network connection error during health check: {Message}", ex.Message);
            return false;
        }
        catch (TaskCanceledException)
        {
            _logger.LogDebug("Health check timed out");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Health check failed");
            return false;
        }
    }

    /// <summary>
    /// Executes an HTTP request with conditional retry logic (only outside workflows).
    /// Simplified version with basic retry and circuit breaker.
    /// </summary>
    public async Task<HttpResponseMessage> ExecuteWithRetryAsync(Func<HttpClient, Task<HttpResponseMessage>> requestFunc, int maxRetries = 3)
    {
        // Check if we're in a Temporal workflow
        var isInWorkflow = Workflow.InWorkflow;
        
        if (isInWorkflow)
        {
            _logger.LogDebug("In Temporal workflow - skipping retry logic");
            var response = await requestFunc(_client);
            RecordRequest(response.IsSuccessStatusCode);
            return response;
        }

        // Outside workflow - apply simple retry logic
        _logger.LogDebug("Outside Temporal workflow - applying retry logic");

        // Quick circuit breaker check
        if (_failureCount >= MaxFailures)
        {
            var timeSinceLastFailure = DateTime.UtcNow - _lastFailureTime;
            if (timeSinceLastFailure <= TimeSpan.FromSeconds(CircuitBreakerTimeoutSeconds))
            {
                throw new InvalidOperationException($"Circuit breaker is open - service unavailable for {CircuitBreakerTimeoutSeconds - timeSinceLastFailure.TotalSeconds:F0} more seconds");
            }
            _failureCount = 0; // Reset circuit breaker
        }

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await requestFunc(_client);
                
                if (response.IsSuccessStatusCode)
                {
                    RecordSuccess();
                    return response;
                }
                
                // Retry on server errors (5xx)
                if (response.StatusCode >= HttpStatusCode.InternalServerError && attempt < maxRetries)
                {
                    var delay = TimeSpan.FromMilliseconds(BaseRetryDelayMs * Math.Pow(2, attempt - 1));
                    _logger.LogDebug("Retrying after {Delay} due to status {StatusCode}", delay, response.StatusCode);
                    await Task.Delay(delay);
                    continue;
                }
                
                RecordRequest(false);
                return response;
            }
            catch (HttpRequestException ex) when (IsRetryableException(ex))
            {
                if (attempt < maxRetries)
                {
                    var delay = TimeSpan.FromMilliseconds(BaseRetryDelayMs * Math.Pow(2, attempt - 1));
                    _logger.LogDebug("Retrying after {Delay} due to network error", delay);
                    await Task.Delay(delay);
                    continue;
                }
                RecordFailure();
                throw;
            }
            catch (TaskCanceledException)
            {
                if (attempt < maxRetries)
                {
                    var delay = TimeSpan.FromMilliseconds(BaseRetryDelayMs * Math.Pow(2, attempt - 1));
                    _logger.LogDebug("Retrying after {Delay} due to timeout", delay);
                    await Task.Delay(delay);
                    continue;
                }
                RecordFailure();
                throw;
            }
        }
        
        throw new InvalidOperationException($"Request failed after {maxRetries} attempts");
    }

    /// <summary>
    /// Records a successful operation to reset circuit breaker.
    /// </summary>
    private void RecordSuccess()
    {
        lock (_healthLock)
        {
            _failureCount = 0;
            _totalRequests++;
        }
    }

    /// <summary>
    /// Records a failure and potentially opens the circuit breaker.
    /// </summary>
    private void RecordFailure()
    {
        lock (_healthLock)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;
            _totalRequests++;
            _failedRequests++;
            
            if (_failureCount >= MaxFailures)
            {
                _logger.LogWarning("Circuit breaker opened after {FailureCount} consecutive failures", _failureCount);
            }
        }
    }

    /// <summary>
    /// Records a request attempt for observability.
    /// </summary>
    private void RecordRequest(bool isSuccess)
    {
        lock (_healthLock)
        {
            _totalRequests++;
            if (!isSuccess)
            {
                _failedRequests++;
            }
        }
    }

    /// <summary>
    /// Determines if a status code is retryable.
    /// </summary>
    public static bool IsRetryableStatusCode(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout ||
               statusCode == HttpStatusCode.InternalServerError ||
               statusCode == HttpStatusCode.BadGateway ||
               statusCode == HttpStatusCode.ServiceUnavailable ||
               statusCode == HttpStatusCode.GatewayTimeout;
    }

    /// <summary>
    /// Determines if an exception is retryable.
    /// </summary>
    public static bool IsRetryableException(HttpRequestException ex)
    {
        return ex.InnerException is SocketException ||
               ex.InnerException is System.Net.WebException ||
               ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
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
    /// Initializes a new instance of the SecureApi class with enhanced connection management.
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

        // Configure HttpClient with better connection management
        var handler = new HttpClientHandler
        {
            // Enable connection pooling
            UseProxy = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        _client = new HttpClient(handler)
        { 
            BaseAddress = new Uri(serverUrl),
            Timeout = TimeSpan.FromSeconds(30),
            // Add default headers for better connection management
            DefaultRequestHeaders = 
            {
                { "Connection", "keep-alive" },
                { "User-Agent", "XiansAi.Lib/1.0" }
            }
        };

        try
        {
            // Convert the Base64 string to a certificate
            var certificateBytes = Convert.FromBase64String(certificateBase64);
            
            // Suppress warning about X509Certificate2 constructor being obsolete in .NET Core 
            #pragma warning disable SYSLIB0057
            _clientCertificate = new X509Certificate2(certificateBytes);
            #pragma warning restore SYSLIB0057

            // Export the certificate as Base64 and add it to the request headers for authentication
            var exportedCertBytes = _clientCertificate.Export(X509ContentType.Cert);
            var exportedCertBase64 = Convert.ToBase64String(exportedCertBytes);
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {exportedCertBase64}");
            
            _logger.LogInformation("SecureApi initialized successfully with enhanced connection management");

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
            // Dispose managed resources
            _client?.Dispose();
            _clientCertificate?.Dispose();
        }

        _disposed = true;
    }

    /// <summary>
    /// Finalizer to ensure resources are properly cleaned up if Dispose is not called.
    /// </summary>
    ~SecureApi() => Dispose(false);
} 