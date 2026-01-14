# Logging Tests Documentation

This directory contains comprehensive tests for the Xians.Lib logging system.

## Test Structure

### Unit Tests (`UnitTests/Logging/`)

#### LogModelTests.cs
Tests the `Log` model structure and validation.

**Test Coverage:**
- ✅ Required fields validation
- ✅ Optional fields handling
- ✅ All log levels supported
- ✅ Null value handling
- ✅ Properties dictionary
- ✅ Exception handling
- ✅ Timestamp management

**Run Command:**
```bash
dotnet test --filter "FullyQualifiedName~LogModelTests"
```

#### ApiLoggerProviderTests.cs
Tests the `ApiLoggerProvider` and `ApiLogger` implementation.

**Test Coverage:**
- ✅ Logger creation
- ✅ Log level filtering
- ✅ Environment variable parsing
- ✅ Scope management
- ✅ Exception handling
- ✅ Multiple dispose safety
- ✅ Default log level behavior

**Run Command:**
```bash
dotnet test --filter "FullyQualifiedName~ApiLoggerProviderTests"
```

#### LoggerFactoryTests.cs
Tests the enhanced `LoggerFactory` with API logging support.

**Test Coverage:**
- ✅ Factory creation methods
- ✅ API logging enabled/disabled
- ✅ Thread safety
- ✅ Singleton pattern
- ✅ Reset functionality
- ✅ Environment variable handling
- ✅ Custom log levels

**Run Command:**
```bash
dotnet test --filter "FullyQualifiedName~LoggerFactoryTests"
```

### Integration Tests (`IntegrationTests/Logging/`)

#### LoggingServicesTests.cs
Tests the `LoggingServices` background processor with WireMock server.

**Test Coverage:**
- ✅ Log queue management
- ✅ Background processing
- ✅ Batch configuration
- ✅ HTTP upload with retry
- ✅ Graceful shutdown
- ✅ Multiple logs handling
- ✅ Failed upload retry logic

**Run Command:**
```bash
dotnet test --filter "FullyQualifiedName~LoggingServicesTests"
```

#### EndToEndLoggingTests.cs
Tests the complete logging flow from logger to HTTP upload.

**Test Coverage:**
- ✅ Full logging pipeline
- ✅ Context capture
- ✅ Multiple loggers
- ✅ Critical logs
- ✅ Exception logging
- ✅ Log level filtering
- ✅ Large batch processing
- ✅ Shutdown and flush

**Run Command:**
```bash
dotnet test --filter "FullyQualifiedName~EndToEndLoggingTests"
```

## Running All Logging Tests

```bash
# Run all unit tests
dotnet test --filter "FullyQualifiedName~UnitTests.Logging"

# Run all integration tests
dotnet test --filter "FullyQualifiedName~IntegrationTests.Logging"

# Run ALL logging tests
dotnet test --filter "FullyQualifiedName~Logging"
```

## Test Statistics

| Test File | Test Count | Category | Speed |
|-----------|------------|----------|-------|
| LogModelTests.cs | 7 | Unit | ⚡ Very Fast |
| ApiLoggerProviderTests.cs | 15 | Unit | ⚡ Very Fast |
| LoggerFactoryTests.cs | 14 | Unit | ⚡ Very Fast |
| LoggingServicesTests.cs | 13 | Integration | 🏃 Fast |
| EndToEndLoggingTests.cs | 10 | Integration | 🏃 Fast |
| **TOTAL** | **59** | Mixed | ~30-60s |

## Test Categories

### Unit Tests (36 tests)
- **No external dependencies**
- **Very fast execution** (<1 second total)
- **Test individual components in isolation**

### Integration Tests (23 tests)
- **Use WireMock for HTTP mocking**
- **Fast execution** (~30-60 seconds total)
- **Test component interaction**
- **No real server required**

## What's NOT Tested (By Design)

### Not Tested:
- ❌ Real server communication (use manual testing)
- ❌ Temporal workflow integration (requires full Temporal setup)
- ❌ Production performance under load
- ❌ Network timeout scenarios (too time-consuming)

### Why:
These scenarios require:
- Real Temporal server
- Real application server
- Long-running tests
- Manual verification

Use the examples in `Xians.Lib/Logging/Examples/` for manual testing of these scenarios.

## Common Test Patterns

### Pattern 1: Testing with Environment Variables

```csharp
[Fact]
public void Test_WithEnvironmentVariable()
{
    // Arrange
    Environment.SetEnvironmentVariable("API_LOG_LEVEL", "ERROR");
    
    // Act
    var logger = new ApiLogger();
    
    // Assert
    Assert.True(logger.IsEnabled(LogLevel.Error));
    
    // Cleanup
    Environment.SetEnvironmentVariable("API_LOG_LEVEL", null);
}
```

### Pattern 2: Testing Async Operations

```csharp
[Fact]
public async Task Test_AsyncOperation()
{
    // Arrange
    LoggingServices.Initialize(httpService);
    LoggingServices.ConfigureBatchSettings(5, 1000);
    
    // Act
    EnqueueLogs();
    await Task.Delay(3000); // Wait for processing
    
    // Assert
    Assert.True(logsWereSent);
}
```

### Pattern 3: Testing with WireMock

```csharp
[Fact]
public async Task Test_WithMockServer()
{
    // Arrange
    _mockServer
        .Given(Request.Create().WithPath("/api/logs"))
        .RespondWith(Response.Create().WithStatusCode(200));
    
    // Act
    await SendLog();
    
    // Assert
    var requests = _mockServer.LogEntries;
    Assert.Single(requests);
}
```

## Troubleshooting Tests

### Tests Timing Out

Some integration tests wait for background processing. If tests timeout:

```csharp
// Increase wait time
await Task.Delay(5000); // Instead of 3000
```

### Flaky Tests

If tests are flaky due to timing:

1. **Increase processing intervals** in test setup
2. **Add longer delays** before assertions
3. **Use polling** instead of fixed delays:

```csharp
// Instead of fixed delay
await Task.Delay(3000);

// Use polling
var maxWait = TimeSpan.FromSeconds(5);
var stopwatch = Stopwatch.StartNew();
while (stopwatch.Elapsed < maxWait && !condition)
{
    await Task.Delay(100);
}
```

### Environment Variable Conflicts

If tests interfere with each other:

1. **Always cleanup** in `Dispose()`:
```csharp
public void Dispose()
{
    Environment.SetEnvironmentVariable("API_LOG_LEVEL", null);
}
```

2. **Use `IDisposable`** pattern for all test classes
3. **Reset LoggerFactory** before tests:
```csharp
public TestClass()
{
    LoggerFactory.Reset();
}
```

## Continuous Integration

### GitHub Actions / CI Pipeline

```yaml
- name: Run Logging Tests
  run: dotnet test --filter "FullyQualifiedName~Logging" --logger "trx;LogFileName=logging-tests.trx"
```

### Test Coverage

To check test coverage:

```bash
dotnet test --collect:"XPlat Code Coverage" --filter "FullyQualifiedName~Logging"
```

## Adding New Tests

### Checklist for New Tests

- [ ] Add test to appropriate category (Unit/Integration)
- [ ] Follow naming convention: `MethodName_Scenario_ExpectedBehavior`
- [ ] Use `Arrange-Act-Assert` pattern
- [ ] Add cleanup in `Dispose()` if needed
- [ ] Add `[Fact]` or `[Theory]` attribute
- [ ] Add to test count in this README
- [ ] Verify test runs independently
- [ ] Verify test runs in suite

### Example New Test

```csharp
[Fact]
public void NewMethod_WithSpecificScenario_BehavesCorrectly()
{
    // Arrange
    var input = CreateTestInput();
    
    // Act
    var result = SystemUnderTest.NewMethod(input);
    
    // Assert
    Assert.Equal(expectedValue, result);
}
```

## Performance Benchmarks

Typical execution times on modern hardware:

| Test Suite | Time |
|------------|------|
| LogModelTests | <0.1s |
| ApiLoggerProviderTests | <0.5s |
| LoggerFactoryTests | <0.5s |
| LoggingServicesTests | ~10-20s |
| EndToEndLoggingTests | ~20-30s |
| **All Logging Tests** | **~30-60s** |

## Related Documentation

- [Main Logging README](../../../Xians.Lib/Logging/README.md)
- [Migration Guide](../../../Xians.Lib/Logging/MIGRATION_GUIDE.md)
- [Usage Examples](../../../Xians.Lib/Logging/Examples/LoggingUsageExample.cs)
- [Test Types Guide](../../docs/TEST_TYPES.md)
