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
        CancellationToken cancellationToken = default)
    {
        // Load the embedded resource content from the calling assembly
        var content = LoadEmbeddedResource(resourcePath);
        
        // Determine the knowledge name (use filename if not provided)
        var name = knowledgeName ?? Path.GetFileName(resourcePath);
        
        // Infer knowledge type from file extension if not provided
        var type = knowledgeType ?? InferKnowledgeType(resourcePath);
        
        // Upload to the knowledge collection
        // If systemScoped is not provided, the UpdateAsync method will use the agent's SystemScoped setting
        return await knowledgeCollection.UpdateAsync(name, content, type, systemScoped, cancellationToken);
    }

    /// <summary>
    /// Uploads raw text as knowledge without requiring an embedded resource.
    /// </summary>
    /// <param name="knowledgeCollection">The knowledge collection to upload to.</param>
    /// <param name="knowledgeName">The name of the knowledge item.</param>
    /// <param name="content">The text content to upload.</param>
    /// <param name="knowledgeType">Optional knowledge type (e.g., "instruction", "document", "markdown"). Defaults to "text" when null.</param>
    /// <param name="systemScoped">Optional override for system scoping. If null, uses the agent's SystemScoped setting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the upload succeeds.</returns>
    public static async Task<bool> UploadTextResourceAsync(
        this KnowledgeCollection knowledgeCollection,
        string knowledgeName,
        string content,
        string? knowledgeType = null,
        CancellationToken cancellationToken = default)
    {
        if (knowledgeCollection is null)
            throw new ArgumentNullException(nameof(knowledgeCollection));
        if (string.IsNullOrWhiteSpace(knowledgeName))
            throw new ArgumentException("Knowledge name is required.", nameof(knowledgeName));
        if (content is null)
            throw new ArgumentNullException(nameof(content));

        var type = string.IsNullOrWhiteSpace(knowledgeType) ? "text" : knowledgeType;
        return await knowledgeCollection.UpdateAsync(knowledgeName, content, type, null, cancellationToken);
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
            var resourceName = $"{entryAssembly.GetName().Name}.{normalizedPath}";
            var stream = entryAssembly.GetManifestResourceStream(resourceName);
            
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
        }
        
        // If not found in entry assembly, search all loaded assemblies
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // Skip system assemblies
            var assemblyName = assembly.GetName().Name;
            if (assemblyName == null || assemblyName.StartsWith("System.") || assemblyName.StartsWith("Microsoft."))
                continue;
            
            var resourceName = $"{assemblyName}.{normalizedPath}";
            var stream = assembly.GetManifestResourceStream(resourceName);
            
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
        }
        
        // Resource not found - provide helpful error message
        var searchedAssemblies = new List<string>();
        if (entryAssembly != null)
            searchedAssemblies.Add($"{entryAssembly.GetName().Name} (entry assembly)");
        
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var assemblyName = assembly.GetName().Name;
            if (assemblyName != null && !assemblyName.StartsWith("System.") && !assemblyName.StartsWith("Microsoft."))
            {
                var resources = assembly.GetManifestResourceNames();
                if (resources.Length > 0)
                    searchedAssemblies.Add($"{assemblyName} ({resources.Length} resources)");
            }
        }
        
        throw new FileNotFoundException(
            $"Embedded resource with path '{resourcePath}' not found. " +
            $"Searched assemblies: {string.Join(", ", searchedAssemblies)}. " +
            $"Make sure the file is marked as an EmbeddedResource in the .csproj file.");
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
