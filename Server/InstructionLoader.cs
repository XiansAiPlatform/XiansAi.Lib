using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using XiansAi.Http;
using XiansAi.Models;

namespace XiansAi.Server;

public class InstructionLoader
{
    private readonly ILogger<InstructionLoader> _logger;

    public InstructionLoader()
    {
        _logger = Globals.LogFactory.CreateLogger<InstructionLoader>();
    }

    public async Task<Instruction> LoadInstruction(string instructionName)
    {

        // Check the environment variable for the instruction path
        var instructionPath = Environment.GetEnvironmentVariable(instructionName);

        // If the environment variable is not set, try to load from the server
        if (instructionPath == null) {
            var fromServer = await LoadFromServer(instructionName);
            if (fromServer != null) {
                return fromServer;
            } else {
                _logger.LogError($"Failed to load instruction from server: {instructionName}. Instruction does not exist.");
                throw new InvalidOperationException($"Failed to load instruction from server: {instructionName}. Instruction does not exist.");
            }
        } else {
            // If the environment variable is set, load from the file
            if (!File.Exists(instructionPath)) {
                _logger.LogError($"Instruction file does not exist: {instructionPath}");
                throw new InvalidOperationException($"Instruction file does not exist: {instructionPath}");
            }
            var instruction = new Instruction {
                Content = File.ReadAllText(instructionPath),
                Name = instructionName,
            };
            return instruction;
        }
        
    }

    private async Task<Instruction?> LoadFromServer(string instructionName)
    {
        if (!SecureApi.IsReady()) { 
            _logger.LogError("SecureApi is not initialized");
            throw new InvalidOperationException("SecureApi is not initialized");
        }

        var url = BuildServerUrl(instructionName);
        
        try {
            var client = SecureApi.GetClient();
            var httpResult = await client.GetAsync(url);

            if (httpResult.StatusCode == HttpStatusCode.NotFound) {
                return null;
            }
            
            if (httpResult.StatusCode != HttpStatusCode.OK) {
                _logger.LogError($"Failed to get instruction from server: {httpResult.StatusCode}");
                throw new InvalidOperationException($"Failed to get instruction from server: {httpResult.StatusCode}");
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
            var json = JsonSerializer.Deserialize<Dictionary<string, string>>(response);

            if (json == null || !json.ContainsKey("content")) {
                _logger.LogError($"Failed to deserialize instruction from server: {response}");
                throw new InvalidOperationException($"Failed to deserialize instruction from server: {response}");
            }
            return new Instruction {
                Id = json["id"],
                Name = json["name"],
                Version = json["version"],
                Type = json["type"],
                CreatedAt = DateTime.Parse(json["createdAt"]),
                Content = json["content"]
            };
        } 
        catch (Exception e) {
            _logger.LogError(e, $"Failed to deserialize instruction from server: {response}");
            throw new InvalidOperationException($"Failed to deserialize instruction from server: {response} {e.Message}");
        }
    }
}
