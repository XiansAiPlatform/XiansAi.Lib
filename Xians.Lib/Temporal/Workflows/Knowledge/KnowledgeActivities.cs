using Temporalio.Activities;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Knowledge.Models;
using Xians.Lib.Temporal.Workflows.Models;
using Xians.Lib.Agents.Knowledge;
using Xians.Lib.Common.Caching;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Temporal.Workflows.Knowledge.Models;

namespace Xians.Lib.Temporal.Workflows.Knowledge;

/// <summary>
/// Activities for managing knowledge in the Xians platform.
/// Activities can perform non-deterministic operations like HTTP calls.
/// Delegates to shared KnowledgeService to avoid code duplication.
/// </summary>
public class KnowledgeActivities
{
    /// <summary>
    /// Static singletons for services - initialized once at startup, shared by all activity instances.
    /// This pattern is necessary because Temporal may create activity instances in different contexts
    /// where constructor injection doesn't preserve state.
    /// </summary>
    private static HttpClient? _staticHttpClient;
    private static Xians.Lib.Common.Caching.CacheService? _staticCacheService;
    private static readonly object _initLock = new();

    /// <summary>
    /// Initializes KnowledgeActivities with required dependencies.
    /// The first instance to be constructed will set the static services for all subsequent instances.
    /// </summary>
    public KnowledgeActivities(HttpClient? httpClient, Xians.Lib.Common.Caching.CacheService? cacheService = null)
    {
        // Initialize static services on first construction (thread-safe double-check locking)
        if (httpClient != null && _staticHttpClient == null)
        {
            lock (_initLock)
            {
                if (_staticHttpClient == null)
                {
                    _staticHttpClient = httpClient;
                    _staticCacheService = cacheService;
                }
            }
        }
    }
    
    /// <summary>
    /// Parameterless constructor for Temporal's activity instantiation.
    /// </summary>
    public KnowledgeActivities() : this(null, null)
    {
    }
    
    /// <summary>
    /// Gets the static cache service for use by other workflow components.
    /// </summary>
    public static Xians.Lib.Common.Caching.CacheService? GetStaticCacheService()
    {
        return _staticCacheService;
    }

    /// <summary>
    /// Clears static services. Intended for testing purposes only.
    /// </summary>
    internal static void ClearStaticServicesForTests()
    {
        lock (_initLock)
        {
            _staticHttpClient = null;
            _staticCacheService = null;
        }
    }

    /// <summary>
    /// Retrieves knowledge by name from the server.
    /// Delegates to shared KnowledgeService.
    /// </summary>
    [Activity]
    public async Task<Xians.Lib.Agents.Knowledge.Models.Knowledge?> GetKnowledgeAsync(GetKnowledgeRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "GetKnowledge activity started: Name={Name}, Agent={Agent}, Tenant={Tenant}, ActivationName={ActivationName}",
            request.KnowledgeName,
            request.AgentName,
            request.TenantId,
            request.ActivationName);

        try
        {
            var service = CreateKnowledgeService();
            return await service.GetAsync(
                request.KnowledgeName,
                request.AgentName,
                request.TenantId,
                request.ActivationName);
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error fetching knowledge: Name={Name}",
                request.KnowledgeName);
            throw;
        }
    }
    
    /// <summary>
    /// Retrieves system-scoped knowledge by name from the server.
    /// Delegates to shared KnowledgeService.
    /// </summary>
    [Activity]
    public async Task<Xians.Lib.Agents.Knowledge.Models.Knowledge?> GetSystemKnowledgeAsync(GetKnowledgeRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "GetSystemKnowledge activity started: Name={Name}, Agent={Agent}, ActivationName={ActivationName}",
            request.KnowledgeName,
            request.AgentName,
            request.ActivationName);

        try
        {
            var service = CreateKnowledgeService();
            return await service.GetSystemAsync(
                request.KnowledgeName,
                request.AgentName,
                request.ActivationName);
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error fetching system knowledge: Name={Name}",
                request.KnowledgeName);
            throw;
        }
    }
    
    /// <summary>
    /// Creates a KnowledgeService instance using static dependencies.
    /// </summary>
    private Xians.Lib.Agents.Knowledge.KnowledgeService CreateKnowledgeService()
    {
        if (_staticHttpClient == null)
        {
            throw new InvalidOperationException("KnowledgeActivities not properly initialized - HttpClient is null");
        }
        
        var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<Xians.Lib.Agents.Knowledge.KnowledgeService>();
        return new Xians.Lib.Agents.Knowledge.KnowledgeService(_staticHttpClient, _staticCacheService, logger);
    }

    /// <summary>
    /// Updates or creates knowledge on the server.
    /// Delegates to shared KnowledgeService.
    /// </summary>
    [Activity]
    public async Task<bool> UpdateKnowledgeAsync(UpdateKnowledgeRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "UpdateKnowledge activity started: Name={Name}, Agent={Agent}, Type={Type}, SystemScoped={SystemScoped}, Tenant={Tenant}, ActivationName={ActivationName}",
            request.KnowledgeName,
            request.AgentName,
            request.Type,
            request.SystemScoped,
            request.TenantId,
            request.ActivationName);

        try
        {
            var service = CreateKnowledgeService();
            return await service.UpdateAsync(
                request.KnowledgeName,
                request.Content,
                request.Type,
                request.AgentName,
                request.TenantId,
                request.SystemScoped,
                request.ActivationName);
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error updating knowledge: Name={Name}",
                request.KnowledgeName);
            throw;
        }
    }

    /// <summary>
    /// Deletes knowledge from the server.
    /// Delegates to shared KnowledgeService.
    /// </summary>
    [Activity]
    public async Task<bool> DeleteKnowledgeAsync(DeleteKnowledgeRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "DeleteKnowledge activity started: Name={Name}, Agent={Agent}, Tenant={Tenant}, ActivationName={ActivationName}",
            request.KnowledgeName,
            request.AgentName,
            request.TenantId,
            request.ActivationName);

        try
        {
            var service = CreateKnowledgeService();
            return await service.DeleteAsync(
                request.KnowledgeName,
                request.AgentName,
                request.TenantId,
                request.ActivationName);
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error deleting knowledge: Name={Name}",
                request.KnowledgeName);
            throw;
        }
    }

    /// <summary>
    /// Lists all knowledge for an agent.
    /// Delegates to shared KnowledgeService.
    /// </summary>
    [Activity]
    public async Task<List<Xians.Lib.Agents.Knowledge.Models.Knowledge>> ListKnowledgeAsync(ListKnowledgeRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "ListKnowledge activity started: Agent={Agent}, Tenant={Tenant}, ActivationName={ActivationName}",
            request.AgentName,
            request.TenantId,
            request.ActivationName);

        try
        {
            var service = CreateKnowledgeService();
            return await service.ListAsync(
                request.AgentName,
                request.TenantId,
                request.ActivationName);
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error listing knowledge for Agent={Agent}",
                request.AgentName);
            throw;
        }
    }
}

