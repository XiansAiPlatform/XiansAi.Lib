using System.Net;
using System.Text.Json;
using Xians.Lib.Common.Models;
using Xians.Lib.Configuration.Models;
using Xians.Lib.Http;

namespace Xians.Lib.Common.Infrastructure;

/// <summary>
/// Service for fetching configuration settings from the application server.
/// </summary>
public static class SettingsService
{
    private const int MAX_RESPONSE_SIZE_BYTES = 1 * 1024 * 1024; // 1 MB
    private const int JSON_MAX_DEPTH = 32;
    
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
            return await client.GetAsync(WorkflowConstants.ApiEndpoints.FlowServerSettings);
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

        if (content.Length > MAX_RESPONSE_SIZE_BYTES)
        {
            throw new InvalidOperationException(
                $"Response size {content.Length} exceeds maximum allowed size of {MAX_RESPONSE_SIZE_BYTES} bytes");
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            MaxDepth = JSON_MAX_DEPTH
        };

        var settings = JsonSerializer.Deserialize<ServerSettings>(content, options);

        if (settings == null)
        {
            throw new InvalidOperationException("Failed to deserialize server settings");
        }

        if (string.IsNullOrWhiteSpace(settings.FlowServerUrl) || 
            string.IsNullOrWhiteSpace(settings.FlowServerNamespace))
        {
            throw new InvalidOperationException(
                "FlowServerUrl and FlowServerNamespace must be provided by server");
        }

        // Allow environment variable override for Temporal server URL
        // This should be used with caution in production environments
        var temporalServerUrl = Environment.GetEnvironmentVariable("TEMPORAL_SERVER_URL");
        if (!string.IsNullOrWhiteSpace(temporalServerUrl))
        {
            // Validate: accept full URLs (grpc://, https://, http://) or host:port (IP or hostname)
            var toValidate = temporalServerUrl;
            if (!toValidate.Contains("://", StringComparison.Ordinal))
            {
                toValidate = "grpc://" + toValidate; // host:port is valid for Temporal
            }
            if (!Uri.TryCreate(toValidate, UriKind.Absolute, out var uri))
            {
                throw new InvalidOperationException(
                    $"Invalid TEMPORAL_SERVER_URL environment variable: '{temporalServerUrl}' is not a valid URL or host:port");
            }

            // Additional validation: ensure it's using expected schemes (if needed)
            // For Temporal, typically we expect host:port format without scheme, or grpc/https schemes
            // Uncomment the following if you want to enforce scheme restrictions:
            // if (uri.Scheme != "grpc" && uri.Scheme != "https" && uri.Scheme != "http")
            // {
            //     throw new InvalidOperationException(
            //         $"Invalid TEMPORAL_SERVER_URL scheme: '{uri.Scheme}'. Expected grpc, https, or http");
            // }

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
