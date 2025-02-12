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

    private async Task<Instruction?> LoadFromLocal(string instructionName)
    {
        var envVarName = instructionName.Replace(".", "_").Replace(" ", "_").ToUpper();
        _logger.LogWarning($"Loading instruction from local file. File path taken from environment variable '{envVarName}'");

        var instructionPath = Environment.GetEnvironmentVariable(envVarName);
        if (instructionPath == null) {
            _logger.LogError($"Instruction file does not exist: '{instructionPath}'. Failed to load instruction from local file. Please check the environment variable '{envVarName}'.");
            return null;
        }
        return new Instruction { 
            Content = await File.ReadAllTextAsync(instructionPath), 
            Name = instructionName,
            Id = null // Indicate that this is a local instruction from a file
        };
    }

    public async Task<Instruction?> Load(string instructionName)
    {

        if (!(SecureApi.IsReady())) { 
            _logger.LogWarning("App server secure connection is not initialized");
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
