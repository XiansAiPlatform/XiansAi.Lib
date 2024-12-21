using Temporalio.Client;

public class TemporalClientService
{
    private ITemporalClient? _client;
    private readonly TemporalConfig _config;

    public TemporalClientService(TemporalConfig config)
    {
        _config = config;
    }


    public async Task<ITemporalClient> GetClientAsync()
    {
        if (_client != null) return _client;

        _client = await TemporalClient.ConnectAsync(new(_config.TemporalServerUrl)
        {
            Namespace = _config.Namespace,
            Tls = new()
            {
                ClientCert = await File.ReadAllBytesAsync(_config.ClientCert),
                ClientPrivateKey = await File.ReadAllBytesAsync(_config.ClientPrivateKey),
            }
        });

        return _client;
    }
}