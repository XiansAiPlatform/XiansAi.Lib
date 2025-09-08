using Microsoft.Extensions.Logging;
using Server;

namespace XiansAi.Knowledge;

public class KnowledgeSync
{
    private readonly ILogger<KnowledgeSync> _logger = Globals.LogFactory.CreateLogger<KnowledgeSync>();
    private readonly KnowledgeService _knowledgeService = new KnowledgeService();

    private readonly string _agent;

    public KnowledgeSync(string agent)
    {
        _agent = agent;
    }

    /// <summary>
    /// Synchronizes all knowledge files from local directory to server.
    /// </summary>
    /// <returns>A task representing the sync operation.</returns>
    public async Task SyncAllKnowledgeToServerAsync()
    {
        string? knowledgeFolder = "knowledge_base";

        if (!Directory.Exists(knowledgeFolder))
        {
            // Check if 'knowledge' folder exists in current directory as fallback
            string defaultKnowledgeFolder = Path.Combine(Directory.GetCurrentDirectory(), knowledgeFolder);
            if (!Directory.Exists(defaultKnowledgeFolder))
            {
                _logger.LogInformation($"Folder '{knowledgeFolder}' not found in current directory");
                return;
            }
        }

        var knowledgeFiles = Directory.GetFiles(knowledgeFolder);
        _logger.LogInformation($"Found {knowledgeFiles.Length} knowledge files to check");

        foreach (var filePath in knowledgeFiles)
        {
            await SyncKnowledgeFileAsync(filePath);
        }

    }

    /// <summary>
    /// Synchronizes a single knowledge file with the server.
    /// </summary>
    /// <param name="filePath">Full path to the knowledge file.</param>
    /// <returns>A task representing the sync operation.</returns>
    private async Task SyncKnowledgeFileAsync(string filePath)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            var fileExtension = Path.GetExtension(filePath);
            var fileInfo = new FileInfo(filePath);

            _logger.LogInformation($"Checking knowledge file: {fileName}");

            // Check if knowledge exists on server and is up-to-date
            var shouldUpload = await CheckIfKnowledgeOnServerIsOutdatedAsync(fileNameWithoutExt, fileInfo.LastWriteTime);

            if (shouldUpload)
            {
                _logger.LogInformation($"Uploading knowledge file: {fileName}");

                // Prepare knowledge for upload
                var knowledge = new Models.Knowledge
                {
                    Name = fileNameWithoutExt,
                    Content = await File.ReadAllTextAsync(filePath),
                    Type = DetermineKnowledgeType(fileExtension),
                    Agent = _agent
                };

                _logger.LogInformation($"Uploading knowledge to server: {knowledge}");

                // Upload knowledge to server
                await _knowledgeService.UploadKnowledgeToServer(knowledge);
            }
            else
            {
                _logger.LogWarning($"Server version of {fileNameWithoutExt} is more up-to-date. Skipping upload.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error syncing knowledge file: {filePath}", ex);
        }
    }

    /// <summary>
    /// Checks if knowledge exists on server and if local version is newer.
    /// </summary>
    /// <param name="knowledgeName">Name of the knowledge file.</param>
    /// <param name="lastModified">Last modified timestamp of the local file.</param>
    /// <returns>True if knowledge should be uploaded, false otherwise.</returns>
    private async Task<bool> CheckIfKnowledgeOnServerIsOutdatedAsync(string knowledgeName, DateTime lastModified)
    {
        if (!SecureApi.IsReady)
        {
            throw new InvalidOperationException("App server connection not ready, cannot check knowledge on server");
        }

        try
        {
            var knowledge = await _knowledgeService.GetKnowledgeFromServer(knowledgeName, _agent);

            // If knowledge doesn't exist on server, we should upload it
            if (knowledge == null)
            {
                return true;
            }

            // Compare timestamps - if local file is newer than server version, return true
            return lastModified > knowledge.CreatedAt;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error checking knowledge on server: {knowledgeName}", ex);
            // Default to false in case of errors to prevent unwanted overwrites
            return false;
        }
    }

    /// <summary>
    /// Determines knowledge type based on file extension.
    /// </summary>
    /// <param name="fileExtension">File extension including the dot.</param>
    /// <returns>Knowledge type string.</returns>
    private string DetermineKnowledgeType(string fileExtension)
    {
        return fileExtension.ToLowerInvariant() switch
        {
            ".json" => "json",
            ".md" => "markdown",
            ".txt" => "text",
            _ => throw new ArgumentException($"Unsupported knowledge file extension: {fileExtension}")
        };
    }
}