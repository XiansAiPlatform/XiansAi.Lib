using Microsoft.Extensions.Logging;
using System.Text.Json;
using XiansAi.Knowledge;

namespace XiansAi.Flow.Router.Plugins;

/// <summary>
/// Provides functionality to load capability knowledge from JSON content via the KnowledgeLoader.
/// </summary>
public class CapabilityKnowledgeLoader
{
    private static readonly ILogger _logger = Globals.LogFactory.CreateLogger<CapabilityKnowledgeLoader>();
    private static readonly IKnowledgeLoader _knowledgeLoader = new KnowledgeLoaderImpl();
    
    /// <summary>
    /// Loads capability knowledge using the KnowledgeLoader service.
    /// </summary>
    /// <param name="knowledgeKey">The key/name of the knowledge to load.</param>
    /// <returns>The loaded capability knowledge model.</returns>
    public static CapabilityKnowledgeModel? Load(string knowledgeKey)
    {
        try
        {
            // Use the KnowledgeLoader to get the content
            var knowledgeTask = _knowledgeLoader.Load(knowledgeKey);
            knowledgeTask.Wait();  // We need to wait for the async task to complete since our method is synchronous
            
            var knowledge = knowledgeTask.Result;
            
            if (knowledge == null)
            {
                _logger.LogError("Capability knowledge not found: {knowledgeKey}", knowledgeKey);   
                return null;
            }

            var jsonContent = knowledge.Content;
            
            // Security: Validate content size
            const int MaxContentSize = 1 * 1024 * 1024; // 1 MB
            if (jsonContent.Length > MaxContentSize)
            {
                throw new InvalidOperationException($"Capability knowledge content size {jsonContent.Length} exceeds maximum allowed size of {MaxContentSize} bytes");
            }

            var options = new JsonSerializerOptions
            { 
                PropertyNameCaseInsensitive = true,
                // Security: Limit JSON depth to prevent deeply nested attacks
                MaxDepth = 32
            };
            
            var knowledgeModel = JsonSerializer.Deserialize<CapabilityKnowledgeModel>(jsonContent, options);
            
            if (knowledgeModel == null)
            {
                throw new InvalidOperationException($"Failed to deserialize capability knowledge: {knowledgeKey}");
            }

            // Validate that required properties are present
            if (string.IsNullOrEmpty(knowledgeModel.Description))
            {
                throw new InvalidOperationException($"Capability knowledge {knowledgeKey} is missing a description");
            }
            
            if (string.IsNullOrEmpty(knowledgeModel.Returns))
            {
                throw new InvalidOperationException($"Capability knowledge {knowledgeKey} is missing a return description");
            }
            
            return knowledgeModel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to load capability knowledge: {knowledgeKey}");
            throw;
        }
    }
    
    /// <summary>
    /// Asynchronously loads capability knowledge using the KnowledgeLoader service.
    /// </summary>
    /// <param name="knowledgeKey">The key/name of the knowledge to load.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the loaded capability knowledge model.</returns>
    public static async Task<CapabilityKnowledgeModel?> LoadAsync(string knowledgeKey)
    {
        try
        {
            // Use the KnowledgeLoader to get the content asynchronously
            var knowledge = await _knowledgeLoader.Load(knowledgeKey);
            
            if (knowledge == null)
            {
                _logger.LogError("Capability knowledge not found: {knowledgeKey}", knowledgeKey);   
                return null;
            }

            var jsonContent = knowledge.Content;
            
            // Security: Validate content size
            const int MaxContentSize = 1 * 1024 * 1024; // 1 MB
            if (jsonContent.Length > MaxContentSize)
            {
                throw new InvalidOperationException($"Capability knowledge content size {jsonContent.Length} exceeds maximum allowed size of {MaxContentSize} bytes");
            }

            var options = new JsonSerializerOptions
            { 
                PropertyNameCaseInsensitive = true,
                // Security: Limit JSON depth to prevent deeply nested attacks
                MaxDepth = 32
            };
            
            var knowledgeModel = JsonSerializer.Deserialize<CapabilityKnowledgeModel>(jsonContent, options);
            
            if (knowledgeModel == null)
            {
                throw new InvalidOperationException($"Failed to deserialize capability knowledge: {knowledgeKey}");
            }

            // Validate that required properties are present
            if (string.IsNullOrEmpty(knowledgeModel.Description))
            {
                throw new InvalidOperationException($"Capability knowledge {knowledgeKey} is missing a description");
            }
            
            if (string.IsNullOrEmpty(knowledgeModel.Returns))
            {
                throw new InvalidOperationException($"Capability knowledge {knowledgeKey} is missing a return description");
            }
            
            return knowledgeModel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to load capability knowledge: {knowledgeKey}");
            throw;
        }
    }
} 