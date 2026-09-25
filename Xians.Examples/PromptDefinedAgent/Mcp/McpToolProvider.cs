using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
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
        var enabledServers = EnabledServers(config, loadId);
        Console.WriteLine($"[MCP {loadId}] Loading {enabledServers.Count} enabled servers.");

        var loadedServers = await Task.WhenAll(enabledServers.Select(server => ConnectAsync(server, loadId)));
        foreach (var server in loadedServers)
            if (server is not null) result.Add(server);

        Console.WriteLine($"[MCP {loadId}] Loaded {result.ToolCount} tools from {result.ServerCount} servers.");
        if (result.ToolCount == 0) Console.Error.WriteLine($"[MCP {loadId}] No MCP tools are available to the model.");
        return result;
    }

    private static List<McpServerConfig> EnabledServers(RulesConfig config, string loadId)
    {
        var result = new List<McpServerConfig>();
        foreach (var server in config.McpServers ?? [])
        {
            if (server is null)
            {
                Console.Error.WriteLine($"[MCP {loadId}] Skipping null server configuration.");
                continue;
            }
            if (!server.Enabled)
            {
                Console.WriteLine($"[MCP {loadId}] Server {Label(server.Name)} is disabled.");
                continue;
            }
            result.Add(server);
        }
        return result;
    }

    private static async Task<LoadedMcpServer?> ConnectAsync(McpServerConfig server, string loadId)
    {
        if (string.IsNullOrWhiteSpace(server.Name) ||
            !Uri.TryCreate(server.Url, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            Console.Error.WriteLine($"[MCP {loadId}] Skipping server {Label(server.Name)}: name missing or invalid HTTP URL.");
            return null;
        }

        var timer = Stopwatch.StartNew();
        var stage = "authentication";
        McpClient? client = null;
        try
        {
            Console.WriteLine($"[MCP {loadId}] Server {Label(server.Name)}: transport={Label(server.Transport)}, auth={Label(server.Authentication?.Type ?? "none")}.");
            var headers = await BuildAuthenticationHeadersAsync(server.Authentication);
            stage = "connection/initialization";
            Console.WriteLine($"[MCP {loadId}] Server {Label(server.Name)}: connecting.");
            client = await McpClient.CreateAsync(new HttpClientTransport(new()
            {
                Name = server.Name,
                Endpoint = endpoint,
                TransportMode = ParseTransport(server.Transport),
                AdditionalHeaders = headers
            }));
            stage = "tool discovery";
            Console.WriteLine($"[MCP {loadId}] Server {Label(server.Name)}: connected; discovering tools.");
            var tools = await client.ListToolsAsync();
            Console.WriteLine($"[MCP {loadId}] Server {Label(server.Name)}: loaded {tools.Count} tools in {timer.ElapsedMilliseconds}ms.");
            return new LoadedMcpServer(client, tools, server.Context?.Equals("xians", StringComparison.OrdinalIgnoreCase) == true,
                server.Name, loadId);
        }
        catch (OperationCanceledException)
        {
            if (client is not null) await client.DisposeAsync();
            throw;
        }
        catch (Exception exception)
        {
            if (client is not null) await client.DisposeAsync();
            Console.Error.WriteLine($"[MCP {loadId}] Skipping server {Label(server.Name)}: {stage} failed after {timer.ElapsedMilliseconds}ms; {Failure(exception)}.");
            return null;
        }
    }

    internal static string Label(string value) => System.Text.Json.JsonSerializer.Serialize(value);

    internal static string Failure(Exception exception) => exception switch
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

internal sealed record LoadedMcpServer(
    McpClient Client,
    IList<McpClientTool> Tools,
    bool UsesXiansContext,
    string Name,
    string LoadId);

internal sealed class McpToolCollection : IAsyncDisposable
{
    private readonly List<LoadedMcpServer> _servers = [];
    public int ServerCount => _servers.Count;
    public int ToolCount => _servers.Sum(server => server.Tools.Count);

    public void Add(LoadedMcpServer server) => _servers.Add(server);

    public IReadOnlyList<AITool> GetTools(XiansToolContext context)
    {
        var result = new List<AITool>();
        foreach (var server in _servers)
        {
            foreach (var tool in server.Tools)
            {
                if (!server.UsesXiansContext)
                {
                    result.Add(tool);
                    continue;
                }
                try
                {
                    result.Add(XiansContextFunction.Bind(tool, context));
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine($"[MCP {server.LoadId}] Server {McpToolProvider.Label(server.Name)}: skipping tool {McpToolProvider.Label(tool.Name)} because context binding failed; {McpToolProvider.Failure(exception)}.");
                }
            }
        }
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var server in _servers)
            await server.Client.DisposeAsync();
    }
}
