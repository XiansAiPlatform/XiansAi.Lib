using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Server;

namespace XiansAi.Knowledge;

/// <summary>
/// Defines a service for updating knowledges either on a server or locally.
/// </summary>
public interface IKnowledgeUpdater
{
    /// <summary>
    /// Updates an knowledge by name to the available sources.
    /// </summary>
    /// <param name="knowledgeName">The name of the knowledge to update</param>
    /// <param name="knowledgeType">The type of the knowledge to update</param>
    /// <param name="knowledgeContent">The content of the knowledge to update</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task<bool> Update(string knowledgeName, string knowledgeType, string knowledgeContent);
}

/// <summary>
/// Implementation of the knowledge updater that can update knowledges
/// to either an API server or local files based on configuration and availability.
/// </summary>
public class KnowledgeUpdaterImpl : IKnowledgeUpdater
{
    private readonly ILogger<KnowledgeUpdaterImpl> _logger = Globals.LogFactory.CreateLogger<KnowledgeUpdaterImpl>();
    private readonly KnowledgeService _knowledgeService = new KnowledgeService();

    /// <summary>
    /// Updates an knowledge by name to either the server or local filesystem.
    /// </summary>
    /// <param name="knowledgeName">The name of the knowledge to update</param>
    /// <param name="knowledgeType">The type of the knowledge to update</param>
    /// <param name="knowledgeContent">The content of the knowledge to update</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="ArgumentException">Thrown if knowledgeName or content is null or empty</exception>
    public async Task<bool> Update(string knowledgeName, string knowledgeType, string knowledgeContent)
    {
        if (string.IsNullOrEmpty(knowledgeName))
        {
            throw new ArgumentException("knowledge name cannot be null or empty", nameof(knowledgeName));
        }

        if (string.IsNullOrEmpty(knowledgeContent))
        {
            throw new ArgumentException("Content cannot be null or empty", nameof(knowledgeContent));
        }

        _logger.LogDebug($"Updating knowledge on server - {knowledgeName}");
        return await UpdateOnServer(knowledgeName, knowledgeType, knowledgeContent);
    }

    /// <summary>
    /// Updates an knowledge on the server via API.
    /// </summary>
    /// <param name="knowledgeName">The name of the knowledge to update</param>
    /// <param name="knowledgeType">The type of the knowledge to update</param>
    /// <param name="knowledgeContent">The content of the knowledge to update</param>
    /// <returns>A task representing the asynchronous operation</returns>
    private async Task<bool> UpdateOnServer(string knowledgeName, string knowledgeType, string knowledgeContent)
    {
        var agent = AgentContext.AgentName;
        _logger.LogInformation($"Updating knowledge on server: {knowledgeName} for agent: {agent}");
        _logger.LogInformation($"Content: {knowledgeContent}");

        // Prepare knowledge for upload
        var knowledge = new Models.Knowledge
        {
            Name = knowledgeName,
            Content = knowledgeContent,
            Type = knowledgeType,
            Agent = agent
        };

        _logger.LogInformation($"Uploading knowledge to server: {knowledge}");

        // Upload knowledge to server
        await _knowledgeService.UploadKnowledgeToServer(knowledge);
        
        // Invalidate cache
        KnowledgeCache.Cache.Remove(knowledgeName);
        
        return await Task.FromResult(true);
    }
}
