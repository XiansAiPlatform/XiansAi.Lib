using Microsoft.Extensions.Logging;
using Temporalio.Client;
using XiansAi;

namespace Temporal;

public interface ITemporalClientService
{
    Task<ITemporalClient> GetClientAsync();
}

public class TemporalClientService : ITemporalClientService
{
    private ITemporalClient? _client;

    public async Task<ITemporalClient> GetClientAsync()
    {
        if (_client != null) return _client;

        _client = await TemporalClient.ConnectAsync(new(PlatformConfig.FLOW_SERVER_URL ?? throw new InvalidOperationException("FLOW_SERVER_URL is not set"))
        {
            Namespace = PlatformConfig.FLOW_SERVER_NAMESPACE ?? throw new InvalidOperationException("FLOW_SERVER_NAMESPACE is not set"),
            Tls = getTlsConfig(), 
            LoggerFactory = LoggerFactory.Create(builder =>
                builder.
                    AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
                    SetMinimumLevel(LogLevel.Information)),
        });

        return _client;
    }

    private TlsOptions getTlsConfig()
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