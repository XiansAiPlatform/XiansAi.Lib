using Temporalio.Client;
using XiansAi.Flow;

namespace XiansAi.Temporal;

public interface ITemporalClientService
{
    Task<ITemporalClient> GetClientAsync();

    Config Config { get; }
}

public class TemporalClientService : ITemporalClientService
{
    private ITemporalClient? _client;

    public Config Config { get; set; }

    public TemporalClientService(Config config)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
    }


    public async Task<ITemporalClient> GetClientAsync()
    {
        if (_client != null) return _client;

        _client = await TemporalClient.ConnectAsync(new(Config.FlowServerUrl)
        {
            Namespace = Config.FlowServerNamespace,
            Tls = new()
            {
                ClientCert = await File.ReadAllBytesAsync(Config.FlowServerCertPath),
                ClientPrivateKey = await File.ReadAllBytesAsync(Config.FlowServerPrivateKeyPath),
            }
        });

        return _client;
    }
}