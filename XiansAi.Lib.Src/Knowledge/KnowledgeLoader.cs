using Server;
using XiansAi.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace XiansAi.Knowledge;

/// <summary>
/// Shared cache for knowledge items.
/// </summary>
internal static class KnowledgeCache
{
    public static readonly IMemoryCache Cache = new MemoryCache(new MemoryCacheOptions());
    
    public static MemoryCacheEntryOptions GetCacheOptions()
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
}

/// <summary>
/// Defines a service for loading instructions from either a server or local filesystem.
/// </summary>
public interface IKnowledgeLoader
{
    /// <summary>
    /// Loads an instruction by name from the available sources.
    /// </summary>
    /// <param name="instructionName">The name of the instruction to load</param>
    /// <returns>The loaded instruction, or null if not found</returns>
    Task<Models.Knowledge?> Load(string instructionName);
}

/// <summary>
/// Implementation of the instruction loader that can retrieve instructions
/// from either an API server or local files based on configuration and availability.
/// </summary>
public class KnowledgeLoaderImpl : IKnowledgeLoader
{
    private readonly Logger<KnowledgeLoaderImpl> _logger = Logger<KnowledgeLoaderImpl>.For();
    private readonly KnowledgeService _knowledgeService = new KnowledgeService();

    // Path to local instructions folder, configured via environment variable
    private readonly string? _localFolder = Environment.GetEnvironmentVariable("LOCAL_KNOWLEDGE_FOLDER");

    /// <summary>
    /// Loads an instruction by name from either the server or local filesystem.
    /// </summary>
    /// <param name="instructionName">The name of the instruction to load</param>
    /// <returns>The loaded instruction, or null if not found</returns>
    /// <exception cref="ArgumentException">Thrown if instructionName is null or empty</exception>
    public async Task<Models.Knowledge?> Load(string instructionName)
    {
        if (string.IsNullOrEmpty(instructionName))
        {
            throw new ArgumentException("Instruction name cannot be null or empty", nameof(instructionName));
        }
        
        // Check cache first
        if (KnowledgeCache.Cache.TryGetValue(instructionName, out Models.Knowledge? cached))
        {
            _logger.LogDebug($"Fetching Knowledge from cache: {instructionName}");
            return cached;
        }
        
        _logger.LogDebug($"Fetching Knowledge from source: {instructionName}");
        
        // Fall back to local loading if server connection isn't available
        Models.Knowledge? knowledge;
        if (!string.IsNullOrEmpty(_localFolder))
        { 
            _logger.LogWarning($"Loading instruction locally - {instructionName}");
            knowledge = await LoadFromLocal(instructionName);
        }
        else
        {
            _logger.LogDebug($"Loading instruction from server - {instructionName}");
            knowledge = await LoadFromServer(instructionName);
        }
        
        // Cache if found
        if (knowledge != null)
        {
            KnowledgeCache.Cache.Set(instructionName, knowledge, KnowledgeCache.GetCacheOptions());
        }
        
        return knowledge;
    }

    /// <summary>
    /// Loads an instruction from the local filesystem.
    /// </summary>
    /// <param name="instructionName">The name of the instruction to load</param>
    /// <returns>The loaded instruction</returns>
    /// <exception cref="InvalidOperationException">Thrown if local instructions folder is not configured or multiple matching files found</exception>
    /// <exception cref="FileNotFoundException">Thrown if instruction file not found</exception>
    /// <exception cref="ArgumentException">Thrown if instructionName contains path traversal characters</exception>
    private async Task<Models.Knowledge?> LoadFromLocal(string instructionName)
    {
        if (string.IsNullOrEmpty(_localFolder))
        {
            throw new InvalidOperationException("LOCAL_INSTRUCTIONS_FOLDER environment variable is not set. Please set it to the path of the local instructions folder.");
        }

        // Security: Validate instructionName to prevent path traversal attacks
        if (string.IsNullOrWhiteSpace(instructionName))
        {
            throw new ArgumentException("Instruction name cannot be null or empty", nameof(instructionName));
        }

        if (instructionName.Contains("..") || instructionName.Contains("/") || instructionName.Contains("\\") || 
            instructionName.Contains(":") || Path.IsPathRooted(instructionName))
        {
            _logger.LogWarning($"Potential path traversal attempt detected: {instructionName}");
            throw new ArgumentException("Invalid instruction name. Path traversal or absolute paths are not allowed.", nameof(instructionName));
        }

        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(instructionName);
        var searchPattern = fileNameWithoutExt + ".*";
        
        // Search for files both with and without extensions to support different file formats
        var matchingFiles = Directory.GetFiles(_localFolder, searchPattern)
            .Concat(Directory.GetFiles(_localFolder, fileNameWithoutExt))
            .Distinct()
            .ToList();

        if (matchingFiles.Count > 1)
        {
            var fileList = string.Join(", ", matchingFiles.Select(Path.GetFileName));
            _logger.LogError($"Multiple matching files found for '{fileNameWithoutExt}': {fileList}");
            throw new InvalidOperationException($"Multiple matching files found for '{fileNameWithoutExt}'. Found: {fileList}");
        }
        
        if (matchingFiles.Count == 0)
        {
            _logger.LogError($"No instruction file found with name: '{fileNameWithoutExt}' in folder: '{_localFolder}'");
            return null;
        }

        var instructionPath = matchingFiles[0];
        return new Models.Knowledge
        { 
            Content = await File.ReadAllTextAsync(instructionPath), 
            Name = instructionName,
            Id = null // Indicate that this is a local instruction from a file
        };
    }

    /// <summary>
    /// Loads an instruction from the server via API.
    /// </summary>
    /// <param name="instructionName">The name of the instruction to load</param>
    /// <returns>The loaded instruction, or null if not found on server</returns>
    public async Task<Models.Knowledge?> LoadFromServer(string instructionName)
    {
        var agent = AgentContext.AgentName;
        _logger.LogInformation($"Loading instruction from server: {instructionName} for agent: {agent}");
        return await _knowledgeService.GetKnowledgeFromServer(instructionName, agent);
    }
}
