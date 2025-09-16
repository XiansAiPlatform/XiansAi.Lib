using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using Agentri.Models;

namespace Agentri.Server;

public class KnowledgeService
{
    private readonly ILogger _logger;
    private const string KNOWLEDGE_URL = "api/agent/knowledge/latest?name={name}&agent={agent}";
    private const string UPLOAD_KNOWLEDGE_URL = "api/agent/knowledge";

    public KnowledgeService()
    {
        _logger = Globals.LogFactory.CreateLogger<KnowledgeService>();
    }

    /// <summary>
    /// Loads knowledge from the server by name
    /// </summary>
    /// <param name="knowledgeName">The name of the knowledge to load</param>
    /// <returns>The loaded knowledge, or null if not found</returns>
    public async Task<Models.Knowledge?> GetKnowledgeFromServer(string knowledgeName, string agent)
    {
        if (!SecureApi.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, cannot load knowledge from server");
            return null;
        }

        var url = BuildKnowledgeUrl(knowledgeName, agent);
        _logger.LogInformation($"Loading knowledge from server: {url}");
        
        try
        {
            var client = SecureApi.Instance.Client;
            var httpResult = await client.GetAsync(url);

            if (httpResult.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation($"Knowledge not found on server: {knowledgeName}");
                return null;
            }
            
            if (httpResult.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogError($"Failed to get knowledge from server. Status code: {httpResult.StatusCode}");
                return null;
            }

            return await ParseKnowledgeResponse(httpResult);
        }
        catch (Exception e)
        {
            _logger.LogError($"Failed to load knowledge from server: {knowledgeName}", e);
            return null;
        }
    }

    /// <summary>
    /// Uploads knowledge to the server
    /// </summary>
    /// <param name="knowledge">The knowledge to upload</param>
    /// <returns>True if upload was successful</returns>
    public async Task<bool> UploadKnowledgeToServer(Models.Knowledge knowledge)
    {
        if (!SecureApi.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping knowledge upload");
            return false;
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var response = await client.PostAsJsonAsync(UPLOAD_KNOWLEDGE_URL, knowledge);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Successfully uploaded knowledge: {knowledge.Name}");
                return true;
            }
            else
            {
                _logger.LogError($"Failed to upload knowledge. Status code: {response.StatusCode}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error uploading knowledge to server: {knowledge.Name}", ex);
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
    private async Task<Models.Knowledge?> ParseKnowledgeResponse(HttpResponseMessage httpResult)
    {
        var response = await httpResult.Content.ReadAsStringAsync();

        try
        {
            var options = new JsonSerializerOptions
            { 
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var knowledge = JsonSerializer.Deserialize<Models.Knowledge>(response, options);

            if (knowledge?.Content == null || knowledge.Name == null)
            {
                _logger.LogError($"Failed to deserialize knowledge from server: {response}");
                return null;
            }
            
            return knowledge;
        } 
        catch (Exception e)
        {
            _logger.LogError($"Failed to deserialize knowledge from server: {response}", e);
            return null;
        }
    }
}
