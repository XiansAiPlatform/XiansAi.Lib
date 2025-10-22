using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;

namespace XiansAi.Onboarding;

/// <summary>
/// Parses onboarding JSON and resolves file:// and embedded:// references 
/// by loading content from disk or embedded resources.
/// </summary>
public static class OnboardingParser
{
    private const string FileProtocol = "file://";
    private const string EmbeddedProtocol = "embedded://";
    
    /// <summary>
    /// Processes onboarding JSON and resolves all file:// and embedded:// references.
    /// </summary>
    /// <param name="onboardingJson">Raw onboarding JSON with file references</param>
    /// <param name="baseDirectory">Base directory for resolving relative file paths (optional)</param>
    /// <param name="callingAssembly">Assembly to search for embedded resources (optional)</param>
    /// <returns>Processed JSON with all file contents loaded</returns>
    /// <exception cref="ArgumentException">Thrown if onboardingJson is null or empty</exception>
    /// <exception cref="InvalidOperationException">Thrown if JSON parsing fails</exception>
    /// <exception cref="FileNotFoundException">Thrown if referenced file is not found</exception>
    /// <exception cref="IOException">Thrown if file reading fails</exception>
    public static string Parse(string onboardingJson, string? baseDirectory = null, Assembly? callingAssembly = null)
    {
        if (string.IsNullOrWhiteSpace(onboardingJson))
        {
            throw new ArgumentException("Onboarding JSON cannot be null or empty.", nameof(onboardingJson));
        }

        baseDirectory ??= FindProjectRoot();
        
        try
        {
            var jsonNode = JsonNode.Parse(onboardingJson);
            if (jsonNode == null)
            {
                throw new JsonException("Failed to parse onboarding JSON.");
            }

            ProcessNode(jsonNode, baseDirectory, callingAssembly);
            
            return jsonNode.ToJsonString(new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid onboarding JSON format: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Recursively processes JSON nodes to find and resolve file references.
    /// </summary>
    private static void ProcessNode(JsonNode? node, string baseDirectory, Assembly? callingAssembly)
    {
        if (node == null) return;

        switch (node)
        {
            case JsonObject obj:
                // Create a list to avoid modifying collection during iteration
                foreach (var property in obj.ToList())
                {
                    if (property.Value is JsonValue value && 
                        value.TryGetValue<string>(out var stringValue) && 
                        !string.IsNullOrEmpty(stringValue))
                    {
                        // Check if it's a file:// or embedded:// reference
                        if (stringValue.StartsWith(FileProtocol))
                        {
                            var filePath = stringValue.Substring(FileProtocol.Length);
                            var content = LoadFile(filePath, baseDirectory);
                            obj[property.Key] = JsonValue.Create(content);
                        }
                        else if (stringValue.StartsWith(EmbeddedProtocol) && callingAssembly != null)
                        {
                            var resourceName = stringValue.Substring(EmbeddedProtocol.Length);
                            var content = LoadEmbeddedResource(resourceName, callingAssembly);
                            obj[property.Key] = JsonValue.Create(content);
                        }
                    }
                    else
                    {
                        // Recursively process nested objects/arrays
                        ProcessNode(property.Value, baseDirectory, callingAssembly);
                    }
                }
                break;

            case JsonArray array:
                for (int i = 0; i < array.Count; i++)
                {
                    ProcessNode(array[i], baseDirectory, callingAssembly);
                }
                break;
        }
    }

    /// <summary>
    /// Loads a file from disk with multiple fallback locations.
    /// </summary>
    private static string LoadFile(string relativePath, string baseDirectory)
    {
        try
        {
            var fullPath = Path.Combine(baseDirectory, relativePath);
            
            if (File.Exists(fullPath))
            {
                return File.ReadAllText(fullPath);
            }

            // Try alternative paths
            var alternatives = new[]
            {
                Path.Combine(AppContext.BaseDirectory, relativePath),
                Path.Combine(Directory.GetCurrentDirectory(), relativePath),
                relativePath // Try as absolute path
            };

            foreach (var altPath in alternatives)
            {
                if (File.Exists(altPath))
                {
                    return File.ReadAllText(altPath);
                }
            }

            throw new FileNotFoundException(
                $"Could not find file: {relativePath}. " +
                $"Searched in {baseDirectory} and alternative locations.");
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            throw new IOException($"Error reading file '{relativePath}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads an embedded resource from an assembly.
    /// </summary>
    private static string LoadEmbeddedResource(string resourceName, Assembly assembly)
    {
        try
        {
            // Normalize path separators (handle both / and \)
            var normalizedName = resourceName.Replace('/', '\\');
            
            // Try multiple patterns to find the embedded resource
            var patterns = new[]
            {
                normalizedName,                                              // As-is (e.g., "knowledge-base\file.md")
                normalizedName.Replace('\\', '.'),                          // Dotted (e.g., "knowledge-base.file.md")
                $"{assembly.GetName().Name}.{normalizedName}",              // With assembly prefix
                $"{assembly.GetName().Name}.{normalizedName.Replace('\\', '.')}"  // Assembly prefix + dotted
            };

            Stream? stream = null;
            foreach (var pattern in patterns)
            {
                stream = assembly.GetManifestResourceStream(pattern);
                if (stream != null) break;
            }

            if (stream == null)
            {
                var availableResources = string.Join(", ", assembly.GetManifestResourceNames());
                throw new FileNotFoundException(
                    $"Embedded resource '{resourceName}' not found in assembly '{assembly.GetName().Name}'. " +
                    $"Tried patterns: {string.Join(", ", patterns)}. " +
                    $"Available resources: {availableResources}");
            }

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            throw new IOException($"Error reading embedded resource '{resourceName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Attempts to find the project root directory by looking for .csproj files.
    /// </summary>
    private static string FindProjectRoot()
    {
        var currentDir = AppContext.BaseDirectory;
        
        // Navigate up to find a .csproj file
        while (!string.IsNullOrEmpty(currentDir))
        {
            if (Directory.GetFiles(currentDir, "*.csproj").Any())
            {
                return currentDir;
            }
            
            var parent = Directory.GetParent(currentDir);
            if (parent == null) break;
            currentDir = parent.FullName;
        }
        
        // Fallback to AppContext.BaseDirectory
        return AppContext.BaseDirectory;
    }

    /// <summary>
    /// Validates that the onboarding JSON has the required structure.
    /// </summary>
    /// <param name="onboardingJson">The JSON to validate</param>
    /// <param name="errors">List of validation errors</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool Validate(string onboardingJson, out List<string> errors)
    {
        errors = new List<string>();
        
        try
        {
            var jsonNode = JsonNode.Parse(onboardingJson);
            if (jsonNode is not JsonObject root)
            {
                errors.Add("Root element must be a JSON object");
                return false;
            }

            // Check required fields (adjust based on your schema)
            var requiredFields = new[] { "display-name", "version", "workflow" };
            foreach (var field in requiredFields)
            {
                if (root[field] == null)
                {
                    errors.Add($"Missing required field: {field}");
                }
            }

            // Validate workflow is an array
            if (root["workflow"] is not JsonArray workflow)
            {
                errors.Add("'workflow' must be an array");
            }
            else if (workflow.Count == 0)
            {
                errors.Add("'workflow' array cannot be empty");
            }

            return errors.Count == 0;
        }
        catch (JsonException ex)
        {
            errors.Add($"Invalid JSON: {ex.Message}");
            return false;
        }
    }
}

