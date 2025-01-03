using System.Net;
using System.Text.Json;
using System.Web;

public class InstructionLoader
{
    private readonly string[] _instructions;

    public InstructionLoader(string[] instructions)
    {
        _instructions = instructions;
    }

    public async Task<string> LoadInstruction(int index = 0)
    {
        if (_instructions == null || index >= _instructions.Length) {
            throw new InvalidOperationException("Instructions are not set or index is out of range");
        }
        var instructionName = _instructions[index];

        if (Globals.XiansAIConfig?.PriorityToServer == true) {
            var fromServer = await LoadFromServer(instructionName);
            if (fromServer != null) {
                return fromServer;
            }
            Console.WriteLine($"Failed to load instruction from server: {instructionName}. response: {fromServer}");
        }
        
        return File.ReadAllText(instructionName);
    }

    private async Task<string?> LoadFromServer(string instructionName)
    {
        if (Globals.XiansAIConfig?.CertificatePath == null || 
            Globals.XiansAIConfig.CertificatePassword == null) {    
            throw new InvalidOperationException("CertificatePath and CertificatePassword are required for XiansAI Server");
        }

        var instructionNameOnly = Path.GetFileNameWithoutExtension(Path.GetFileName(instructionName));
        var url = BuildServerUrl(instructionNameOnly);
        
        try {
            var api = new SecureApi(Globals.XiansAIConfig.CertificatePath, Globals.XiansAIConfig.CertificatePassword);
            var client = api.GetClient();
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
            Console.WriteLine($"Failed to load instruction from server: {instructionNameOnly}. error: {e.Message}");
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

    private async Task<string> ParseServerResponse(HttpResponseMessage httpResult)
    {
        var response = await httpResult.Content.ReadAsStringAsync();

        try {
            var json = JsonSerializer.Deserialize<Dictionary<string, string>>(response);

            if (json == null || !json.ContainsKey("content")) {
                throw new InvalidOperationException($"Failed to deserialize instruction from server: {response}");
            }
            return json["content"];
        } 
        catch (Exception e) {
            throw new InvalidOperationException($"Failed to deserialize instruction from server: {response} {e.Message}");
        }
    }
}
