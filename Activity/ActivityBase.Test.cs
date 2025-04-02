using Xunit;
using Microsoft.Extensions.Logging;
using XiansAi.Server;

namespace XiansAi.Activity;

public class ActivityBaseTest
{
    private class TestActivity : ActivityBase
    {
        public async Task<T?> GetCachedValue<T>(string key)
        {
            return await GetCacheValueAsync<T>(key);
        }

        public async Task<bool> SetCachedValue<T>(string key, T value)
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
        var result = await activity.GetCachedValue<string>(key);

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
        var getResult = await activity.GetCachedValue<string>(key);

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
        var getResult = await activity.GetCachedValue<string>(key);

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

    [Fact]
    public async Task CacheOperations_ShouldWorkWithDifferentDataTypes()
    {
        // Arrange
        var activity = new TestActivity();
        var stringKey = "string_key";
        var intKey = "int_key";
        var boolKey = "bool_key";
        var dateKey = "date_key";

        // Act & Assert
        // Test string
        await activity.SetCachedValue(stringKey, "test");
        var stringResult = await activity.GetCachedValue<string>(stringKey);
        Assert.Equal("test", stringResult);

        // Test integer
        await activity.SetCachedValue(intKey, 42);
        var intResult = await activity.GetCachedValue<int>(intKey);
        Assert.Equal(42, intResult);

        // Test boolean
        await activity.SetCachedValue(boolKey, true);
        var boolResult = await activity.GetCachedValue<bool>(boolKey);
        Assert.True(boolResult);

        // Test DateTime
        var date = DateTime.Now;
        await activity.SetCachedValue(dateKey, date);
        var dateResult = await activity.GetCachedValue<DateTime>(dateKey);
        Assert.Equal(date, dateResult);
    }

    [Fact]
    public async Task CacheOperations_ShouldHandleNullValues()
    {
        // Arrange
        var activity = new TestActivity();
        var key = "null_value_key";

        // Act
        var setResult = await activity.SetCachedValue<string>(key, null);
        var getResult = await activity.GetCachedValue<string>(key);

        // Assert
        Assert.True(setResult);
        Assert.Null(getResult);
    }

    [Fact]
    public async Task CacheOperations_ShouldHandleEmptyKeys()
    {
        // Arrange
        var activity = new TestActivity();
        var emptyKey = "";
        var value = "test_value";

        // Act & Assert
        // Empty key should still work
        var setResult = await activity.SetCachedValue(emptyKey, value);
        Assert.True(setResult);

        var getResult = await activity.GetCachedValue<string>(emptyKey);
        Assert.Equal(value, getResult);
    }

    [Fact]
    public async Task CacheOperations_ShouldHandleSpecialCharacters()
    {
        // Arrange
        var activity = new TestActivity();
        var specialKey = "test!@#$%^&*()_+-=[]{}|;:'\",.<>?/\\";
        var value = "test_value";

        // Act
        var setResult = await activity.SetCachedValue(specialKey, value);
        var getResult = await activity.GetCachedValue<string>(specialKey);

        // Assert
        Assert.True(setResult);
        Assert.Equal(value, getResult);
    }

    [Fact]
    public async Task CacheOperations_ShouldHandleLongValues()
    {
        // Arrange
        var activity = new TestActivity();
        var key = "long_value_key";
        var longValue = new string('a', 10000); // 10KB string

        // Act
        var setResult = await activity.SetCachedValue(key, longValue);
        var getResult = await activity.GetCachedValue<string>(key);

        // Assert
        Assert.True(setResult);
        Assert.Equal(longValue, getResult);
    }

    [Fact]
    public async Task ConcurrentCacheOperations_ShouldWorkCorrectly()
    {
        // Arrange
        var activity = new TestActivity();
        var tasks = new List<Task>();
        var keyPrefix = "concurrent_key_";
        var valuePrefix = "concurrent_value_";

        // Act
        // Create 10 concurrent set operations
        for (int i = 0; i < 10; i++)
        {
            var key = keyPrefix + i;
            var value = valuePrefix + i;
            tasks.Add(activity.SetCachedValue(key, value));
        }

        // Wait for all set operations to complete
        await Task.WhenAll(tasks);

        // Verify all values were set correctly
        for (int i = 0; i < 10; i++)
        {
            var key = keyPrefix + i;
            var expectedValue = valuePrefix + i;
            var actualValue = await activity.GetCachedValue<string>(key);
            Assert.Equal(expectedValue, actualValue);
        }
    }
} 