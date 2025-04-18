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
    
    /// <summary>
    /// Indicates whether the secure client is properly initialized and ready to use.
    /// </summary>
    bool IsReady { get; }
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
    private static Lazy<SecureApi> _instance = new Lazy<SecureApi>(
        () => throw new InvalidOperationException("SecureApi must be initialized before use"));

    /// <summary>
    /// Gets the configured HTTP client for making secure API requests.
    /// </summary>
    public HttpClient Client => _client;
    
    /// <summary>
    /// Gets the singleton instance of the SecureApi client.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if accessed before initialization.</exception>
    public static ISecureApiClient Instance => _instance.Value;

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

        _client = new HttpClient { BaseAddress = new Uri(serverUrl) };

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
    /// Indicates whether the client is properly initialized and ready to use.
    /// </summary>
    public bool IsReady => _client != null;

    /// <summary>
    /// Initializes the singleton instance of the SecureApi client.
    /// This method must be called before using the Instance property.
    /// </summary>
    /// <param name="certificateBase64">The client certificate in Base64 encoded format.</param>
    /// <param name="serverUrl">The base URL of the server API.</param>
    /// <returns>The configured HTTP client instance.</returns>
    public static HttpClient InitializeClient(string certificateBase64, string serverUrl)
    {
        var logger = Globals.LogFactory.CreateLogger<SecureApi>();
        _instance = new Lazy<SecureApi>(() => new SecureApi(certificateBase64, serverUrl, logger));
        return _instance.Value.Client;
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