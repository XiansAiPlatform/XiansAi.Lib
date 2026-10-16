using System.Collections.Concurrent;
using System.Text.Json;
using ModelContextProtocol.Authentication;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Secrets;

namespace PromptDefinedAgent.Mcp;

internal sealed class OAuthConnectionResolver(XiansToolContext context)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SecretVaultScopeBuilder _secrets = XiansContext.CurrentAgent.Secrets
        .TenantScope(context.TenantId)
        .AgentScope(context.AgentName)
        .ActivationScope(context.ActivationName);
    private readonly ConcurrentDictionary<string, Lazy<Task<ClientOAuthOptions>>> _options = new();
    private readonly Lazy<Task<Dictionary<string, string>>> _secretIds = new(async () =>
        (await XiansContext.CurrentAgent.Secrets
            .TenantScope(context.TenantId)
            .AgentScope(context.AgentName)
            .ActivationScope(context.ActivationName)
            .ListAsync()).ToDictionary(secret => secret.Key, secret => secret.Id));

    public Task<ClientOAuthOptions> ResolveAsync(string? connectionKey)
    {
        if (string.IsNullOrWhiteSpace(connectionKey))
            throw new InvalidOperationException("OAuth authentication requires a connection key.");

        return _options.GetOrAdd(connectionKey, key => new(() => BuildAsync(key))).Value;
    }

    private async Task<ClientOAuthOptions> BuildAsync(string connectionKey)
    {
        var fetchTask = _secrets.FetchByKeyAsync(connectionKey);
        var idsTask = _secretIds.Value;
        await Task.WhenAll(fetchTask, idsTask);
        var stored = await fetchTask
            ?? throw new InvalidOperationException($"OAuth connection '{connectionKey}' was not found for this activation.");
        var connection = Deserialize(stored.Value, connectionKey);

        if (!Uri.TryCreate(connection.RedirectUri, UriKind.Absolute, out var redirectUri) ||
            !IsAllowedRedirectUri(redirectUri))
            throw new InvalidOperationException($"OAuth connection '{connectionKey}' has an invalid redirect URI.");

        if (!(await idsTask).TryGetValue(connectionKey, out var secretId))
            throw new InvalidOperationException($"OAuth connection '{connectionKey}' could not be resolved in this activation.");

        return new ClientOAuthOptions
        {
            ClientId = connection.ClientId,
            ClientSecret = connection.ClientSecret,
            RedirectUri = redirectUri,
            Scopes = connection.Scopes,
            TokenCache = new SecretVaultTokenCache(_secrets, secretId, connectionKey, connection),
            AuthorizationRedirectDelegate = (_, _, _) => throw new InvalidOperationException(
                $"OAuth connection '{connectionKey}' must be reconnected in Agent Studio.")
        };
    }

    private static OAuthConnection Deserialize(string value, string connectionKey)
    {
        try
        {
            return JsonSerializer.Deserialize<OAuthConnection>(value, JsonOptions)
                ?? throw new InvalidOperationException($"OAuth connection '{connectionKey}' is invalid.");
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException($"OAuth connection '{connectionKey}' failed to deserialize.", exception);
        }
    }

    private static bool IsAllowedRedirectUri(Uri redirectUri) =>
        redirectUri.Scheme == Uri.UriSchemeHttps ||
        (redirectUri.Scheme == Uri.UriSchemeHttp && redirectUri.IsLoopback);
}
