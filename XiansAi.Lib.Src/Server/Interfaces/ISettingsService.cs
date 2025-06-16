using Server;

namespace XiansAi.Server.Interfaces;

/// <summary>
/// Interface for managing application settings with caching support
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the flow server settings, using cache if available
    /// </summary>
    /// <returns>The flow server settings</returns>
    Task<FlowServerSettings> GetFlowServerSettingsAsync();
    
    /// <summary>
    /// Refreshes the settings cache by invalidating current cache and reloading from server
    /// </summary>
    /// <returns>Task representing the refresh operation</returns>
    Task RefreshSettingsAsync();
    
    /// <summary>
    /// Gets cached settings without making a server call
    /// </summary>
    /// <returns>Cached settings if available, null otherwise</returns>
    FlowServerSettings? GetCachedSettings();
} 