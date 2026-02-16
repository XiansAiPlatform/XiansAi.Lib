using System.Reflection;

namespace Xians.Lib.Agents.Knowledge;

/// <summary>
/// Extension methods for loading embedded resources and uploading them as knowledge to the Xians platform.
/// Allows knowledge files to be embedded in assemblies and uploaded at runtime.
/// </summary>
public static class EmbeddedKnowledgeLoader
{
    /// <summary>
    /// Loads an embedded resource from the calling assembly and uploads it to the knowledge collection.
    /// </summary>
    /// <param name="knowledgeCollection">The knowledge collection to upload to.</param>
    /// <param name="resourcePath">The relative path of the embedded resource (e.g., "WebAgent/web-agent-prompt.md").</param>
    /// <param name="knowledgeName">Optional custom name for the knowledge item. If null, uses the file name.</param>
    /// <param name="knowledgeType">Optional knowledge type (e.g., "instruction", "document", "markdown"). If null, inferred from file extension.</param>
    /// <param name="systemScoped">Optional override for system scoping. If null, uses the agent's SystemScoped setting.</param>
    /// <param name="description">Optional description of the knowledge item.</param>
    /// <param name="visible">Whether the knowledge item is visible. Defaults to true.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the upload succeeds.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the embedded resource is not found.</exception>
    /// <example>
    /// <code>
    /// // In .csproj:
    /// // &lt;ItemGroup&gt;
    /// //   &lt;EmbeddedResource Include="Knowledge\**\*.md" /&gt;
    /// // &lt;/ItemGroup&gt;
    /// 
    /// // In Program.cs:
    /// await agent.Knowledge.UploadEmbeddedResourceAsync(
    ///     resourcePath: "Knowledge/system-prompt.md",
    ///     knowledgeName: "system-prompt",
    ///     knowledgeType: "markdown"
    /// );
    /// </code>
    /// </example>
    public static async Task<bool> UploadEmbeddedResourceAsync(
        this KnowledgeCollection knowledgeCollection,
        string resourcePath,
        string? knowledgeName = null,
        string? knowledgeType = null,
        bool? systemScoped = null,
        string? description = null,
        bool visible = true,
        CancellationToken cancellationToken = default)
    {
        // Validate or derive the name
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(resourcePath);
        var normalizedFileName = fileNameWithoutExt.Replace(" ", "-").ToLowerInvariant();

        string name;
        if (!string.IsNullOrWhiteSpace(knowledgeName))
        {
            var normalizedName = knowledgeName.Replace(" ", "-").ToLowerInvariant();
            if (normalizedName != normalizedFileName)
            {
                var derivedName = DeriveNameFromFileName(fileNameWithoutExt);
                throw new ArgumentException(
                    $"The provided knowledge name '{knowledgeName}' does not match the embedded resource file name. " +
                    $"When normalized (spaces replaced with hyphens, lowercased), the name becomes '{normalizedName}', " +
                    $"but the resource file name normalizes to '{normalizedFileName}'. " +
                    $"Either use a matching name (e.g., '{derivedName}' or '{normalizedFileName}') or omit the name parameter to derive it automatically from the file.",
                    nameof(knowledgeName));
            }
            name = knowledgeName;
        }
        else
        {
            name = DeriveNameFromFileName(fileNameWithoutExt);
        }

        // Load the embedded resource content from the calling assembly
        var content = LoadEmbeddedResource(resourcePath);
        
        // Infer knowledge type from file extension if not provided
        var type = knowledgeType ?? InferKnowledgeType(resourcePath);
        
        // Upload to the knowledge collection
        // If systemScoped is not provided, the UpdateAsync method will use the agent's SystemScoped setting
        return await knowledgeCollection.UpdateAsync(name, content, type, systemScoped, description, visible, cancellationToken);
    }

    /// <summary>
    /// Uploads raw text as knowledge without requiring an embedded resource.
    /// </summary>
    /// <param name="knowledgeCollection">The knowledge collection to upload to.</param>
    /// <param name="knowledgeName">The name of the knowledge item.</param>
    /// <param name="content">The text content to upload.</param>
    /// <param name="knowledgeType">Optional knowledge type (e.g., "instruction", "document", "markdown"). Defaults to "text" when null.</param>
    /// <param name="visible">Whether the knowledge item is visible. Defaults to true.</param>
    /// <param name="description">Optional description of the knowledge item.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the upload succeeds.</returns>
    public static async Task<bool> UploadTextResourceAsync(
        this KnowledgeCollection knowledgeCollection,
        string knowledgeName,
        string content,
        string? knowledgeType = null,
        bool visible = true,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (knowledgeCollection is null)
            throw new ArgumentNullException(nameof(knowledgeCollection));
        if (string.IsNullOrWhiteSpace(knowledgeName))
            throw new ArgumentException("Knowledge name is required.", nameof(knowledgeName));
        if (content is null)
            throw new ArgumentNullException(nameof(content));

        var type = string.IsNullOrWhiteSpace(knowledgeType) ? "text" : knowledgeType;
        return await knowledgeCollection.UpdateAsync(knowledgeName, content, type, systemScoped: null, description: description, visible, cancellationToken);
    }

    private static string LoadEmbeddedResource(string resourcePath)
    {
        // Normalize the resource path to match embedded resource naming convention
        // Replace forward slashes and backslashes with dots
        var normalizedPath = resourcePath.Replace("/", ".").Replace("\\", ".");
        
        // First, try the entry assembly (the executing application)
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly != null)
        {
            var content = TryLoadFromAssembly(entryAssembly, normalizedPath);
            if (content != null)
                return content;
        }
        
        // If not found in entry assembly, search all loaded assemblies
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // Skip system assemblies
            var assemblyName = assembly.GetName().Name;
            if (assemblyName == null || assemblyName.StartsWith("System.") || assemblyName.StartsWith("Microsoft."))
                continue;
            
            var content = TryLoadFromAssembly(assembly, normalizedPath);
            if (content != null)
                return content;
        }
        
        // Resource not found - provide helpful error message with detailed search information
        var searchedAssemblies = new List<string>();
        var allResources = new List<string>();
        
        if (entryAssembly != null)
        {
            var resources = entryAssembly.GetManifestResourceNames();
            searchedAssemblies.Add($"{entryAssembly.GetName().Name} (entry assembly)");
            allResources.AddRange(resources);
        }
        
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var assemblyName = assembly.GetName().Name;
            if (assemblyName != null && !assemblyName.StartsWith("System.") && !assemblyName.StartsWith("Microsoft."))
            {
                var resources = assembly.GetManifestResourceNames();
                if (resources.Length > 0)
                {
                    searchedAssemblies.Add($"{assemblyName} ({resources.Length} resources)");
                    allResources.AddRange(resources);
                }
            }
        }
        
        // Find similar resource names to help with debugging
        var similarResources = allResources
            .Where(r => r.Contains(normalizedPath) || normalizedPath.Contains(Path.GetFileNameWithoutExtension(r)))
            .Take(5)
            .ToList();
        
        var errorMessage = $"Embedded resource with path '{resourcePath}' not found. " +
                          $"Searched assemblies: {string.Join(", ", searchedAssemblies)}. " +
                          $"Make sure the file is marked as an EmbeddedResource in the .csproj file.";
        
        if (similarResources.Any())
        {
            errorMessage += $" Similar resources found: {string.Join(", ", similarResources)}.";
        }
        
        throw new FileNotFoundException(errorMessage);
    }

    /// <summary>
    /// Attempts to load an embedded resource from the specified assembly using multiple naming strategies.
    /// </summary>
    /// <param name="assembly">The assembly to search in.</param>
    /// <param name="normalizedPath">The normalized resource path (with dots instead of slashes).</param>
    /// <returns>The resource content if found, otherwise null.</returns>
    private static string? TryLoadFromAssembly(Assembly assembly, string normalizedPath)
    {
        var assemblyName = assembly.GetName().Name;
        if (assemblyName == null)
            return null;

        // Strategy 1: Try with assembly name (original approach)
        var resourceName1 = $"{assemblyName}.{normalizedPath}";
        var content = TryGetManifestResourceString(assembly, resourceName1);
        if (content != null)
            return content;

        // Strategy 2: Try to find the resource by examining all manifest resource names
        // and looking for ones that end with our normalized path
        var allResources = assembly.GetManifestResourceNames();
        var matchingResource = allResources.FirstOrDefault(r => r.EndsWith($".{normalizedPath}"));
        if (matchingResource != null)
        {
            content = TryGetManifestResourceString(assembly, matchingResource);
            if (content != null)
                return content;
        }

        // Strategy 3: Try alternative naming patterns for common assembly/namespace mismatches
        var alternativeNames = GenerateAlternativeResourceNames(assemblyName, normalizedPath);
        foreach (var alternativeName in alternativeNames)
        {
            content = TryGetManifestResourceString(assembly, alternativeName);
            if (content != null)
                return content;
        }

        return null;
    }

    /// <summary>
    /// Generates alternative resource names to try when the standard assembly.path approach fails.
    /// </summary>
    /// <param name="assemblyName">The assembly name.</param>
    /// <param name="normalizedPath">The normalized resource path.</param>
    /// <returns>A list of alternative resource names to try.</returns>
    private static IEnumerable<string> GenerateAlternativeResourceNames(string assemblyName, string normalizedPath)
    {
        var alternatives = new List<string>();

        // Convert hyphens to underscores (common in root namespaces)
        if (assemblyName.Contains("-"))
        {
            var underscoreVersion = assemblyName.Replace("-", "_");
            alternatives.Add($"{underscoreVersion}.{normalizedPath}");
        }

        // Convert underscores to hyphens
        if (assemblyName.Contains("_"))
        {
            var hyphenVersion = assemblyName.Replace("_", "-");
            alternatives.Add($"{hyphenVersion}.{normalizedPath}");
        }

        // Try without dots (in case assembly name has dots)
        var noDotVersion = assemblyName.Replace(".", "");
        if (noDotVersion != assemblyName)
        {
            alternatives.Add($"{noDotVersion}.{normalizedPath}");
        }

        // Try common project name patterns
        if (assemblyName.Contains("."))
        {
            // Try just the last part after the dot
            var lastPart = assemblyName.Split('.').Last();
            alternatives.Add($"{lastPart}.{normalizedPath}");
        }

        return alternatives.Distinct();
    }

    /// <summary>
    /// Safely attempts to get a manifest resource stream and read its content.
    /// </summary>
    /// <param name="assembly">The assembly to read from.</param>
    /// <param name="resourceName">The full resource name.</param>
    /// <returns>The resource content if found, otherwise null.</returns>
    private static string? TryGetManifestResourceString(Assembly assembly, string resourceName)
    {
        try
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return null;

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch
        {
            // Ignore any exceptions and return null
            return null;
        }
    }

    /// <summary>
    /// Derives a human-friendly name from a file name (without extension).
    /// Converts hyphen/underscore-separated segments to Title Case with spaces (e.g., "web-agent-prompt" -> "Web Agent Prompt").
    /// </summary>
    /// <param name="fileNameWithoutExt">The file name without extension.</param>
    /// <returns>A display name with spaces and capital starting letters in words.</returns>
    private static string DeriveNameFromFileName(string fileNameWithoutExt)
    {
        if (string.IsNullOrWhiteSpace(fileNameWithoutExt))
            return fileNameWithoutExt;

        var words = fileNameWithoutExt.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries);
        var titleCased = words.Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant());
        return string.Join(" ", titleCased);
    }

    /// <summary>
    /// Infers the knowledge type from the file extension.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <returns>The inferred knowledge type.</returns>
    private static string InferKnowledgeType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        return extension switch
        {
            ".md" => "markdown",
            ".json" => "json",
            ".txt" => "text",
            ".xml" => "xml",
            ".yaml" or ".yml" => "yaml",
            _ => "text"
        };
    }
}
