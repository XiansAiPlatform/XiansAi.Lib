using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

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
}

/// <summary>
/// Implements a secure API client that uses certificate-based authentication.
/// This class is implemented as a singleton to ensure a single HTTP client is used throughout the application.
/// </summary>
public class SecureApi : ISecureApiClient, IDisposable
{
    private readonly HttpClient _client;
    private readonly X509Certificate2 _clientCertificate;
    private readonly ILogger<SecureApi> _logger;
    private bool _disposed;
    
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
        
        string apiVersion = VersionInfo.MajorApiVersion;
        string baseEndpoint = $"/api/{apiVersion}/agent/";
        string serverUrlWithEndpoint = $"{serverUrl}{baseEndpoint}";

        if (string.IsNullOrEmpty(certificateBase64))
            throw new ArgumentNullException(nameof(certificateBase64));
            
        if (string.IsNullOrEmpty(serverUrl))
            throw new ArgumentNullException(nameof(serverUrl));

        _client = new HttpClient { 
            BaseAddress = new Uri(serverUrlWithEndpoint),
            Timeout = TimeSpan.FromSeconds(30) // Increase timeout to 30 seconds
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
            
            _logger.LogInformation("SecureApi initialized successfully");
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
    /// <param name="certificateBase64">The client certificate in Base64 encoded format.</param>
    /// <param name="serverUrl">The base URL of the server API.</param>
    /// <param name="forceReinitialize">Whether to force reinitialization even if already initialized.</param>
    /// <returns>The configured HTTP client instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if already initialized with different parameters.</exception>
    public static HttpClient InitializeClient(string certificateBase64, string serverUrl, bool forceReinitialize = false)
    {
        // Validate parameters first
        if (string.IsNullOrEmpty(certificateBase64))
            throw new ArgumentNullException(nameof(certificateBase64));
            
        if (string.IsNullOrEmpty(serverUrl))
            throw new ArgumentNullException(nameof(serverUrl));

        lock (_lock)
        {
            if (_instance != null && !forceReinitialize)
            {
                // Check if the existing instance has the same configuration
                if (_currentServerUrl == serverUrl && _currentCertificate == certificateBase64)
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
            _instance = new SecureApi(certificateBase64, serverUrl, logger);
            _currentServerUrl = serverUrl;
            _currentCertificate = certificateBase64;
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