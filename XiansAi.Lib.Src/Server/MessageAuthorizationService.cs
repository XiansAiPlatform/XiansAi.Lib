using System.Net;
using Microsoft.Extensions.Logging;

namespace Server;

public class MessageAuthorizationService
{
    private readonly ILogger<MessageAuthorizationService> _logger;
    private const string AUTHORIZATION_URL = "api/agent/conversation/authorization/{authorization}";

    public MessageAuthorizationService()
    {
        _logger = Globals.LogFactory.CreateLogger<MessageAuthorizationService>();
    }

    public async Task<string?> GetAuthorization(string? authorization)
    {
        try
        {
            if (!SecureApi.IsReady)
            {
                _logger.LogError("Secure API is not ready");
                return null;
            }
            var client = SecureApi.Instance.Client;
            var url = AUTHORIZATION_URL.Replace("{authorization}", authorization ?? "");
            var response = await client.GetAsync(url);
         
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get authorization. Status code: {StatusCode}", response.StatusCode);
                return null;
            }

            var token = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Empty response received from authorization endpoint");
                return null;
            }

            return token.Trim('"');
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving authorization for GUID: {Authorization}", authorization);
            return null;
        }
    }
}