using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Server;

namespace XiansAi.Knowledge;

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
    private readonly ILogger<KnowledgeLoaderImpl> _logger;

    // Path to local instructions folder, configured via environment variable
    private readonly string? _localInstructionsFolder = Environment.GetEnvironmentVariable("LOCAL_INSTRUCTIONS_FOLDER");

    // API endpoint for retrieving instructions by name
    private const string URL = "api/agent/knowledge/latest?name=";

    /// <summary>
    /// Initializes a new instance of the InstructionLoader class.
    /// </summary>
    /// <param name="loggerFactory">Factory to create a logger instance</param>
    /// <param name="secureApi">Secure API client for server communication</param>
    /// <exception cref="ArgumentNullException">Thrown if secureApi is null</exception>
    public KnowledgeLoaderImpl()
    {
        _logger = Globals.LogFactory.CreateLogger<KnowledgeLoaderImpl>();
    }

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
        
        // Fall back to local loading if server connection isn't available
        if (!string.IsNullOrEmpty(_localInstructionsFolder))
        { 
            _logger.LogWarning("App server connection not ready, loading instruction locally from {_localInstructionsFolder}", _localInstructionsFolder);
            _logger.LogWarning("Loading instruction locally - {instructionName}", instructionName);
            return await LoadFromLocal(instructionName);
        }
        _logger.LogWarning("Loading instruction from server - {instructionName}", instructionName);
        return await LoadFromServer(instructionName);
    }

    /// <summary>
    /// Loads an instruction from the local filesystem.
    /// </summary>
    /// <param name="instructionName">The name of the instruction to load</param>
    /// <returns>The loaded instruction</returns>
    /// <exception cref="InvalidOperationException">Thrown if local instructions folder is not configured or multiple matching files found</exception>
    /// <exception cref="FileNotFoundException">Thrown if instruction file not found</exception>
    private async Task<Models.Knowledge?> LoadFromLocal(string instructionName)
    {
        if (string.IsNullOrEmpty(_localInstructionsFolder))
        {
            throw new InvalidOperationException("LOCAL_INSTRUCTIONS_FOLDER environment variable is not set. Please set it to the path of the local instructions folder.");
        }

        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(instructionName);
        var searchPattern = fileNameWithoutExt + ".*";
        
        // Search for files both with and without extensions to support different file formats
        var matchingFiles = Directory.GetFiles(_localInstructionsFolder, searchPattern)
            .Concat(Directory.GetFiles(_localInstructionsFolder, fileNameWithoutExt))
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
            _logger.LogError($"No instruction file found with name: '{fileNameWithoutExt}' in folder: '{_localInstructionsFolder}'");
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
    /// <exception cref="InvalidOperationException">Thrown if server request fails</exception>
    private async Task<Models.Knowledge?> LoadFromServer(string instructionName)
    {
        var url = BuildServerUrl(instructionName);
        
        try
        {
            var client = SecureApi.Instance.Client;
            var httpResult = await client.GetAsync(url);

            // Handle specific HTTP status codes with appropriate responses
            if (httpResult.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogError($"Instruction not found on server: {instructionName}");
                return null;
            }
            
            if (httpResult.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogError($"Failed to get instruction from server. Status code: {httpResult.StatusCode}");
                throw new InvalidOperationException($"Failed to get instruction from server: {await httpResult.Content.ReadAsStringAsync()}");
            }

            return await ParseServerResponse(httpResult);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to load instruction from server: {instructionName}");
            throw new InvalidOperationException($"Failed to load instruction from server: {instructionName}. Error: {e.Message}", e);
        }
    }

    /// <summary>
    /// Builds the server URL for retrieving an instruction.
    /// </summary>
    /// <param name="instructionName">The name of the instruction to load</param>
    /// <returns>The fully qualified URL for the instruction API endpoint</returns>
    private string BuildServerUrl(string instructionName)
    {
        return URL + UrlEncoder.Default.Encode(instructionName);
    }

    /// <summary>
    /// Parses the server response into an Instruction object.
    /// </summary>
    /// <param name="httpResult">The HTTP response from the server</param>
    /// <returns>The deserialized Instruction object</returns>
    /// <exception cref="InvalidOperationException">Thrown if deserialization fails or response is invalid</exception>
    private async Task<Models.Knowledge> ParseServerResponse(HttpResponseMessage httpResult)
    {
        var response = await httpResult.Content.ReadAsStringAsync();

        try
        {
            // Configure JSON deserialization options for server response
            var options = new JsonSerializerOptions
            { 
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var knowledge = JsonSerializer.Deserialize<Models.Knowledge>(response, options);

            // Validate that required properties are present
            if (knowledge?.Content == null || knowledge.Name == null)
            {
                _logger.LogError($"Failed to deserialize instruction from server: {response}");
                throw new InvalidOperationException($"Failed to deserialize instruction from server: {response}");
            }
            
            return knowledge;
        } 
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to deserialize instruction from server: {response}");
            throw new InvalidOperationException($"Failed to deserialize instruction from server: {response}. Error: {e.Message}", e);
        }
    }
}
