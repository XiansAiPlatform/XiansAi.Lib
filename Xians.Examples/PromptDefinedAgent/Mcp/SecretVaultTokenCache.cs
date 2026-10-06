using System.Text.Json;
using ModelContextProtocol.Authentication;
using Xians.Lib.Agents.Secrets;

namespace PromptDefinedAgent.Mcp;

internal sealed class SecretVaultTokenCache(
    SecretVaultScopeBuilder secrets,
    string connectionKey,
    OAuthConnection connection) : ITokenCache
{
    public ValueTask<TokenContainer?> GetTokensAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<TokenContainer?>(connection.Tokens);

    public async ValueTask StoreTokensAsync(TokenContainer tokens, CancellationToken cancellationToken = default)
    {
        connection.Tokens = tokens;
        await secrets.UpdateAsync(
            connection.SecretId,
            JsonSerializer.Serialize(connection),
            cancellationToken: cancellationToken);
        Console.WriteLine($"[MCP Authentication] OAuth connection {McpToolProvider.Label(connectionKey)} refreshed.");
    }
}
