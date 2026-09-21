using System.Net.Http.Headers;
using System.Text;
using System.Diagnostics;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using PromptDefinedAgent.Configuration;
using Xians.Lib.Agents.Core;

namespace PromptDefinedAgent.Mcp;

internal static class McpToolProvider
{
    public static async Task<McpToolCollection> LoadAsync(RulesConfig config)
    {
        var result = new McpToolCollection();
        var loadId = Guid.NewGuid().ToString("N")[..8];
        var servers = config.McpServers ?? [];
        Console.WriteLine($"[MCP {loadId}] Loading {servers.Count} configured servers.");
        foreach (var server in servers)
        {
            if (server is null) { Console.Error.WriteLine($"[MCP {loadId}] Skipping null server configuration."); continue; }
            if (!server.Enabled) { Console.WriteLine($"[MCP {loadId}] Server {Label(server.Name)} is disabled."); continue; }
            await AddServerAsync(server, result, loadId);
        }

        Console.WriteLine($"[MCP {loadId}] Loaded {result.Tools.Count} tools from {result.ServerCount} servers.");
        if (result.Tools.Count == 0) Console.Error.WriteLine($"[MCP {loadId}] No MCP tools are available to the model.");

        return result;
    }

    private static async Task AddServerAsync(McpServerConfig server, McpToolCollection result, string loadId)
    {
        if (string.IsNullOrWhiteSpace(server.Name) ||
            !Uri.TryCreate(server.Url, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            Console.Error.WriteLine($"[MCP {loadId}] Skipping server {Label(server.Name)}: name missing or invalid HTTP URL.");
            return;
        }

        var timer = Stopwatch.StartNew();
        var stage = "authentication";
        try
        {
            Console.WriteLine($"[MCP {loadId}] Server {Label(server.Name)}: transport={Label(server.Transport)}, auth={Label(server.Authentication?.Type ?? "none")}.");
            var headers = await BuildAuthenticationHeadersAsync(server.Authentication);
            stage = "connection/initialization";
            Console.WriteLine($"[MCP {loadId}] Server {Label(server.Name)}: connecting.");
            var client = await McpClient.CreateAsync(new HttpClientTransport(new()
            {
                Name = server.Name,
                Endpoint = endpoint,
                TransportMode = ParseTransport(server.Transport),
                AdditionalHeaders = headers
            }));
            try
            {
                stage = "tool discovery";
                Console.WriteLine($"[MCP {loadId}] Server {Label(server.Name)}: connected; discovering tools.");
                var tools = await client.ListToolsAsync();
                IEnumerable<AITool> exposedTools = tools;
                if (server.Context?.Equals("xians", StringComparison.OrdinalIgnoreCase) == true)
                    exposedTools = tools.Select(XiansContextFunction.Bind).ToArray();
                result.Add(client, exposedTools);
                Console.WriteLine($"[MCP {loadId}] Server {Label(server.Name)}: loaded {tools.Count} tools in {timer.ElapsedMilliseconds}ms.");
            }
            catch
            {
                await client.DisposeAsync();
                throw;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[MCP {loadId}] Skipping server {Label(server.Name)}: {stage} failed after {timer.ElapsedMilliseconds}ms; {Failure(exception)}.");
        }
    }

    // JSON escaping prevents multiline log injection. Never log endpoints, headers, or exception messages.
    private static string Label(string value) => System.Text.Json.JsonSerializer.Serialize(value);

    private static string Failure(Exception exception) => exception switch
    {
        HttpRequestException http => $"HttpRequestException, HTTP status={http.StatusCode?.ToString() ?? "unavailable"}",
        _ => exception.GetType().Name + (exception.InnerException is null ? "" : $"; caused by {Failure(exception.InnerException)}")
    };

    private static HttpTransportMode ParseTransport(string transport) => transport.ToLowerInvariant() switch
    {
        "auto" => HttpTransportMode.AutoDetect,
        "streamablehttp" => HttpTransportMode.StreamableHttp,
        "sse" => HttpTransportMode.Sse,
        _ => throw new InvalidOperationException($"Unsupported MCP transport '{transport}'.")
    };

    private static async Task<Dictionary<string, string>?> BuildAuthenticationHeadersAsync(
        McpAuthenticationConfig? authentication)
    {
        if (authentication is null || authentication.Type.Equals("none", StringComparison.OrdinalIgnoreCase))
            return null;

        return authentication.Type.ToLowerInvariant() switch
        {
            "bearer" => new() { ["Authorization"] = $"Bearer {await GetSecretAsync(authentication.Secret)}" },
            "apikey" => new() { [authentication.Header ?? "X-API-Key"] = await GetSecretAsync(authentication.Secret) },
            "basic" => new()
            {
                ["Authorization"] = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{await GetSecretAsync(authentication.UsernameSecret)}:{await GetSecretAsync(authentication.PasswordSecret)}")))
                    .ToString()
            },
            _ => throw new InvalidOperationException($"Unsupported MCP authentication type '{authentication.Type}'.")
        };
    }

    private static async Task<string> GetSecretAsync(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            Console.Error.WriteLine("[MCP Authentication] Secret key name is missing from configuration.");
            throw new InvalidOperationException("MCP authentication requires a secret key name.");
        }

        var secrets = XiansContext.CurrentAgent.Secrets;
        var secret = await secrets.TenantScope().AgentScope().ActivationScope(XiansContext.SafeIdPostfix).FetchByKeyAsync(key)
            ?? await secrets.TenantScope().AgentScope().FetchByKeyAsync(key)
            ?? await secrets.TenantScope().FetchByKeyAsync(key);
        Console.WriteLine(secret is null
            ? "[MCP Authentication] Secret not found in activation, agent, or tenant vault."
            : "[MCP Authentication] Secret resolved from vault.");
        return secret?.Value ?? throw new InvalidOperationException($"MCP secret '{key}' was not found.");
    }
}

internal sealed class McpToolCollection : IAsyncDisposable
{
    private readonly List<McpClient> _clients = [];
    public List<AITool> Tools { get; } = [];
    public int ServerCount => _clients.Count;

    public void Add(McpClient client, IEnumerable<AITool> tools)
    {
        _clients.Add(client);
        Tools.AddRange(tools);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
            await client.DisposeAsync();
    }
}
