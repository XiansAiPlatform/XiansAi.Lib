using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Xians.Lib.Agents.Knowledge.Providers;

/// <summary>
/// Provides knowledge from embedded resources for local/unit testing.
/// Does not make HTTP calls - all data comes from embedded resources or in-memory storage.
/// </summary>
/// <remarks>
/// Resource naming convention: {AgentName}.Knowledge.{KnowledgeName}.{extension}
/// Example embedded resource: "MyAgent.Knowledge.system-prompt.md"
/// 
/// Supported extensions: .md, .txt, .json, .yaml, .yml
/// 
/// To use in tests:
/// 1. Add files to your test project
/// 2. Mark them as EmbeddedResource in .csproj:
///    &lt;EmbeddedResource Include="TestData\MyAgent.Knowledge.*.md" /&gt;
/// 3. Enable LocalMode in XiansOptions
/// </remarks>
internal class LocalKnowledgeProvider : IKnowledgeProvider
{
    private readonly Assembly[] _assemblies;
    private readonly ILogger _logger;
    private readonly Dictionary<string, Models.Knowledge> _inMemoryStore = new();
    private readonly object _storeLock = new();

    public LocalKnowledgeProvider(Assembly[]? assemblies, ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // If assemblies not specified, search all non-system assemblies
        _assemblies = assemblies ?? AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && 
                       !IsSystemAssembly(a))
            .ToArray();

        _logger.LogInformation(
            "[LocalMode] LocalKnowledgeProvider initialized with {Count} assemblies to search",
            _assemblies.Length);
    }

    public Task<Models.Knowledge?> GetAsync(
        string knowledgeName,
        string agentName,
        string? tenantId,
        string? activationName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "[LocalMode] Fetching knowledge: Name={Name}, Agent={Agent}, Tenant={Tenant}",
            knowledgeName, agentName, tenantId);

        // Check in-memory store first (for updated/created knowledge)
        var storeKey = GetStoreKey(tenantId, agentName, activationName, knowledgeName);
        lock (_storeLock)
        {
            if (_inMemoryStore.TryGetValue(storeKey, out var stored))
            {
                _logger.LogDebug(
                    "[LocalMode] Knowledge found in memory store: {Name}",
                    knowledgeName);
                return Task.FromResult<Models.Knowledge?>(stored);
            }
        }

        // Try to load from embedded resources
        var knowledge = LoadFromEmbeddedResource(knowledgeName, agentName, systemScoped: false);
        return Task.FromResult(knowledge);
    }

    public Task<Models.Knowledge?> GetSystemAsync(
        string knowledgeName,
        string agentName,
        string? activationName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "[LocalMode] Fetching system knowledge: Name={Name}, Agent={Agent}",
            knowledgeName, agentName);

        // Check in-memory store first
        var storeKey = GetStoreKey(null, agentName, activationName, knowledgeName);
        lock (_storeLock)
        {
            if (_inMemoryStore.TryGetValue(storeKey, out var stored))
            {
                _logger.LogDebug(
                    "[LocalMode] System knowledge found in memory store: {Name}",
                    knowledgeName);
                return Task.FromResult<Models.Knowledge?>(stored);
            }
        }

        // Try to load from embedded resources
        var knowledge = LoadFromEmbeddedResource(knowledgeName, agentName, systemScoped: true);
        return Task.FromResult(knowledge);
    }

    public Task<bool> UpdateAsync(
        string knowledgeName,
        string content,
        string? type,
        string agentName,
        string? tenantId,
        bool systemScoped = false,
        string? activationName = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "[LocalMode] Updating knowledge: Name={Name}, Agent={Agent}, SystemScoped={SystemScoped}",
            knowledgeName, agentName, systemScoped);

        var knowledge = new Models.Knowledge
        {
            Name = knowledgeName,
            Content = content,
            Type = type ?? "text",
            Agent = agentName,
            SystemScoped = systemScoped
        };

        var storeKey = GetStoreKey(tenantId, agentName, activationName, knowledgeName);
        lock (_storeLock)
        {
            _inMemoryStore[storeKey] = knowledge;
        }

        _logger.LogDebug(
            "[LocalMode] Knowledge updated successfully: {Name}",
            knowledgeName);

        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(
        string knowledgeName,
        string agentName,
        string? tenantId,
        string? activationName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "[LocalMode] Deleting knowledge: Name={Name}, Agent={Agent}",
            knowledgeName, agentName);

        var storeKey = GetStoreKey(tenantId, agentName, activationName, knowledgeName);
        bool existed;
        lock (_storeLock)
        {
            existed = _inMemoryStore.Remove(storeKey);
        }

        if (existed)
        {
            _logger.LogDebug(
                "[LocalMode] Knowledge deleted successfully: {Name}",
                knowledgeName);
        }
        else
        {
            _logger.LogDebug(
                "[LocalMode] Knowledge not found for deletion: {Name}",
                knowledgeName);
        }

        return Task.FromResult(existed);
    }

    public Task<List<Models.Knowledge>> ListAsync(
        string agentName,
        string? tenantId,
        string? activationName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "[LocalMode] Listing knowledge: Agent={Agent}, Tenant={Tenant}",
            agentName, tenantId);

        List<Models.Knowledge> results;
        lock (_storeLock)
        {
            results = _inMemoryStore.Values
                .Where(k => k.Agent == agentName)
                .ToList();
        }

        _logger.LogDebug(
            "[LocalMode] Found {Count} knowledge items for agent {Agent}",
            results.Count, agentName);

        return Task.FromResult(results);
    }

    /// <summary>
    /// Loads knowledge from embedded resources using naming conventions.
    /// </summary>
    private Models.Knowledge? LoadFromEmbeddedResource(
        string knowledgeName,
        string agentName,
        bool systemScoped)
    {
        // Convention 1: {AgentName}.Knowledge.{KnowledgeName}.{extension}
        // Convention 2 (fallback): *.{normalizedKnowledgeName}.{extension} (e.g. *.article-extraction-schema.json)
        var extensions = new[] { "md", "txt", "json"};
        var normalizedName = knowledgeName
            .Replace(" ", "-")
            .ToLowerInvariant();
        var searchedFileNames = new HashSet<string>();

        foreach (var assembly in _assemblies)
        {
            foreach (var ext in extensions)
            {
                // Primary: strict convention
                var resourceName = $"{agentName}.Knowledge.{knowledgeName}.{ext}";
                searchedFileNames.Add(resourceName);
                var content = TryLoadResource(assembly, resourceName);

                // Fallback: match any resource ending with .{normalizedName}.{ext}
                if (content == null)
                {
                    var fallbackPattern = $"*.{normalizedName}.{ext}";
                    searchedFileNames.Add(fallbackPattern);
                    var fallbackSuffix = $".{normalizedName}.{ext}";
                    content = TryLoadResourceBySuffix(assembly, fallbackSuffix);
                }

                if (content != null)
                {
                    var type = InferKnowledgeType($".{ext}");
                    _logger.LogDebug(
                        "[LocalMode] Loaded knowledge from embedded resource: Name={Name} in {Assembly}",
                        knowledgeName, assembly.GetName().Name);

                    return new Models.Knowledge
                    {
                        Name = knowledgeName,
                        Content = content,
                        Type = type,
                        Agent = agentName,
                        SystemScoped = systemScoped
                    };
                }
            }
        }

        var searchedList = string.Join(", ", searchedFileNames.Order());
        _logger.LogWarning(
            "[LocalMode] Knowledge not found. Name={Name}, Agent={Agent}. Searched for these file names: [{SearchedFileNames}]",
            knowledgeName, agentName, searchedList);

        return null;
    }

    /// <summary>
    /// Tries to load a resource by matching any manifest resource name that ends with the given suffix.
    /// </summary>
    private string? TryLoadResourceBySuffix(Assembly assembly, string suffix)
    {
        try
        {
            var allResources = assembly.GetManifestResourceNames();
            var matchingResource = allResources.FirstOrDefault(r =>
                r.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

            if (matchingResource == null)
                return null;

            using var stream = assembly.GetManifestResourceStream(matchingResource);
            if (stream == null)
                return null;

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[LocalMode] Failed to load resource by suffix: {Suffix} from {Assembly}",
                suffix, assembly.GetName().Name);
            return null;
        }
    }

    /// <summary>
    /// Attempts to load a resource from an assembly by searching for matching resource names.
    /// </summary>
    private string? TryLoadResource(Assembly assembly, string resourceName)
    {
        try
        {
            var allResources = assembly.GetManifestResourceNames();
            
            // Try exact match first (case-insensitive)
            var matchingResource = allResources.FirstOrDefault(r =>
                r.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));

            if (matchingResource != null)
            {
                using var stream = assembly.GetManifestResourceStream(matchingResource);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    return reader.ReadToEnd();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[LocalMode] Failed to load resource: {ResourceName} from {Assembly}",
                resourceName, assembly.GetName().Name);
        }

        return null;
    }

    /// <summary>
    /// Generates a unique key for the in-memory store.
    /// </summary>
    private string GetStoreKey(
        string? tenantId,
        string agentName,
        string? activationName,
        string knowledgeName)
    {
        return $"{tenantId ?? "system"}:{agentName}:{activationName ?? "default"}:{knowledgeName}";
    }

    /// <summary>
    /// Infers knowledge type from file extension.
    /// </summary>
    private string InferKnowledgeType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".md" => "markdown",
            ".json" => "json",
            ".txt" => "text",
            ".xml" => "xml",
            ".yaml" or ".yml" => "yaml",
            _ => "text"
        };
    }

    /// <summary>
    /// Checks if an assembly is a system assembly that should be skipped.
    /// </summary>
    private static bool IsSystemAssembly(Assembly assembly)
    {
        var name = assembly.GetName().Name;
        if (name == null) return true;

        return name.StartsWith("System.") ||
               name.StartsWith("Microsoft.") ||
               name.StartsWith("netstandard") ||
               name.StartsWith("mscorlib");
    }
}
