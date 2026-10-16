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
    private const int PersistAttempts = 3;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private TokenContainer? _tokens = connection.Tokens;

    public ValueTask<TokenContainer?> GetTokensAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Volatile.Read(ref _tokens));
    }

    public async ValueTask StoreTokensAsync(TokenContainer tokens, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            Volatile.Write(ref _tokens, tokens);
            connection.Tokens = tokens;
            for (var attempt = 1; attempt <= PersistAttempts; attempt++)
            {
                try
                {
                    await secrets.UpdateAsync(
                        secretId,
                        JsonSerializer.Serialize(connection),
                        cancellationToken: cancellationToken);
                    Console.WriteLine($"[MCP Authentication] OAuth connection {McpToolProvider.Label(connectionKey)} refreshed.");
                    return;
                }
                catch (Exception exception) when (attempt < PersistAttempts && exception is not OperationCanceledException)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine($"[MCP Authentication] OAuth connection {McpToolProvider.Label(connectionKey)} token persistence failed after {attempt} attempts; {McpToolProvider.Failure(exception)}.");
                    throw;
                }
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
