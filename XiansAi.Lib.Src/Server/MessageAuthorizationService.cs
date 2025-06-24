using System.Net;
using Microsoft.Extensions.Logging;
using XiansAi.Server.Base;

namespace Server;

public class MessageAuthorizationService
{
    private readonly ILogger<MessageAuthorizationService> _logger;
    private readonly IApiService _apiService;
    private const string AUTHORIZATION_URL = "api/agent/conversation/authorization/{authorization}";

    /// <summary>
    /// Constructor for dependency injection with IApiService
    /// </summary>
    public MessageAuthorizationService(IApiService apiService, ILogger<MessageAuthorizationService> logger)
    {
        _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Legacy constructor for backward compatibility - creates instance without DI
    /// </summary>
    public MessageAuthorizationService()
    {
        // Create a BaseApiService instance for legacy support
        var httpClient = GetLegacyHttpClient();
        _apiService = new LegacyApiServiceWrapper(httpClient, Globals.LogFactory.CreateLogger<LegacyApiServiceWrapper>());
        _logger = Globals.LogFactory.CreateLogger<MessageAuthorizationService>();
    }

    /// <summary>
    /// Gets HttpClient for legacy constructor - fallback to SecureApi
    /// </summary>
    private static HttpClient GetLegacyHttpClient()
    {
        if (!SecureApi.IsReady)
        {
            throw new InvalidOperationException("SecureApi is not ready. Please ensure it is properly initialized or use dependency injection.");
        }
        return SecureApi.Instance.Client;
    }

    /// <summary>
    /// Legacy wrapper that implements IApiService using BaseApiService for backward compatibility
    /// </summary>
    private class LegacyApiServiceWrapper : BaseApiService
    {
        public LegacyApiServiceWrapper(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
        }
    }

    public async Task<string?> GetAuthorization(string? authorization)
    {
        try
        {
            var url = AUTHORIZATION_URL.Replace("{authorization}", authorization ?? "");
            var token = await _apiService.GetAsync<string>(url);
            
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogError("Empty response received from authorization endpoint");
                return null;
            }

            // Trim both single and double quotes
            return token.Trim('"', '\'');
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving authorization for GUID: {Authorization}", authorization);
            return null;
        }
    }
}