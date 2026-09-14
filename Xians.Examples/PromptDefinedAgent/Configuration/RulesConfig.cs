namespace PromptDefinedAgent.Configuration;

internal sealed class RulesConfig
{
    public List<McpServerConfig> McpServers { get; init; } = [];
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
