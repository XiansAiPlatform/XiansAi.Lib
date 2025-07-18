using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace Server;


public class FlowServerSettings
{
    public required string FlowServerUrl { get; set; }
    public required string FlowServerNamespace { get; set; }
    public string? FlowServerCertBase64 { get; set; }
    public string? FlowServerPrivateKeyBase64 { get; set; }
    public required string OpenAIApiKey { get; set; }
    public required string ModelName { get; set; }
}

public static class SettingsService
{
    private static readonly ILogger _logger = Globals.LogFactory.CreateLogger<FlowServerSettings>();
    private const string SETTINGS_URL = "api/agent/settings/flowserver";

    private static readonly Lazy<Task<FlowServerSettings>> _settingsLazy = new(() => LoadSettingsFromServer());


    /// <summary>
    /// Loads settings from the server. Caches the settings after the first fetch.
    /// </summary>
    /// <returns>The flow server settings</returns>
    public static async Task<FlowServerSettings> GetSettingsFromServer()
    {
        return await _settingsLazy.Value;
    }

    /// <summary>
    /// Internal method that actually loads settings from the server
    /// </summary>
    private static async Task<FlowServerSettings> LoadSettingsFromServer()
    {
        if (!SecureApi.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, cannot load settings from server");
            throw new Exception("App server secure API is not ready, cannot load settings from server");
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var httpResult = await client.GetAsync(SETTINGS_URL);

            
            if (httpResult.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"Failed to get settings from server. Status code: {httpResult.StatusCode}");
            }

            return await ParseSettingsResponse(httpResult);
        }
        catch (Exception e)
        {
            _logger.LogError($"Failed to load settings from server {e.Message}");
            throw new Exception($"Failed to load settings from server {e.Message}", e);
        }
    }


    /// <summary>
    /// Parses the server response into a Knowledge object
    /// </summary>
    private static async Task<FlowServerSettings> ParseSettingsResponse(HttpResponseMessage httpResult)
    {
        var response = await httpResult.Content.ReadAsStringAsync();

        try
        {
            var options = new JsonSerializerOptions
            { 
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var settings = JsonSerializer.Deserialize<FlowServerSettings>(response, options);

            if (settings == null)
            {
                _logger.LogError($"Failed to deserialize settings from server: {response}");
                throw new Exception($"Failed to deserialize settings from server: {response}");
            }

            if (settings.FlowServerUrl == null || settings.FlowServerNamespace == null)
            {
                _logger.LogError("Flow server URL or namespace is not set, cannot connect to flow server");
                throw new Exception("Flow server URL or namespace is not set, cannot connect to flow server");
            }

            if (settings.FlowServerCertBase64 == null || settings.FlowServerPrivateKeyBase64 == null)
            {
                _logger.LogWarning("Flow server cert or private key is not set, using default TLS config");
            }

            _logger.LogInformation($"Settings loaded from server: {settings.FlowServerUrl}");
            
            return settings;
        } 
        catch (Exception e)
        {
            _logger.LogError($"Failed to deserialize settings from server: {response} {e.Message}", e);
            throw new Exception($"Failed to deserialize settings from server: {response} {e.Message}", e);
        }
    }
}
