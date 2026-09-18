using System.Text.Json;
using Xians.Lib.Agents.Core;

namespace PromptDefinedAgent.Configuration;

internal sealed class RulesConfig
{
    public List<McpServerConfig> McpServers { get; init; } = [];

    public static async Task<RulesConfig> LoadAsync()
    {
        var rules = await XiansContext.CurrentAgent.Knowledge.GetAsync("Rules");
        if (string.IsNullOrWhiteSpace(rules?.Content)) return new();

        try
        {
            return JsonSerializer.Deserialize<RulesConfig>(rules.Content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new();
        }
        catch (JsonException exception)
        {
            Console.Error.WriteLine($"Ignoring invalid Rules JSON: {exception.Message}");
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
