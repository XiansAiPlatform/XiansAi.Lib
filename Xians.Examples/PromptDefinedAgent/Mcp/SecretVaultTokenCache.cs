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

    public async ValueTask<TokenContainer?> GetTokensAsync(CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            return connection.Tokens;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async ValueTask StoreTokensAsync(TokenContainer tokens, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var previousTokens = connection.Tokens;
            connection.Tokens = tokens;
            try
            {
                await secrets.UpdateAsync(
                    secretId,
                    JsonSerializer.Serialize(connection),
                    cancellationToken: cancellationToken);
            }
            catch
            {
                connection.Tokens = previousTokens;
                throw;
            }
            Console.WriteLine($"[MCP Authentication] OAuth connection {McpToolProvider.Label(connectionKey)} refreshed.");
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
