using Microsoft.Extensions.Logging;
using Temporalio.Client;
using XiansAi.Flow;

namespace XiansAi.Temporal;

public interface ITemporalClientService
{
    Task<ITemporalClient> GetClientAsync();

}

public class TemporalClientService : ITemporalClientService
{
    private ITemporalClient? _client;


    public TemporalClientService()
    {
    }


     public async Task<ITemporalClient> GetClientAsyncLocal()
    {
        if (_client != null) return _client;



        var options = new TemporalClientConnectOptions(new("localhost:7233")) // Local Temporal URL
                {
                    Namespace = "default", // Default local namespace
                };

                _client =  await TemporalClient.ConnectAsync(options);

        return _client;
    }

    public async Task<ITemporalClient> GetClientAsync()
    {
        if (_client != null) return _client;

        _client = await TemporalClient.ConnectAsync(new(PlatformConfig.FLOW_SERVER_URL ?? throw new InvalidOperationException("FLOW_SERVER_URL is not set"))
        {
            Namespace = PlatformConfig.FLOW_SERVER_NAMESPACE ?? throw new InvalidOperationException("FLOW_SERVER_NAMESPACE is not set"),
            Tls = await getTlsConfig(), 
            LoggerFactory = LoggerFactory.Create(builder =>
                builder.
                    AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
                    SetMinimumLevel(LogLevel.Information)),
        });

        return _client;
    }

    private async Task<TlsOptions> getTlsConfig()
    {
        if (String.IsNullOrEmpty(PlatformConfig.FLOW_SERVER_API_KEY)) {
        return new()
            {
                ClientCert = await File.ReadAllBytesAsync(PlatformConfig.FLOW_SERVER_CERT_PATH ?? throw new InvalidOperationException("FLOW_SERVER_CERT_PATH is not set")),
                ClientPrivateKey = await File.ReadAllBytesAsync(PlatformConfig.FLOW_SERVER_PRIVATE_KEY_PATH ?? throw new InvalidOperationException("FLOW_SERVER_PRIVATE_KEY_PATH is not set")),
            };
        }
        else {
            var apiKey = PlatformConfig.FLOW_SERVER_API_KEY;
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
}