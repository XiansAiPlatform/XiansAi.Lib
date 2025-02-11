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


     public async Task<ITemporalClient> GetClientAsync()
    {
        if (_client != null) return _client;



        var options = new TemporalClientConnectOptions(new("localhost:7233")) // Local Temporal URL
                {
                    Namespace = "default", // Default local namespace
                };

                _client =  await TemporalClient.ConnectAsync(options);

        return _client;
    }

    public async Task<ITemporalClient> GetClientGlobalAsync()
    {
        if (_client != null) return _client;

        _client = await TemporalClient.ConnectAsync(new(PlatformConfig.FLOW_SERVER_URL ?? throw new InvalidOperationException("FLOW_SERVER_URL is not set"))
        {
            Namespace = PlatformConfig.FLOW_SERVER_NAMESPACE ?? throw new InvalidOperationException("FLOW_SERVER_NAMESPACE is not set"),
            Tls = new()
            {
                ClientCert = await File.ReadAllBytesAsync(PlatformConfig.FLOW_SERVER_CERT_PATH ?? throw new InvalidOperationException("FLOW_SERVER_CERT_PATH is not set")),
                ClientPrivateKey = await File.ReadAllBytesAsync(PlatformConfig.FLOW_SERVER_PRIVATE_KEY_PATH ?? throw new InvalidOperationException("FLOW_SERVER_PRIVATE_KEY_PATH is not set")   ),
            }, 
            LoggerFactory = LoggerFactory.Create(builder =>
                builder.
                    AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
                    SetMinimumLevel(LogLevel.Information)),
        });

        return _client;
    }
}