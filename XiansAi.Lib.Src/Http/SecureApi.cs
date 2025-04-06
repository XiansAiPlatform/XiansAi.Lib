using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace XiansAi.Http;

public interface ISecureApiClient
{
    HttpClient Client { get; }
    bool IsReady { get; }
}

public class SecureApi : ISecureApiClient, IDisposable
{
    private readonly HttpClient _client;
    private readonly X509Certificate2 _clientCertificate;
    private readonly ILogger<SecureApi> _logger;
    private bool _disposed;
    
    private static Lazy<SecureApi> _instance = new Lazy<SecureApi>(
        () => throw new InvalidOperationException("SecureApi must be initialized before use"));

    public HttpClient Client => _client;
    public static ISecureApiClient Instance => _instance.Value;

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
            var certificateBytes = Convert.FromBase64String(certificateBase64);
            #pragma warning disable SYSLIB0057
            _clientCertificate = new X509Certificate2(certificateBytes);
            #pragma warning restore SYSLIB0057

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

    public bool IsReady => _client != null;

    public static HttpClient InitializeClient(string certificateBase64, string serverUrl)
    {
        var logger = Globals.LogFactory.CreateLogger<SecureApi>();
        _instance = new Lazy<SecureApi>(() => new SecureApi(certificateBase64, serverUrl, logger));
        return _instance.Value.Client;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _client?.Dispose();
            _clientCertificate?.Dispose();
        }

        _disposed = true;
    }

    ~SecureApi() => Dispose(false);
} 