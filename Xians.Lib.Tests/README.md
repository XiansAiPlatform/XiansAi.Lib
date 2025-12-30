# Xians.Lib Tests

Comprehensive test suite for the Xians.Lib library, including unit tests and integration tests.

## Project Structure

```
Xians.Lib.Tests/
├── UnitTests/
│   ├── Configuration/        # Configuration validation tests
│   │   ├── ServerConfigurationTests.cs
│   │   └── TemporalConfigurationTests.cs
│
├── IntegrationTests/
│   ├── Common/              # ServiceFactory integration tests
│   ├── Http/                # HTTP client integration tests
│   └── Temporal/            # Temporal client integration tests
│
├── .env.example             # Example environment configuration
└── Xians.Lib.Tests.csproj
```

## Running Tests

### All Tests (includes mocks, but NOT real server)
```bash
dotnet test
```

### Unit Tests Only (fast, no dependencies)
```bash
dotnet test --filter "Category!=Integration&Category!=RealServer"
```

### Mock Integration Tests (uses WireMock, not real server)
```bash
dotnet test --filter "Category=Integration"
```

### **Real Server Tests (actually connects to .env server)** ⭐
```bash
# These actually hit your server!
dotnet test --filter "Category=RealServer"
```

### All Tests INCLUDING Real Server
```bash
dotnet test --filter "Category!=Integration|Category=RealServer"
```

### With Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Configuration

### Environment Variables

Following the XiansAi.Lib.Src pattern, **only SERVER_URL and API_KEY are required**. Temporal configuration is fetched from the server's settings endpoint.

1. Copy `env.template` to `.env`:
   ```bash
   cp env.template .env
   ```

2. Update the mandatory values in `.env`:
   ```env
   # MANDATORY - Application Server Configuration
   SERVER_URL=https://api.example.com
   API_KEY=your-api-key
   
   # OPTIONAL - Override Temporal server URL (normally fetched from server)
   # TEMPORAL_SERVER_URL=localhost:7233
   
   # OPTIONAL - Enable integration tests requiring Temporal
   RUN_INTEGRATION_TESTS=false
   ```

### How It Works

```
┌──────────────┐
│  Your App    │
└──────┬───────┘
       │ 1. Initialize with SERVER_URL + API_KEY
       ▼
┌──────────────────────────────────────────┐
│  Xians.Lib                               │
│  ┌────────────────────────────────────┐  │
│  │ HTTP Client (SERVER_URL, API_KEY)  │  │
│  └──────────────┬─────────────────────┘  │
│                 │ 2. GET /api/agent/settings/flowserver
│                 ▼
│  ┌────────────────────────────────────┐  │
│  │ Temporal Client (settings from ↑)  │  │
│  │ - FlowServerUrl                    │  │
│  │ - FlowServerNamespace              │  │
│  │ - FlowServerCertBase64             │  │
│  │ - FlowServerPrivateKeyBase64       │  │
│  └────────────────────────────────────┘  │
└──────────────────────────────────────────┘
```

### Running Integration Tests

Integration tests are organized into two categories:

#### 1. **Mock-based Integration Tests** (Always Run)
- HTTP client tests use WireMock.Net
- Don't require external services
- Test retry logic, authentication, headers, etc.

#### 2. **Real Service Integration Tests** (Optional)
- Temporal client tests require a running Temporal server
- Controlled by `RUN_INTEGRATION_TESTS` environment variable
- Set to `true` only when you have services running

### Setting Up Temporal for Integration Tests

**Option 1: Docker Compose**
```bash
# Start Temporal server
docker run -d -p 7233:7233 temporalio/auto-setup:latest
```

**Option 2: Local Temporal CLI**
```bash
temporal server start-dev
```

Then enable integration tests:
```bash
export RUN_INTEGRATION_TESTS=true
dotnet test --filter "Category=Integration"
```

## Test Categories

### Unit Tests (14 tests)
- **Configuration Tests**: Validate configuration objects
- Fast execution, no external dependencies
- Run on every commit

### Integration Tests (16 tests)
**Important**: These use WireMock (mock servers), NOT your real server!
- **HTTP Client Tests**: Test HTTP communication with mock servers
- **Temporal Client Tests**: Test Temporal client connections (conditional)
- **ServiceFactory Tests**: Test service creation and configuration

### Real Server Tests (5 tests) - **NEW!**
**Actually connect to the server in your .env file**
- Test real HTTP connection
- Fetch actual settings from server
- Verify end-to-end integration
- Run with: `dotnet test --filter "Category=RealServer"`

## CI/CD Integration

### GitHub Actions Example
```yaml
- name: Run Unit Tests
  run: dotnet test --filter "Category!=Integration"

- name: Start Temporal (Optional)
  run: docker run -d -p 7233:7233 temporalio/auto-setup:latest

- name: Run Integration Tests
  env:
    RUN_INTEGRATION_TESTS: true
    TEMPORAL_SERVER_URL: localhost:7233
  run: dotnet test --filter "Category=Integration"
```

## Test Frameworks & Libraries

- **xUnit**: Testing framework
- **Moq**: Mocking framework for unit tests
- **WireMock.Net**: HTTP server mocking for integration tests
- **DotNetEnv**: Environment variable management
- **coverlet**: Code coverage collection

## Writing New Tests

### Unit Test Example
```csharp
public class MyServiceTests
{
    [Fact]
    public void Method_WithValidInput_ShouldReturnExpectedResult()
    {
        // Arrange
        var service = new MyService();
        
        // Act
        var result = service.Method("input");
        
        // Assert
        Assert.Equal("expected", result);
    }
}
```

### Integration Test Example
```csharp
[Trait("Category", "Integration")]
public class MyIntegrationTests : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        // Setup resources
    }

    [Fact]
    public async Task Integration_Test()
    {
        // Test with real/mock services
    }

    public async Task DisposeAsync()
    {
        // Cleanup resources
    }
}
```

## Best Practices

1. **Isolate Tests**: Each test should be independent
2. **Use Fixtures**: Leverage `IAsyncLifetime` for setup/teardown
3. **Clear Naming**: Test names should describe what they test
4. **Tag Integration Tests**: Use `[Trait("Category", "Integration")]`
5. **Mock External Services**: Use WireMock for HTTP dependencies
6. **Environment Variables**: Use `.env` for configuration

## Troubleshooting

### Integration Tests Failing
- Ensure external services are running (Temporal, etc.)
- Check environment variables are set correctly
- Verify network connectivity to services

### WireMock Tests Failing
- Check for port conflicts
- Ensure proper cleanup in `DisposeAsync`

### Coverage Issues
- Make sure coverlet.collector is installed
- Use: `dotnet test --collect:"XPlat Code Coverage"`

