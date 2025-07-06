using Microsoft.Extensions.Logging;
using Server;
using Temporalio.Client;

namespace Temporal;

public class TemporalClientService 
{
    private static readonly ILogger _logger = Globals.LogFactory.CreateLogger<TemporalClientService>();
    private static readonly Lazy<TemporalClientService> _instance = new(() => new TemporalClientService());
    private ITemporalClient? _client;
    private readonly object _lock = new();
    private bool _isInitialized;

    private TemporalClientService() {}

    public static TemporalClientService Instance => _instance.Value;

    public ITemporalClient GetClientAsync()
    {
        if (_isInitialized && _client != null) return _client;

        lock (_lock)
        {
            if (_isInitialized && _client != null) return _client;
            
            _client = CreateClientAsync().GetAwaiter().GetResult();
            _isInitialized = true;
            return _client;
        }
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

        _logger.LogInformation($"Connecting to flow server at {settings.FlowServerUrl} with namespace {settings.FlowServerNamespace}");

        return await TemporalClient.ConnectAsync(options);
    }

    private static TlsOptions? getTlsConfig(FlowServerSettings settings)
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
}