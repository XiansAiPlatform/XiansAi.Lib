using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common.Caching;

namespace Xians.Lib.Agents.Knowledge.Providers;

/// <summary>
/// Factory for creating knowledge providers based on configuration.
/// Chooses between server (HTTP) and local (embedded resources) providers.
/// </summary>
internal static class KnowledgeProviderFactory
{
    /// <summary>
    /// Creates the appropriate knowledge provider based on the options.
    /// </summary>
    /// <param name="options">Platform options that determine the mode.</param>
    /// <param name="httpClient">HTTP client for server mode (can be null in local mode).</param>
    /// <param name="cacheService">Optional cache service for server mode.</param>
    /// <param name="logger">Logger for the provider.</param>
    /// <returns>An IKnowledgeProvider instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when httpClient is null in server mode.</exception>
    public static IKnowledgeProvider Create(
        XiansOptions options,
        HttpClient? httpClient,
        CacheService? cacheService,
        ILogger logger)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (logger == null)
            throw new ArgumentNullException(nameof(logger));

        if (options.LocalMode)
        {
            logger.LogInformation(
                "[LocalMode] Using LocalKnowledgeProvider with {Count} assemblies",
                options.LocalModeAssemblies?.Length ?? 0);
            
            return new LocalKnowledgeProvider(options.LocalModeAssemblies, logger);
        }

        if (httpClient == null)
        {
            throw new InvalidOperationException(
                "HttpClient is required when not in LocalMode. " +
                "Either enable LocalMode or provide an HttpClient.");
        }

        logger.LogDebug("Using ServerKnowledgeProvider for HTTP-based knowledge operations");
        return new ServerKnowledgeProvider(httpClient, cacheService, logger);
    }
}
