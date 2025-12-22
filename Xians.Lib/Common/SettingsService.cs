using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using Xians.Lib.Configuration;
using Xians.Lib.Http;

namespace Xians.Lib.Common;

/// <summary>
/// Server-provided settings for Temporal/Flow server configuration.
/// </summary>
public class ServerSettings
{
    public required string FlowServerUrl { get; set; }
    public required string FlowServerNamespace { get; set; }
    public string? FlowServerCertBase64 { get; set; }
    public string? FlowServerPrivateKeyBase64 { get; set; }
}

/// <summary>
/// Service for fetching configuration settings from the application server.
/// </summary>
public static class SettingsService
{
    private const string SETTINGS_ENDPOINT = "/api/agent/settings/flowserver";
    private static readonly object _settingsLock = new object();
    private static ServerSettings? _manualSettings;
    private static ServerSettings? _cachedSettings;

    /// <summary>
    /// Gets the Temporal/Flow server settings from the application server.
    /// Settings are cached after the first successful fetch.
    /// </summary>
    /// <param name="httpService">The HTTP client service to use for fetching settings.</param>
    /// <returns>The server settings containing Temporal configuration.</returns>
    public static async Task<ServerSettings> GetSettingsAsync(IHttpClientService httpService)
    {
        lock (_settingsLock)
        {
            if (_manualSettings != null)
            {
                return _manualSettings;
            }

            if (_cachedSettings != null)
            {
                return _cachedSettings;
            }
        }

        var settings = await LoadSettingsFromServer(httpService);

        lock (_settingsLock)
        {
            _cachedSettings = settings;
        }

        return settings;
    }

    /// <summary>
    /// Sets manual server settings (primarily for testing).
    /// </summary>
    /// <param name="settings">The settings to use instead of fetching from server.</param>
    public static void SetManualSettings(ServerSettings? settings)
    {
        lock (_settingsLock)
        {
            _manualSettings = settings;
        }
    }

    /// <summary>
    /// Resets the cached settings (primarily for testing).
    /// </summary>
    public static void ResetCache()
    {
        lock (_settingsLock)
        {
            _manualSettings = null;
            _cachedSettings = null;
        }
    }

    /// <summary>
    /// Internal method that loads settings from the server using the provided HTTP service.
    /// </summary>
    private static async Task<ServerSettings> LoadSettingsFromServer(IHttpClientService httpService)
    {
        var response = await httpService.ExecuteWithRetryAsync(async () =>
        {
            var client = await httpService.GetHealthyClientAsync();
            return await client.GetAsync(SETTINGS_ENDPOINT);
        });

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException(
                $"Failed to fetch settings from server. Status: {response.StatusCode}");
        }

        return await ParseSettingsResponse(response);
    }

    /// <summary>
    /// Parses the server response into a ServerSettings object.
    /// </summary>
    private static async Task<ServerSettings> ParseSettingsResponse(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();

        const int MaxResponseSize = 1 * 1024 * 1024; // 1 MB
        if (content.Length > MaxResponseSize)
        {
            throw new InvalidOperationException(
                $"Response size {content.Length} exceeds maximum allowed size of {MaxResponseSize} bytes");
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            MaxDepth = 32
        };

        var settings = JsonSerializer.Deserialize<ServerSettings>(content, options);

        if (settings == null)
        {
            throw new InvalidOperationException("Failed to deserialize server settings");
        }

        if (string.IsNullOrEmpty(settings.FlowServerUrl) || 
            string.IsNullOrEmpty(settings.FlowServerNamespace))
        {
            throw new InvalidOperationException(
                "FlowServerUrl and FlowServerNamespace must be provided by server");
        }

        // Allow environment variable override for Temporal server URL
        var temporalServerUrl = Environment.GetEnvironmentVariable("TEMPORAL_SERVER_URL");
        if (!string.IsNullOrWhiteSpace(temporalServerUrl))
        {
            settings.FlowServerUrl = temporalServerUrl;
        }

        return settings;
    }

    /// <summary>
    /// Creates a TemporalConfiguration from server settings.
    /// </summary>
    /// <param name="serverSettings">The server settings to convert.</param>
    /// <returns>A TemporalConfiguration instance.</returns>
    public static TemporalConfiguration ToTemporalConfiguration(this ServerSettings serverSettings)
    {
        return new TemporalConfiguration
        {
            ServerUrl = serverSettings.FlowServerUrl,
            Namespace = serverSettings.FlowServerNamespace,
            CertificateBase64 = serverSettings.FlowServerCertBase64,
            PrivateKeyBase64 = serverSettings.FlowServerPrivateKeyBase64
        };
    }
}


