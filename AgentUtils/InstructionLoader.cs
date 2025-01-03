using System.Net;
using System.Text.Json;
using System.Web;
using DotNetEnv;

public class InstructionLoader
{
    private readonly string[] _instructions;

    public InstructionLoader(string[] instructions)
    {
        _instructions = instructions;
    }

    public async Task<Instruction> LoadInstruction(int index = 0)
    {
        if (_instructions == null || index >= _instructions.Length) {
            throw new InvalidOperationException("Instructions are not set or index is out of range");
        }
        var instructionName = _instructions[index];

        // Check the environment variable for the instruction path
        var instructionPath = Env.GetString(instructionName);

        // If the environment variable is not set, try to load from the server
        if (instructionPath == null) {
            var fromServer = await LoadFromServer(instructionName);
            if (fromServer != null) {
                return fromServer;
            } else {
                throw new InvalidOperationException($"Failed to load instruction from server: {instructionName}");
            }
        } else {
            // If the environment variable is set, load from the file
            if (!File.Exists(instructionPath)) {
                throw new InvalidOperationException($"Instruction file does not exist: {instructionPath}");
            }
            return new Instruction {
                Content = File.ReadAllText(instructionPath),
                Name = instructionName,
            };
        }
    }

    private async Task<Instruction?> LoadFromServer(string instructionName)
    {
        if (Globals.XiansAIConfig?.CertificatePath == null || 
            Globals.XiansAIConfig.CertificatePassword == null ||    
            Globals.XiansAIConfig.ServerUrl == null) {    
            throw new InvalidOperationException("CertificatePath, CertificatePassword and ServerUrl are required for XiansAI Server");
        }

        var url = BuildServerUrl(instructionName);
        
        try {
            var client = SecureApi.GetClient();
            var httpResult = await client.GetAsync(url);

            if (httpResult.StatusCode == HttpStatusCode.NotFound) {
                return null;
            }
            
            if (httpResult.StatusCode != HttpStatusCode.OK) {
                throw new InvalidOperationException($"Failed to get instruction from server: {httpResult.StatusCode}");
            }

            return await ParseServerResponse(httpResult);
        }
        catch (Exception e) {
            Console.WriteLine($"Failed to load instruction from server: {instructionName}. error: {e.Message}");
            Console.WriteLine($"Server Config: {Globals.XiansAIConfig}");
            throw new InvalidOperationException($"Failed to load instruction from server: {instructionName}. error: {e.Message}");
        }
    }

    private string BuildServerUrl(string instructionNameOnly)
    {
        if (Globals.XiansAIConfig?.ServerUrl == null) {
            throw new InvalidOperationException("ServerUrl is required for XiansAI Server");
        }

        var builder = new UriBuilder(new Uri(Globals.XiansAIConfig!.ServerUrl));
        builder.Path += "api/server/instructions/latest";
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["name"] = instructionNameOnly;
        builder.Query = query.ToString();

        return builder.Uri.ToString();
    }

    private async Task<Instruction> ParseServerResponse(HttpResponseMessage httpResult)
    {
        var response = await httpResult.Content.ReadAsStringAsync();

        try {
            var json = JsonSerializer.Deserialize<Dictionary<string, string>>(response);

            if (json == null || !json.ContainsKey("content")) {
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
            throw new InvalidOperationException($"Failed to deserialize instruction from server: {response} {e.Message}");
        }
    }
}
