using Server;
using XiansAi.Logging;

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
    private readonly Logger<KnowledgeUpdaterImpl> _logger = Logger<KnowledgeUpdaterImpl>.For();
    private readonly KnowledgeService _knowledgeService = new KnowledgeService();

    // Path to local knowledges folder, configured via environment variable
    private readonly string? _localknowledgesFolder = Environment.GetEnvironmentVariable("LOCAL_knowledgeS_FOLDER");

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

        // Fall back to local updating if server connection isn't available
        if (!string.IsNullOrEmpty(_localknowledgesFolder))
        {
            _logger.LogWarning($"App server connection not ready, updating knowledge locally in {_localknowledgesFolder}");
            _logger.LogWarning($"Updating knowledge locally - {knowledgeName}");
            return await UpdateLocal(knowledgeName, knowledgeContent);
        }

        _logger.LogDebug($"Updating knowledge on server - {knowledgeName}");
        return await UpdateOnServer(knowledgeName, knowledgeType, knowledgeContent);
    }

    /// <summary>
    /// Updates an knowledge in the local filesystem.
    /// </summary>
    /// <param name="knowledgeName">The name of the knowledge to update</param>
    /// <param name="content">The content of the knowledge to update</param>
    /// <returns>A task representing the asynchronous operation</returns>
    private Task<bool> UpdateLocal(string knowledgeName, string content)
    {
        // TODO: Implement logic to update the knowledge in the local filesystem
        throw new NotImplementedException("UpdateLocal method is not implemented yet.");
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
        var agent = AgentContext.Agent;
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
        return await Task.FromResult(true);
    }
}
