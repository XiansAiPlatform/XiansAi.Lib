using Temporalio.Workflows;
using Microsoft.Extensions.Caching.Memory;

namespace XiansAi.Knowledge;

public static class KnowledgeHub
{
    private static readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    
    private static MemoryCacheEntryOptions GetCacheOptions()
    {
        var ttlMinutes = Environment.GetEnvironmentVariable("KNOWLEDGE_CACHE_TTL_MINUTES");
        var minutes = 5; // Default to 5 minutes
        
        if (!string.IsNullOrEmpty(ttlMinutes) && int.TryParse(ttlMinutes, out var parsedMinutes) && parsedMinutes > 0)
        {
            minutes = parsedMinutes;
        }
        
        return new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(minutes)
        };
    }

    public static async Task<Models.Knowledge?> Fetch(string knowledgeName)
    {
        // Check cache first
        if (_cache.TryGetValue(knowledgeName, out Models.Knowledge? cached))
        {
            return cached;
        }

        // Fetch from source
        Models.Knowledge? knowledge;
        if (Workflow.InWorkflow) {
            // Go through a Temporal activity to perform IO operations
            knowledge = await Workflow.ExecuteLocalActivityAsync(
                (SystemActivities a) => a.GetKnowledgeAsync(knowledgeName),
                new SystemLocalActivityOptions());
        } else {
            knowledge = await SystemActivities.GetKnowledgeAsyncStatic(knowledgeName);
        }

        // Cache if found
        if (knowledge != null)
        {
            _cache.Set(knowledgeName, knowledge, GetCacheOptions());
        }

        return knowledge;
    }

    public static async Task<bool> Update(string knowledgeName, string knowledgeType, string knowledgeContent)
    {
        try
        {
            bool result;
            if (Workflow.InWorkflow) {
                // Go through a Temporal activity to perform IO operations
                result = await Workflow.ExecuteLocalActivityAsync(
                    (SystemActivities a) => a.UpdateKnowledgeAsync(knowledgeName, knowledgeType, knowledgeContent),
                    new SystemLocalActivityOptions());
            } else {
                result = await SystemActivities.UpdateKnowledgeAsyncStatic(knowledgeName, knowledgeType, knowledgeContent);
            }

            // Invalidate cache on successful update
            if (result)
            {
                _cache.Remove(knowledgeName);
            }

            return result;
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to update knowledge: {knowledgeName}", e);
        }
    }

}
