using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using XiansAi.Http;
using XiansAi.Models;
using System.Text.RegularExpressions;

namespace XiansAi.Server;

public class InstructionLoader
{
    private readonly ILogger<InstructionLoader> _logger;

    public InstructionLoader()
    {
        _logger = Globals.LogFactory.CreateLogger<InstructionLoader>();
    }

    private async Task<Instruction> LoadFromLocal(string instructionName)
    {
        // first check if the local folder env variable is set
        var localFolder = Environment.GetEnvironmentVariable("LOCAL_INSTRUCTIONS_FOLDER");
        if (!string.IsNullOrEmpty(localFolder)) {
            // Get filename without extension
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(instructionName);
            
            // Search pattern includes both files with and without extensions
            var searchPattern = fileNameWithoutExt + ".*";
            var matchingFiles = Directory.GetFiles(localFolder, searchPattern)
                .Concat(Directory.GetFiles(localFolder, fileNameWithoutExt)) // Add files without extension
                .Distinct() // Remove duplicates in case a file without extension was found twice
                .ToList();

            if (matchingFiles.Count > 1)
            {
                var fileList = string.Join(", ", matchingFiles.Select(Path.GetFileName));
                _logger.LogError($"Multiple matching files found for '{fileNameWithoutExt}': {fileList}");
                throw new InvalidOperationException($"Multiple matching files found for '{fileNameWithoutExt}'. Found: {fileList}");
            }
            else if (matchingFiles.Count == 1)
            {
                var instructionPath = matchingFiles[0];
                return new Instruction { 
                    Content = await File.ReadAllTextAsync(instructionPath), 
                    Name = instructionName,
                    Id = null // Indicate that this is a local instruction from a file
                };
            }
            else 
            {
                _logger.LogError($"No instruction file found with name: '{fileNameWithoutExt}' in folder: '{localFolder}'");
                throw new FileNotFoundException($"No instruction file found with name: '{fileNameWithoutExt}'");
            }
        } 
        else {
            throw new InvalidOperationException($"LOCAL_INSTRUCTION_FOLDER environment variable is not set. Please set it to the path of the local instructions folder.");
        }
    }

    public async Task<Instruction?> Load(string instructionName)
    {

        if (!SecureApi.IsReady()) { 
            _logger.LogWarning("App server connection is not established, loading instruction locally");
            return await LoadFromLocal(instructionName);
        } else {
            return await LoadFromServer(instructionName);
        }
    }

    private async Task<Instruction?> LoadFromServer(string instructionName)
    {
        var url = BuildServerUrl(instructionName);
        
        try {
            var client = SecureApi.GetClient();
            var httpResult = await client.GetAsync(url);

            if (httpResult.StatusCode == HttpStatusCode.NotFound) {
                _logger.LogError($"Instruction not found on server: {instructionName}");
                return null;
            }
            
            if (httpResult.StatusCode != HttpStatusCode.OK) {
                _logger.LogError($"Failed to get instruction from server. Status code: {httpResult.StatusCode}");
                throw new InvalidOperationException($"Failed to get instruction from server: {httpResult.Content}");
            }

            return await ParseServerResponse(httpResult);
        }
        catch (Exception e) {
            _logger.LogError(e, $"Failed to load instruction from server: {instructionName}.");
            throw new InvalidOperationException($"Failed to load instruction from server: {instructionName}. error: {e.Message}");
        }
    }

    private string BuildServerUrl(string instructionNameOnly)
    {
        return "api/server/instructions/latest?name=" + UrlEncoder.Default.Encode(instructionNameOnly);
    }

    private async Task<Instruction> ParseServerResponse(HttpResponseMessage httpResult)
    {
        var response = await httpResult.Content.ReadAsStringAsync();

        try {
            var options = new JsonSerializerOptions { 
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var instruction = JsonSerializer.Deserialize<Instruction>(response, options);

            if (instruction?.Content == null || instruction.Name == null) {
                _logger.LogError($"Failed to deserialize instruction from server: {response}");
                throw new InvalidOperationException($"Failed to deserialize instruction from server: {response}");
            }
            return instruction;
        } 
        catch (Exception e) {
            _logger.LogError(e, $"Failed to deserialize instruction from server: {response}");
            throw new InvalidOperationException($"Failed to deserialize instruction from server: {response} {e.Message}");
        }
    }
}
