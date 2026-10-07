using ModelContextProtocol.Authentication;

namespace PromptDefinedAgent.Mcp;

internal sealed class OAuthConnection
{
    public required string ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public required string RedirectUri { get; init; }
    public string[]? Scopes { get; init; }
    public required TokenContainer Tokens { get; set; }
}
