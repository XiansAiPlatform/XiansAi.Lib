namespace XiansAi.Server.Base;

/// <summary>
/// Interface for API services providing common HTTP functionality
/// </summary>
public interface IApiService
{
    /// <summary>
    /// Makes a GET request and deserializes the response
    /// </summary>
    /// <typeparam name="T">Type to deserialize to</typeparam>
    /// <param name="endpoint">API endpoint</param>
    /// <returns>Deserialized response</returns>
    Task<T> GetAsync<T>(string endpoint);
    
    /// <summary>
    /// Makes a POST request with JSON payload and deserializes the response
    /// </summary>
    /// <typeparam name="T">Type to deserialize to</typeparam>
    /// <param name="endpoint">API endpoint</param>
    /// <param name="data">Data to serialize and send</param>
    /// <returns>Deserialized response</returns>
    Task<T> PostAsync<T>(string endpoint, object data);
    
    /// <summary>
    /// Makes a POST request with JSON payload and returns the response as string
    /// </summary>
    /// <param name="endpoint">API endpoint</param>
    /// <param name="data">Data to serialize and send</param>
    /// <returns>Response content as string</returns>
    Task<string> PostAsync(string endpoint, object data);
    
    /// <summary>
    /// Makes a POST request with JSON payload and returns the raw HttpResponseMessage
    /// </summary>
    /// <param name="endpoint">API endpoint</param>
    /// <param name="data">Data to serialize and send</param>
    /// <returns>Raw HttpResponseMessage</returns>
    Task<HttpResponseMessage> PostRawAsync(string endpoint, object data);
} 