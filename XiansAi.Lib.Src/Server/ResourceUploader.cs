using Agentri.Knowledge;
using Agentri.Logging;

namespace Agentri.Server;

/// <summary>
/// Defines a service for uploading flow definitions to the server.
/// </summary>
public interface IResourceUploader
{
    /// <summary>
    /// Uploads all supported resources from the local folder to the server.
    /// </summary>
    /// <returns>True if upload was attempted and succeeded, false otherwise.</returns>
    Task<bool> UploadResource();
}

public class ResourceUploader : IResourceUploader
{
    private readonly Logger<ResourceUploader> _logger = Logger<ResourceUploader>.For();
    private readonly bool _uploadResource;
    private readonly string? _localFolder;

    private static readonly string[] SupportedExtensions = [".md", ".txt", ".json"];

    public ResourceUploader(bool uploadResource)
    {
        _uploadResource = uploadResource;
        _localFolder = Environment.GetEnvironmentVariable("LOCAL_KNOWLEDGE_FOLDER");
    }

    /// <summary>
    /// Uploads a resource to the server.
    /// </summary>
    /// <param name="resourceName">The name of the resource to upload</param>
    /// <param name="resourceType">The type of the resource to upload</param>
    /// <param name="resourceContent">The content of the resource to upload</param>
    /// <returns>A task representing the upload operation</returns>
    public async Task<bool> UploadResource()
    {
        if (!_uploadResource)
        {
            _logger.LogInformation("UPLOAD_RESOURCES is not set. Skipping resource upload.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(_localFolder) || !Directory.Exists(_localFolder))
        {
            _logger.LogWarning($"Knowledge folder not found or invalid: {_localFolder}");
            return false;
        }

        var files = SupportedExtensions
            .SelectMany(ext => Directory.GetFiles(_localFolder, $"*{ext}"))
            .ToArray();

        if (files.Length == 0)
        {
            _logger.LogInformation($"No supported files (.md, .txt, .json) found in {_localFolder}");
            return true; // Operation was valid, just no files to process
        }

        foreach (var filePath in files)
        {
            var resourceName = Path.GetFileNameWithoutExtension(filePath);
            var resourceType = GetResourceTypeFromExtension(Path.GetExtension(filePath));
            string resourceContent;

            try
            {
                resourceContent = await File.ReadAllTextAsync(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to read file '{filePath}': {ex.Message}");
                continue;
            }

            _logger.LogInformation($"Uploading knowledge: {resourceName} ({resourceType})");

            try
            {
                bool result = await KnowledgeHub.Update(resourceName, resourceType, resourceContent);
                _logger.LogInformation($"Upload result for '{resourceName}': {result}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating knowledge '{resourceName}': {ex.Message}");
            }
        }

        return true;
    }

    private static string GetResourceTypeFromExtension(string extension)
    {
        return extension.ToLower() switch
        {
            ".md" => "markdown",
            ".txt" => "text",
            ".json" => "json",
            _ => "unknown"
        };
    }
}
