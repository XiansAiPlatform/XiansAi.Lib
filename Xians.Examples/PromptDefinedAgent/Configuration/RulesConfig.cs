using System.Text.Json;
using Xians.Lib.Agents.Core;

namespace PromptDefinedAgent.Configuration;

internal sealed class RulesConfig
{
    public List<McpServerConfig> McpServers { get; init; } = [];

    public static async Task<RulesConfig> LoadAsync()
    {
        var rules = await XiansContext.CurrentAgent.Knowledge.GetAsync("Rules");
        Console.WriteLine($"[MCP Rules] Resolving Rules for activation {JsonSerializer.Serialize(XiansContext.SafeIdPostfix)}.");
        if (string.IsNullOrWhiteSpace(rules?.Content))
        {
            Console.Error.WriteLine("[MCP Rules] Rules missing or empty; no MCP servers configured.");
            return new();
        }

        try
        {
            return JsonSerializer.Deserialize<RulesConfig>(rules.Content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new();
        }
        catch (JsonException exception)
        {
            Console.Error.WriteLine($"[MCP Rules] Invalid JSON at line {exception.LineNumber}, byte {exception.BytePositionInLine}; no MCP servers configured.");
            return new();
        }
    }
}

internal sealed class McpServerConfig
{
    public string Name { get; init; } = "";
    public string Url { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public string Transport { get; init; } = "auto";
    public string? Context { get; init; }
    public McpAuthenticationConfig? Authentication { get; init; }
}

internal sealed class McpAuthenticationConfig
{
    public string Type { get; init; } = "none";
    public string? Secret { get; init; }
    public string? Header { get; init; }
    public string? UsernameSecret { get; init; }
    public string? PasswordSecret { get; init; }
}
