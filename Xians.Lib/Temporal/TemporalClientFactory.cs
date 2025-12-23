using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Xians.Lib.Configuration.Models;
using Xians.Lib.Common.Exceptions;

namespace Xians.Lib.Temporal;

/// <summary>
/// Creates and configures Temporal clients with TLS support.
/// </summary>
internal class TemporalClientFactory
{
    private readonly TemporalConfiguration _config;
    private readonly ILogger? _logger;

    public TemporalClientFactory(TemporalConfiguration config, ILogger? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
    }

    /// <summary>
    /// Creates a new Temporal client connection.
    /// </summary>
    public async Task<ITemporalClient> CreateClientAsync()
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

        _logger?.LogDebug(
            "Connecting to Temporal server at {ServerUrl} with namespace {Namespace}", 
            _config.ServerUrl, _config.Namespace);

        return await TemporalClient.ConnectAsync(options);
    }

    private TlsOptions? GetTlsConfig()
    {
        if (!_config.IsTlsEnabled)
        {
            _logger?.LogDebug("TLS is not enabled for Temporal connection");
            return null;
        }

        byte[]? cert = null;
        byte[]? privateKey = null;
        
        try
        {
            cert = Convert.FromBase64String(_config.CertificateBase64!);
            privateKey = Convert.FromBase64String(_config.PrivateKeyBase64!);
            
            _logger?.LogDebug("TLS is enabled for Temporal connection");
            
            return new TlsOptions
            {
                ClientCert = cert,
                ClientPrivateKey = privateKey
            };
        }
        catch (Exception ex)
        {
            // On error, securely clear sensitive data
            if (privateKey != null)
            {
                Array.Clear(privateKey, 0, privateKey.Length);
            }
            if (cert != null)
            {
                Array.Clear(cert, 0, cert.Length);
            }
            
            _logger?.LogError(ex, "Failed to parse TLS certificate or private key");
            throw new TemporalConnectionException(
                "Failed to configure TLS for Temporal connection", 
                _config.ServerUrl, 
                _config.Namespace, 
                ex);
        }
    }
}

