using System.Reflection;
using XiansAi.Activity;
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Server;

namespace XiansAi.Lib.Tests.IntegrationTests;

public class ActivityBaseTest
{
    private class TestActivity : ActivityBase
    {
        private readonly string _mockWorkflowId;

        public TestActivity(Mock<ObjectCacheManager> mockCacheManager, string mockWorkflowId = "mock-workflow-id")
        {
            _mockWorkflowId = mockWorkflowId;

            // Use reflection to replace the internal _cacheManager with the mock
            var field = typeof(ActivityBase).GetField("_cacheManager", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(this, mockCacheManager.Object);
        }

        protected override string GetWorkflowPrefixedKey(string key)
        {
            if (string.IsNullOrEmpty(_mockWorkflowId))
            {
                throw new InvalidOperationException("Mock WorkflowId is not set.");
            }
            return $"{_mockWorkflowId}:{key}";
        }

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

    private readonly Mock<ObjectCacheManager> _mockCacheManager;

    public ActivityBaseTest()
    {
        // Initialize LogFactory
        Globals.LogFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });

        // Create a mock cache manager
        _mockCacheManager = new Mock<ObjectCacheManager>();
    }

    [Fact]
    public async Task GetCacheValue_ShouldReturnNull_WhenKeyDoesNotExist()
    {
        // Test: Verifies that retrieving a non-existent key from the cache returns null.
        // Arrange
        var mockWorkflowId = "test-workflow-id";
        var activity = new TestActivity(_mockCacheManager, mockWorkflowId);
        var key = "non_existent_key";
        var prefixedKey = $"{mockWorkflowId}:{key}";

        _mockCacheManager.Setup(m => m.GetValueAsync<string>(prefixedKey))
                         .ReturnsAsync((string?)null);

        // Act
        var result = await activity.GetCachedValue<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAndGetCacheValue_ShouldWorkCorrectly()
    {
        // Test: Ensures that a value can be set and retrieved correctly from the cache.
        // Arrange
        var mockWorkflowId = "test-workflow-id";
        var activity = new TestActivity(_mockCacheManager, mockWorkflowId);
        var key = "test_key";
        var value = "test_value";
        var prefixedKey = $"{mockWorkflowId}:{key}";

        _mockCacheManager.Setup(m => m.SetValueAsync(prefixedKey, value, It.IsAny<CacheOptions?>()))
                         .ReturnsAsync(true);

        _mockCacheManager.Setup(m => m.GetValueAsync<string>(prefixedKey))
                         .ReturnsAsync(value);

        // Act
        var setResult = await activity.SetCachedValue(key, value);
        var getResult = await activity.GetCachedValue<string>(key);

        // Assert
        Assert.True(setResult, "Failed to set cache value.");
        Assert.NotNull(getResult);
        Assert.Equal(value, getResult);
    }

    [Fact]
    public async Task DeleteCacheValue_ShouldRemoveValue()
    {
        // Test: Confirms that deleting a value from the cache removes it successfully.
        // Arrange
        var mockWorkflowId = "test-workflow-id";
        var activity = new TestActivity(_mockCacheManager, mockWorkflowId);
        var key = "test_key";
        var value = "test_value";
        var prefixedKey = $"{mockWorkflowId}:{key}";

        _mockCacheManager.Setup(m => m.SetValueAsync(prefixedKey, value, It.IsAny<CacheOptions?>()))
                        .ReturnsAsync(true);

        _mockCacheManager.Setup(m => m.GetValueAsync<string>(prefixedKey))
                        .ReturnsAsync(value);

        _mockCacheManager.Setup(m => m.DeleteValueAsync(prefixedKey))
                        .ReturnsAsync(true)
                        .Callback(() =>
                        {
                            // Simulate removal: Update mock so GetValueAsync returns null
                            _mockCacheManager.Setup(m => m.GetValueAsync<string>(prefixedKey))
                                            .ReturnsAsync((string)null);
                        });

        // Act
        var setResult = await activity.SetCachedValue(key, value);
        var deleteResult = await activity.DeleteCachedValue(key);
        var getResult = await activity.GetCachedValue<string>(key);

        // Assert
        Assert.True(setResult);  // Value was set
        Assert.True(deleteResult); // Value was deleted
        Assert.Null(getResult); // Value should not exist after deletion
    }

    [Fact]
    public void GetLogger_ShouldReturnValidLogger()
    {
        // Test: Checks that the GetLogger method returns a valid ILogger instance.
        // Arrange
        var mockWorkflowId = "test-workflow-id";
        var activity = new TestActivity(_mockCacheManager, mockWorkflowId);

        // Act
        var logger = activity.GetTestLogger();

        // Assert
        Assert.NotNull(logger);
        Assert.IsAssignableFrom<ILogger>(logger);
    }

    [Fact]
    public async Task CacheOperations_ShouldWorkWithDifferentDataTypes()
    {
        // Test: Validates that the cache can handle various data types (string, int, bool, DateTime).
        // Arrange
        var mockWorkflowId = "test-workflow-id";
        var activity = new TestActivity(_mockCacheManager, mockWorkflowId);

        _mockCacheManager.Setup(m => m.SetValueAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CacheOptions?>()))
                         .ReturnsAsync(true);

        _mockCacheManager.Setup(m => m.GetValueAsync<string>($"{mockWorkflowId}:string_key"))
                         .ReturnsAsync("test");

        _mockCacheManager.Setup(m => m.GetValueAsync<int>($"{mockWorkflowId}:int_key"))
                         .ReturnsAsync(42);

        _mockCacheManager.Setup(m => m.GetValueAsync<bool>($"{mockWorkflowId}:bool_key"))
                         .ReturnsAsync(true);

        var date = DateTime.Now;
        _mockCacheManager.Setup(m => m.GetValueAsync<DateTime>($"{mockWorkflowId}:date_key"))
                         .ReturnsAsync(date);

        // Act & Assert
        await activity.SetCachedValue("string_key", "test");
        Assert.Equal("test", await activity.GetCachedValue<string>("string_key"));

        await activity.SetCachedValue("int_key", 42);
        Assert.Equal(42, await activity.GetCachedValue<int>("int_key"));

        await activity.SetCachedValue("bool_key", true);
        Assert.True(await activity.GetCachedValue<bool>("bool_key"));

        await activity.SetCachedValue("date_key", date);
        Assert.Equal(date, await activity.GetCachedValue<DateTime>("date_key"));
    }

    [Fact]
    public async Task CacheOperations_ShouldHandleNullValues()
    {
        // Test: Ensures that the cache can handle null values correctly.
        // Arrange
        var mockWorkflowId = "test-workflow-id";
        var activity = new TestActivity(_mockCacheManager, mockWorkflowId);
        var key = "null_value_key";
        var prefixedKey = $"{mockWorkflowId}:{key}";

        _mockCacheManager.Setup(m => m.SetValueAsync<string>(prefixedKey, null, It.IsAny<CacheOptions?>()))
                         .ReturnsAsync(true);

        _mockCacheManager.Setup(m => m.GetValueAsync<string>(prefixedKey))
                         .ReturnsAsync((string)null);

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
        // Test: Verifies that the cache can handle empty string keys.
        // Arrange
        var mockWorkflowId = "test-workflow-id";
        var activity = new TestActivity(_mockCacheManager, mockWorkflowId);
        var emptyKey = "";
        var value = "test_value";
        var prefixedKey = $"{mockWorkflowId}:{emptyKey}";

        _mockCacheManager.Setup(m => m.SetValueAsync(prefixedKey, value, It.IsAny<CacheOptions?>()))
                         .ReturnsAsync(true);

        _mockCacheManager.Setup(m => m.GetValueAsync<string>(prefixedKey))
                         .ReturnsAsync(value);

        // Act
        var setResult = await activity.SetCachedValue(emptyKey, value);
        var getResult = await activity.GetCachedValue<string>(emptyKey);

        // Assert
        Assert.True(setResult);
        Assert.Equal(value, getResult);
    }

    [Fact]
    public async Task CacheOperations_ShouldHandleSpecialCharacters()
    {
        // Test: Confirms that keys with special characters can be used in the cache.
        // Arrange
        var mockWorkflowId = "test-workflow-id";
        var activity = new TestActivity(_mockCacheManager, mockWorkflowId);
        var specialKey = "test!@#$%^&*()_+-=[]{}|;:'\",.<>?/\\\\"; 
        var value = "test_value";
        var prefixedKey = $"{mockWorkflowId}:{specialKey}";

        _mockCacheManager.Setup(m => m.SetValueAsync(prefixedKey, value, It.IsAny<CacheOptions?>()))
                         .ReturnsAsync(true);

        _mockCacheManager.Setup(m => m.GetValueAsync<string>(prefixedKey))
                         .ReturnsAsync(value);

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
        // Test: Ensures that the cache can handle large values (e.g., 10KB strings).
        // Arrange
        var mockWorkflowId = "test-workflow-id";
        var activity = new TestActivity(_mockCacheManager, mockWorkflowId);
        var key = "long_value_key";
        var longValue = new string('a', 10000); // 10KB string
        var prefixedKey = $"{mockWorkflowId}:{key}";

        _mockCacheManager.Setup(m => m.SetValueAsync(prefixedKey, longValue, It.IsAny<CacheOptions?>()))
                         .ReturnsAsync(true);

        _mockCacheManager.Setup(m => m.GetValueAsync<string>(prefixedKey))
                         .ReturnsAsync(longValue);

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
        // Test: Validates that concurrent cache operations work without conflicts or data corruption.
        // Arrange
        var mockWorkflowId = "test-workflow-id";
        var activity = new TestActivity(_mockCacheManager, mockWorkflowId);
        var keyPrefix = "concurrent_key_";
        var valuePrefix = "concurrent_value_";

        var tasks = new List<Task>();

        for (int i = 0; i < 10; i++)
        {
            var key = keyPrefix + i;
            var value = valuePrefix + i;
            var prefixedKey = $"{mockWorkflowId}:{key}";

            _mockCacheManager.Setup(m => m.SetValueAsync(prefixedKey, value, It.IsAny<CacheOptions?>()))
                             .ReturnsAsync(true);

            _mockCacheManager.Setup(m => m.GetValueAsync<string>(prefixedKey))
                             .ReturnsAsync(value);

            tasks.Add(activity.SetCachedValue(key, value));
        }

        // Act
        await Task.WhenAll(tasks);

        // Assert
        for (int i = 0; i < 10; i++)
        {
            var key = keyPrefix + i;
            var expectedValue = valuePrefix + i;
            var actualValue = await activity.GetCachedValue<string>(key);
            Assert.Equal(expectedValue, actualValue);
        }
    }

    [Fact]
    public async Task ConcurrentCacheReads_ShouldReturnConsistentValues()
    {
        // Test: Ensures that concurrent reads from the cache return consistent values.
        // This test simulates multiple concurrent read operations for the same key
        // and verifies that all reads return the expected value without any inconsistencies.

        // Arrange
        var mockWorkflowId = "test-workflow-id";
        var activity = new TestActivity(_mockCacheManager, mockWorkflowId);
        var key = "read_concurrent_key";
        var value = "consistent_value";
        var prefixedKey = $"{mockWorkflowId}:{key}";

        _mockCacheManager.Setup(m => m.GetValueAsync<string>(prefixedKey))
                        .ReturnsAsync(value);

        var tasks = Enumerable.Range(0, 100)
                            .Select(_ => activity.GetCachedValue<string>(key))
                            .ToArray();

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, result => Assert.Equal(value, result));
    }

    [Fact]
    public async Task SetCachedValue_ShouldOverwriteExistingValue()
    {
        // Test: Verifies that setting a new value for an existing key overwrites the previous value.
        // This test ensures that the cache correctly updates the value associated with a key
        // when a new value is set, and the old value is no longer retrievable.

        // Arrange
        var mockWorkflowId = "test-workflow-id";
        var activity = new TestActivity(_mockCacheManager, mockWorkflowId);
        var key = "overwrite_key";
        var initialValue = "initial_value";
        var newValue = "new_value";
        var prefixedKey = $"{mockWorkflowId}:{key}";

        _mockCacheManager.Setup(m => m.SetValueAsync(prefixedKey, initialValue, It.IsAny<CacheOptions?>()))
                        .ReturnsAsync(true);
        _mockCacheManager.Setup(m => m.SetValueAsync(prefixedKey, newValue, It.IsAny<CacheOptions?>()))
                        .ReturnsAsync(true);
        _mockCacheManager.Setup(m => m.GetValueAsync<string>(prefixedKey))
                        .ReturnsAsync(newValue);

        // Act
        await activity.SetCachedValue(key, initialValue);
        await activity.SetCachedValue(key, newValue);
        var result = await activity.GetCachedValue<string>(key);

        // Assert
        Assert.Equal(newValue, result);
    }
}