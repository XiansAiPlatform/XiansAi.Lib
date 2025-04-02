using Xunit;
using Microsoft.Extensions.Logging;
using XiansAi.Server;

namespace XiansAi.Activity;

public class ActivityBaseTest
{
    private class TestActivity : ActivityBase
    {
        public async Task<string?> GetCachedValue(string key)
        {
            return await GetCacheValueAsync<string>(key);
        }

        public async Task<bool> SetCachedValue(string key, string value)
        {
            return await SetCacheValueAsync(key, value);
        }

        public async Task<bool> DeleteCachedValue(string key)
        {
            return await DeleteCacheValueAsync(key);
        }

        public ILogger GetTestLogger()
        {
            return GetLogger();
        }
    }

    public ActivityBaseTest()
    {
        // Initialize the LogFactory for testing
        Globals.LogFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });
    }

    [Fact]
    public async Task GetCacheValue_ShouldReturnNull_WhenKeyDoesNotExist()
    {
        // Arrange
        var activity = new TestActivity();
        var key = "non_existent_key";

        // Act
        var result = await activity.GetCachedValue(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAndGetCacheValue_ShouldWorkCorrectly()
    {
        // Arrange
        var activity = new TestActivity();
        var key = "test_key";
        var value = "test_value";

        // Act
        var setResult = await activity.SetCachedValue(key, value);
        var getResult = await activity.GetCachedValue(key);

        // Assert
        Assert.True(setResult);
        Assert.Equal(value, getResult);
    }

    [Fact]
    public async Task DeleteCacheValue_ShouldRemoveValue()
    {
        // Arrange
        var activity = new TestActivity();
        var key = "delete_test_key";
        var value = "delete_test_value";

        // Act
        await activity.SetCachedValue(key, value);
        var deleteResult = await activity.DeleteCachedValue(key);
        var getResult = await activity.GetCachedValue(key);

        // Assert
        Assert.True(deleteResult);
        Assert.Null(getResult);
    }

    [Fact]
    public void GetLogger_ShouldReturnValidLogger()
    {
        // Arrange
        var activity = new TestActivity();

        // Act
        var logger = activity.GetTestLogger();

        // Assert
        Assert.NotNull(logger);
        Assert.IsAssignableFrom<ILogger>(logger);
    }
} 