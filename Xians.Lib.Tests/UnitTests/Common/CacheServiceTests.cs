using Xunit;
using Xians.Lib.Common;
using Xians.Lib.Agents.Models;

namespace Xians.Lib.Tests.UnitTests.Common;

public class CacheServiceTests : IDisposable
{
    private readonly CacheService _cacheService;

    public CacheServiceTests()
    {
        _cacheService = new CacheService();
    }

    [Fact]
    public void CacheService_DefaultOptions_InitializesCorrectly()
    {
        // Arrange & Act
        using var cache = new CacheService();
        var stats = cache.GetStatistics();

        // Assert
        Assert.True(stats.IsEnabled);
        Assert.Equal(0, stats.Count);
    }

    [Fact]
    public void CacheService_WithCustomOptions_UsesProvidedSettings()
    {
        // Arrange
        var options = new CacheOptions
        {
            Enabled = true,
            DefaultTtlMinutes = 10,
            Knowledge = new CacheAspectOptions
            {
                Enabled = true,
                TtlMinutes = 3
            }
        };

        // Act
        using var cache = new CacheService(options);
        var stats = cache.GetStatistics();

        // Assert
        Assert.True(stats.IsEnabled);
    }

    [Fact]
    public void Knowledge_SetAndGet_WorksCorrectly()
    {
        // Arrange
        var knowledge = new Knowledge
        {
            Name = "test",
            Content = "Test content"
        };

        // Act
        _cacheService.SetKnowledge("test-key", knowledge);
        var retrieved = _cacheService.GetKnowledge<Knowledge>("test-key");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("test", retrieved.Name);
        Assert.Equal("Test content", retrieved.Content);
    }

    [Fact]
    public void Knowledge_GetNonExistent_ReturnsNull()
    {
        // Act
        var retrieved = _cacheService.GetKnowledge<Knowledge>("non-existent");

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public void Knowledge_Remove_InvalidatesCache()
    {
        // Arrange
        var knowledge = new Knowledge { Name = "test", Content = "content" };
        _cacheService.SetKnowledge("test-key", knowledge);

        // Act
        _cacheService.RemoveKnowledge("test-key");
        var retrieved = _cacheService.GetKnowledge<Knowledge>("test-key");

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public void Knowledge_WithCachingDisabled_ReturnsNull()
    {
        // Arrange
        var options = new CacheOptions
        {
            Enabled = false
        };
        using var cache = new CacheService(options);

        var knowledge = new Knowledge { Name = "test", Content = "content" };

        // Act
        cache.SetKnowledge("test-key", knowledge);
        var retrieved = cache.GetKnowledge<Knowledge>("test-key");

        // Assert - Should not cache when disabled
        Assert.Null(retrieved);
    }

    [Fact]
    public void Knowledge_WithAspectDisabled_ReturnsNull()
    {
        // Arrange
        var options = new CacheOptions
        {
            Enabled = true,
            Knowledge = new CacheAspectOptions
            {
                Enabled = false,
                TtlMinutes = 5
            }
        };
        using var cache = new CacheService(options);

        var knowledge = new Knowledge { Name = "test", Content = "content" };

        // Act
        cache.SetKnowledge("test-key", knowledge);
        var retrieved = cache.GetKnowledge<Knowledge>("test-key");

        // Assert - Should not cache when aspect is disabled
        Assert.Null(retrieved);
    }

    [Fact]
    public void Clear_RemovesAllCachedItems()
    {
        // Arrange
        _cacheService.SetKnowledge("key1", new Knowledge { Name = "k1", Content = "c1" });
        _cacheService.SetKnowledge("key2", new Knowledge { Name = "k2", Content = "c2" });

        // Act
        _cacheService.Clear();
        var retrieved1 = _cacheService.GetKnowledge<Knowledge>("key1");
        var retrieved2 = _cacheService.GetKnowledge<Knowledge>("key2");

        // Assert
        Assert.Null(retrieved1);
        Assert.Null(retrieved2);
    }

    [Fact]
    public void GetStatistics_ReflectsCurrentState()
    {
        // Arrange
        _cacheService.SetKnowledge("key1", new Knowledge { Name = "k1", Content = "c1" });
        _cacheService.SetKnowledge("key2", new Knowledge { Name = "k2", Content = "c2" });

        // Act
        var stats = _cacheService.GetStatistics();

        // Assert
        Assert.True(stats.IsEnabled);
        Assert.True(stats.Count >= 2); // At least 2 items
    }

    [Fact]
    public void CacheOptions_Validate_WithNegativeTtl_ThrowsException()
    {
        // Arrange
        var options = new CacheOptions
        {
            DefaultTtlMinutes = -1
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void CacheAspectOptions_Validate_WithNegativeTtl_ThrowsException()
    {
        // Arrange
        var aspectOptions = new CacheAspectOptions
        {
            TtlMinutes = -1
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => aspectOptions.Validate("TestAspect"));
    }

    public void Dispose()
    {
        _cacheService?.Dispose();
    }
}

