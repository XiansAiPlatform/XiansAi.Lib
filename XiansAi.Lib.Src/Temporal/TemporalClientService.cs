using Microsoft.Extensions.Logging;
using Server;
using XiansAi.Server.Interfaces;
using XiansAi.Server.Extensions;
using Temporalio.Client;

namespace Temporal;

public class TemporalClientService 
{
    private readonly ISettingsService _settingsService;
    private ITemporalClient? _client;
    private readonly object _lock = new();
    private bool _isInitialized;

    // Backward compatibility - static instance using obsolete factory
    private static readonly Lazy<TemporalClientService> _staticInstance = new(() => 
    {
        #pragma warning disable CS0618 // Type or member is obsolete
        var settingsService = XiansAiServiceFactory.GetSettingsService();
        #pragma warning restore CS0618 // Type or member is obsolete
        return new TemporalClientService(settingsService);
    });

    /// <summary>
    /// Static instance for backward compatibility - will be deprecated
    /// </summary>
    [Obsolete("Use dependency injection instead. This static instance will be removed in a future version.")]
    public static TemporalClientService Instance => _staticInstance.Value;

    public TemporalClientService(ISettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

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

    private async Task<ITemporalClient> CreateClientAsync()
    {
        var settings = await _settingsService.GetFlowServerSettingsAsync();

        return await TemporalClient.ConnectAsync(new(settings.FlowServerUrl)
        {
            Namespace = settings.FlowServerNamespace,
            Tls = getTlsConfig(settings), 
            LoggerFactory = LoggerFactory.Create(builder =>
                builder.
                    AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
                    SetMinimumLevel(LogLevel.Information)),
        });
    }

    private static TlsOptions getTlsConfig(FlowServerSettings settings)
    {
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