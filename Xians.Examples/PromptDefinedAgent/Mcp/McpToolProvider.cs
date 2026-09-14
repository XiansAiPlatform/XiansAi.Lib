using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using PromptDefinedAgent.Configuration;
using Xians.Lib.Agents.Core;

namespace PromptDefinedAgent.Mcp;

internal static class McpToolProvider
{
    public static async Task<McpToolCollection> LoadAsync()
    {
        var result = new McpToolCollection();
        var rules = await XiansContext.CurrentAgent.Knowledge.GetAsync("Rules");
        if (string.IsNullOrWhiteSpace(rules?.Content)) return result;

        RulesConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<RulesConfig>(rules.Content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException exception)
        {
            Console.Error.WriteLine($"Ignoring invalid Rules JSON: {exception.Message}");
            return result;
        }

        foreach (var server in config?.McpServers.Where(server => server.Enabled) ?? [])
            await AddServerAsync(server, result);

        return result;
    }

    private static async Task AddServerAsync(McpServerConfig server, McpToolCollection result)
    {
        if (string.IsNullOrWhiteSpace(server.Name) ||
            !Uri.TryCreate(server.Url, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            Console.Error.WriteLine($"Skipping MCP server '{server.Name}': invalid HTTP URL.");
            return;
        }

        try
        {
            var client = await McpClient.CreateAsync(new HttpClientTransport(new()
            {
                Name = server.Name,
                Endpoint = endpoint,
                TransportMode = ParseTransport(server.Transport),
                AdditionalHeaders = await BuildAuthenticationHeadersAsync(server.Authentication)
            }));
            try
            {
                result.Add(client, await client.ListToolsAsync());
            }
            catch
            {
                await client.DisposeAsync();
                throw;
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Skipping MCP server '{server.Name}': {exception.Message}");
        }
    }

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
            throw new InvalidOperationException("MCP authentication requires a secret key name.");

        var secrets = XiansContext.CurrentAgent.Secrets;
        var secret = await secrets.TenantScope().AgentScope().ActivationScope(XiansContext.SafeIdPostfix).FetchByKeyAsync(key)
            ?? await secrets.TenantScope().AgentScope().FetchByKeyAsync(key)
            ?? await secrets.TenantScope().FetchByKeyAsync(key);
        return secret?.Value ?? throw new InvalidOperationException($"MCP secret '{key}' was not found.");
    }
}

internal sealed class McpToolCollection : IAsyncDisposable
{
    private readonly List<McpClient> _clients = [];
    public List<AITool> Tools { get; } = [];

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
