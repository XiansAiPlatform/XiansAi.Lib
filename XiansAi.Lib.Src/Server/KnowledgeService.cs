using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using XiansAi.Models;
using XiansAi.Server.Base;

namespace Server;

public class KnowledgeService
{
    private readonly IApiService _apiService;
    private readonly ILogger<KnowledgeService> _logger;
    private const string KNOWLEDGE_URL = "api/agent/knowledge/latest?name={name}&agent={agent}";
    private const string UPLOAD_KNOWLEDGE_URL = "api/agent/knowledge";

    /// <summary>
    /// Constructor for dependency injection with IApiService
    /// </summary>
    public KnowledgeService(IApiService apiService, ILogger<KnowledgeService> logger)
    {
        _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Legacy constructor for backward compatibility - creates instance without DI
    /// </summary>
    public KnowledgeService()
    {
        // Create a BaseApiService instance for legacy support
        var httpClient = GetLegacyHttpClient();
        _apiService = new LegacyApiServiceWrapper(httpClient, Globals.LogFactory.CreateLogger<LegacyApiServiceWrapper>());
        _logger = Globals.LogFactory.CreateLogger<KnowledgeService>();
    }

    private static HttpClient GetLegacyHttpClient()
    {
        if (!SecureApi.IsReady)
        {
            throw new InvalidOperationException("SecureApi is not ready. Initialize SecureApi before using KnowledgeService or use dependency injection.");
        }
        return SecureApi.Instance.Client;
    }

    /// <summary>
    /// Loads knowledge from the server by name
    /// </summary>
    /// <param name="knowledgeName">The name of the knowledge to load</param>
    /// <param name="agent">The agent name</param>
    /// <returns>The loaded knowledge, or null if not found</returns>
    public async Task<Knowledge?> GetKnowledgeFromServer(string knowledgeName, string agent)
    {
        var url = BuildKnowledgeUrl(knowledgeName, agent);
        _logger.LogInformation("Loading knowledge from server: {Url}", url);
        
        try
        {
            return await _apiService.GetAsync<Knowledge>(url);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404") || ex.Message.Contains("NotFound"))
        {
            _logger.LogInformation("Knowledge not found on server: {KnowledgeName}", knowledgeName);
            return null;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to load knowledge from server: {KnowledgeName}", knowledgeName);
            return null;
        }
    }

    /// <summary>
    /// Alternative method using raw HTTP response for more control over status codes
    /// </summary>
    /// <param name="knowledgeName">The name of the knowledge to load</param>
    /// <param name="agent">The agent name</param>
    /// <returns>The loaded knowledge, or null if not found</returns>
    public async Task<Knowledge?> GetKnowledgeFromServerWithRawResponse(string knowledgeName, string agent)
    {
        var url = BuildKnowledgeUrl(knowledgeName, agent);
        _logger.LogInformation("Loading knowledge from server: {Url}", url);
        
        try
        {
            // For legacy compatibility, we can use the HttpClient directly when needed
            if (_apiService is LegacyApiServiceWrapper legacyWrapper)
            {
                var httpClient = legacyWrapper.HttpClient;
                var response = await httpClient.GetAsync(url);
                
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Knowledge not found on server: {KnowledgeName}", knowledgeName);
                    return null;
                }
                
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.LogError("Failed to get knowledge from server. Status code: {StatusCode}", response.StatusCode);
                    return null;
                }

                return await ParseKnowledgeResponse(response);
            }
            
            // Fallback to standard GetAsync for DI scenarios
            return await _apiService.GetAsync<Knowledge>(url);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to load knowledge from server: {KnowledgeName}", knowledgeName);
            return null;
        }
    }

    /// <summary>
    /// Uploads knowledge to the server
    /// </summary>
    /// <param name="knowledge">The knowledge to upload</param>
    /// <returns>True if upload was successful</returns>
    public async Task<bool> UploadKnowledgeToServer(Knowledge knowledge)
    {
        try
        {
            await _apiService.PostAsync(UPLOAD_KNOWLEDGE_URL, knowledge);
            _logger.LogInformation("Successfully uploaded knowledge: {KnowledgeName}", knowledge.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading knowledge to server: {KnowledgeName}", knowledge.Name);
            return false;
        }
    }

    /// <summary>
    /// Builds the server URL for retrieving knowledge
    /// </summary>
    private string BuildKnowledgeUrl(string knowledgeName, string agent)
    {
        return KNOWLEDGE_URL.Replace("{name}", UrlEncoder.Default.Encode(knowledgeName))
                           .Replace("{agent}", UrlEncoder.Default.Encode(agent));
    }

    /// <summary>
    /// Parses the server response into a Knowledge object
    /// </summary>
    private async Task<Knowledge?> ParseKnowledgeResponse(HttpResponseMessage httpResult)
    {
        var response = await httpResult.Content.ReadAsStringAsync();

        try
        {
            var options = new JsonSerializerOptions
            { 
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var knowledge = JsonSerializer.Deserialize<Knowledge>(response, options);

            if (knowledge?.Content == null || knowledge.Name == null)
            {
                _logger.LogError("Failed to deserialize knowledge from server: {Response}", response);
                return null;
            }
            
            return knowledge;
        } 
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to deserialize knowledge from server: {Response}", response);
            return null;
        }
    }

    /// <summary>
    /// Legacy wrapper for BaseApiService to support the parameterless constructor
    /// </summary>
    private class LegacyApiServiceWrapper : BaseApiService
    {
        public LegacyApiServiceWrapper(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
        }
        
        // Expose HttpClient for legacy compatibility
        public new HttpClient HttpClient => base.HttpClient;
    }
}
