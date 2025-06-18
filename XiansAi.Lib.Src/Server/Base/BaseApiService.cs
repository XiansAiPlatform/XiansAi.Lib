using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace XiansAi.Server.Base;

/// <summary>
/// Base class for all API services providing common HTTP functionality
/// </summary>
public abstract class BaseApiService
{
    protected readonly HttpClient HttpClient;
    protected readonly ILogger Logger;
    
    protected BaseApiService(HttpClient httpClient, ILogger logger)
    {
        HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Makes a GET request and deserializes the response
    /// </summary>
    /// <typeparam name="T">Type to deserialize to</typeparam>
    /// <param name="endpoint">API endpoint</param>
    /// <returns>Deserialized response</returns>
    protected async Task<T> GetAsync<T>(string endpoint)
    {
        try
        {
            Logger.LogDebug("Making GET request to {Endpoint}", endpoint);
            var response = await HttpClient.GetAsync(endpoint);
            
            if (response.StatusCode != HttpStatusCode.OK)
            {
                Logger.LogError("API request failed with status {StatusCode} for endpoint {Endpoint}", 
                    response.StatusCode, endpoint);
                throw new HttpRequestException($"Failed to get data from {endpoint}. Status code: {response.StatusCode}");
            }
            
            var content = await response.Content.ReadAsStringAsync();
            
            var options = new JsonSerializerOptions
            { 
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var result = JsonSerializer.Deserialize<T>(content, options);
            if (result == null)
            {
                Logger.LogError("Failed to deserialize response from {Endpoint}: {Content}", endpoint, content);
                throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
            }
            
            Logger.LogDebug("Successfully retrieved and deserialized data from {Endpoint}", endpoint);
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "API call failed: GET {Endpoint}", endpoint);
            throw;
        }
    }
    
    /// <summary>
    /// Makes a POST request with JSON payload and deserializes the response
    /// </summary>
    /// <typeparam name="T">Type to deserialize to</typeparam>
    /// <param name="endpoint">API endpoint</param>
    /// <param name="data">Data to serialize and send</param>
    /// <returns>Deserialized response</returns>
    protected async Task<T> PostAsync<T>(string endpoint, object data)
    {
        try
        {
            Logger.LogDebug("Making POST request to {Endpoint}", endpoint);
            var response = await HttpClient.PostAsJsonAsync(endpoint, data);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            
            var options = new JsonSerializerOptions
            { 
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var result = JsonSerializer.Deserialize<T>(content, options);
            if (result == null)
            {
                Logger.LogError("Failed to deserialize response from {Endpoint}: {Content}", endpoint, content);
                throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
            }
            
            Logger.LogDebug("Successfully posted data and received response from {Endpoint}", endpoint);
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "API call failed: POST {Endpoint}", endpoint);
            throw;
        }
    }
    
    /// <summary>
    /// Makes a POST request with JSON payload and returns the response as string
    /// </summary>
    /// <param name="endpoint">API endpoint</param>
    /// <param name="data">Data to serialize and send</param>
    /// <returns>Response content as string</returns>
    protected async Task<string> PostAsync(string endpoint, object data)
    {
        try
        {
            Logger.LogDebug("Making POST request to {Endpoint}", endpoint);
            var response = await HttpClient.PostAsJsonAsync(endpoint, data);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            Logger.LogDebug("Successfully posted data to {Endpoint}", endpoint);
            return content;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "API call failed: POST {Endpoint}", endpoint);
            throw;
        }
    }
    
    /// <summary>
    /// Makes a POST request with JSON payload and returns the raw HttpResponseMessage
    /// This allows derived classes to handle specific status codes before calling EnsureSuccessStatusCode
    /// </summary>
    /// <param name="endpoint">API endpoint</param>
    /// <param name="data">Data to serialize and send</param>
    /// <returns>Raw HttpResponseMessage</returns>
    protected async Task<HttpResponseMessage> PostRawAsync(string endpoint, object data)
    {
        try
        {
            Logger.LogDebug("Making POST request to {Endpoint}", endpoint);
            var response = await HttpClient.PostAsJsonAsync(endpoint, data);
            return response;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "API call failed: POST {Endpoint}", endpoint);
            throw;
        }
    }
} 