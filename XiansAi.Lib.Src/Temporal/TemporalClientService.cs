using Microsoft.Extensions.Logging;
using Temporalio.Client;
using XiansAi;

namespace Temporal;

public class TemporalClientService 
{
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
        return await TemporalClient.ConnectAsync(new(PlatformConfig.FLOW_SERVER_URL ?? throw new InvalidOperationException("FLOW_SERVER_URL is not set"))
        {
            Namespace = PlatformConfig.FLOW_SERVER_NAMESPACE ?? throw new InvalidOperationException("FLOW_SERVER_NAMESPACE is not set"),
            Tls = getTlsConfig(), 
            LoggerFactory = LoggerFactory.Create(builder =>
                builder.
                    AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
                    SetMinimumLevel(LogLevel.Information)),
        });
    }

    private static TlsOptions getTlsConfig()
    {
        var apiKey = PlatformConfig.FLOW_SERVER_API_KEY ?? throw new InvalidOperationException("FLOW_SERVER_API_KEY is not set");
        var certBase64 = apiKey.Split(':')[0];
        var cert = Convert.FromBase64String(certBase64);
        var privateKeyBase64 = apiKey.Split(':')[1];
        var privateKey = Convert.FromBase64String(privateKeyBase64);
        return new()
        {
            ClientCert = cert,
            ClientPrivateKey = privateKey,
        };
    }
}