using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Xians.Lib.Common;
using Xians.Lib.Configuration.Models;
using Xians.Lib.Common.Exceptions;
using Xians.Lib.Common.Security;

namespace Xians.Lib.Http;

/// <summary>
/// Creates and configures HTTP clients with authentication.
/// </summary>
internal class HttpClientFactory
{
    private readonly ServerConfiguration _config;
    private readonly ILogger? _logger;

    public HttpClientFactory(ServerConfiguration config, ILogger? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
    }

    /// <summary>
    /// Creates a new configured HTTP client with certificate-based authentication.
    /// </summary>
    public (HttpClient Client, X509Certificate2 Certificate) CreateClient()
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
                    
                    _logger?.LogWarning("SSL certificate validation failed: {Errors}", errors);
                    return false;
                }
            }
        };

        var client = new HttpClient(socketsHandler)
        {
            BaseAddress = new Uri(_config.ServerUrl),
            Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds)
        };

        X509Certificate2 certificate;
        try
        {
            // Parse API key as Base64-encoded certificate
            var apiKeyBytes = Convert.FromBase64String(_config.ApiKey);
            
            #pragma warning disable SYSLIB0057
            certificate = new X509Certificate2(apiKeyBytes);
            #pragma warning restore SYSLIB0057

            // Validate certificate expiration
            ValidateCertificateExpiration(certificate);

            // Export the certificate as Base64 and add to request headers
            var exportedCertBytes = certificate.Export(X509ContentType.Cert);
            var exportedCertBase64 = Convert.ToBase64String(exportedCertBytes);
            client.DefaultRequestHeaders.Add(WorkflowConstants.Headers.Authorization, $"Bearer {exportedCertBase64}");
            
            _logger?.LogTrace("Client certificate configured for authentication");
        }
        catch (Exception ex)
        {
            client?.Dispose();
            _logger?.LogError(ex, "Failed to configure HTTP client authentication");
            throw new CertificateException(
                "Failed to configure HTTP client authentication. API_KEY must be a Base64-encoded X.509 certificate.", 
                ex);
        }

        return (client, certificate);
    }

    private void ValidateCertificateExpiration(X509Certificate2 certificate)
    {
        CertificateValidationHelper.ValidateExpiration(certificate, _logger);
    }
}

