using Xians.Lib.Agents.Knowledge.Models;
namespace Xians.Lib.Common.Caching;

/// <summary>
/// Configuration options for caching in Xians.Lib.
/// Provides centralized control over caching behavior across all SDK components.
/// </summary>
public class CacheOptions
{
    /// <summary>
    /// Gets or sets whether caching is enabled globally.
    /// When false, all cache operations are bypassed.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the default cache TTL (Time To Live) in minutes.
    /// Applied to cache entries that don't specify a custom TTL.
    /// Default: 5 minutes.
    /// </summary>
    public int DefaultTtlMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets cache configuration for knowledge items.
    /// </summary>
    public CacheAspectOptions Knowledge { get; set; } = new()
    {
        Enabled = true,
        TtlMinutes = 10
    };

    /// <summary>
    /// Gets or sets cache configuration for settings.
    /// </summary>
    public CacheAspectOptions Settings { get; set; } = new()
    {
        Enabled = true,
        TtlMinutes = 10
    };

    /// <summary>
    /// Gets or sets cache configuration for workflow definitions.
    /// </summary>
    public CacheAspectOptions WorkflowDefinitions { get; set; } = new()
    {
        Enabled = true,
        TtlMinutes = 15
    };

    /// <summary>
    /// Validates the cache configuration.
    /// </summary>
    internal void Validate()
    {
        if (DefaultTtlMinutes < 0)
        {
            throw new ArgumentException("DefaultTtlMinutes must be non-negative", nameof(DefaultTtlMinutes));
        }

        Knowledge.Validate(nameof(Knowledge));
        Settings.Validate(nameof(Settings));
        WorkflowDefinitions.Validate(nameof(WorkflowDefinitions));
    }
}

/// <summary>
/// Cache configuration for a specific aspect (e.g., knowledge, settings).
/// </summary>
public class CacheAspectOptions
{
    /// <summary>
    /// Gets or sets whether caching is enabled for this aspect.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the TTL (Time To Live) in minutes for this aspect.
    /// Default: 5 minutes.
    /// </summary>
    public int TtlMinutes { get; set; } = 5;

    /// <summary>
    /// Validates the aspect configuration.
    /// </summary>
    internal void Validate(string aspectName)
    {
        if (TtlMinutes < 0)
        {
            throw new ArgumentException($"{aspectName}.TtlMinutes must be non-negative");
        }
    }
}

