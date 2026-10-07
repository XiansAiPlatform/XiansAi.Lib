using System.Text.Json;
using ModelContextProtocol.Authentication;
using Xians.Lib.Agents.Secrets;

namespace PromptDefinedAgent.Mcp;

internal sealed class SecretVaultTokenCache(
    SecretVaultScopeBuilder secrets,
    string secretId,
    string connectionKey,
    OAuthConnection connection) : ITokenCache
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public ValueTask<TokenContainer?> GetTokensAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<TokenContainer?>(connection.Tokens);

    public async ValueTask StoreTokensAsync(TokenContainer tokens, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            connection.Tokens = tokens;
            await secrets.UpdateAsync(
                secretId,
                JsonSerializer.Serialize(connection),
                cancellationToken: cancellationToken);
            Console.WriteLine($"[MCP Authentication] OAuth connection {McpToolProvider.Label(connectionKey)} refreshed.");
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
