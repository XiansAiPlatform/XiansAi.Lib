# Xians.Lib.Tests - Quick Start Guide

## ⚡ TL;DR

```bash
# Setup
cp env.template .env
# Edit .env - set SERVER_URL and API_KEY

# Run tests (fast — unit + mock; RealServer excluded)
dotnet test

# Unit only
dotnet test --filter "Category!=Integration&Category!=RealServer"
```

## Running Tests

### 1. Run Default Suite (Unit + Mock Integration)
```bash
dotnet test
```
RealServer tests are excluded. See [docs/RUNNING_TESTS.md](docs/RUNNING_TESTS.md).

### 2. Run Only Unit Tests (Fast - No External Dependencies)
```bash
dotnet test --filter "Category!=Integration&Category!=RealServer"
```

### 3. Run Only Integration Tests
```bash
dotnet test --filter "Category=Integration"
```

## 🔑 Configuration Pattern

**Xians.Lib follows the XiansAi.Lib.Src pattern:**
- ✅ **Only requires**: `SERVER_URL` and `API_KEY`
- ✅ **Temporal config**: Fetched from server endpoint
- ✅ **Endpoint**: `GET /api/agent/settings/flowserver`
- ✅ **Returns**: FlowServerUrl, FlowServerNamespace, certificates

## Test Organization

```
Xians.Lib.Tests/
├── UnitTests/                  # Fast, isolated tests
│   └── Configuration/          # Configuration validation tests
├── IntegrationTests/           # Tests with mock/real services
│   ├── Common/                 # ServiceFactory tests
│   ├── Http/                   # HTTP client tests (with WireMock)
│   └── Temporal/               # Temporal client tests
```

## Integration Tests Details

### HTTP Integration Tests
- ✅ **Always Run** - Use WireMock.Net to simulate HTTP servers
- No external dependencies required
- Test retry logic, authentication, health checks

### Temporal Integration Tests
- ⚠️ **Optional** - Controlled by `RUN_INTEGRATION_TESTS` environment variable
- Require a running Temporal server
- Default to safe mode (skip tests requiring Temporal)

## Setting Up for Full Integration Tests

### Option 1: Docker (Recommended)
```bash
# Start Temporal server
docker run -d -p 7233:7233 temporalio/auto-setup:latest

# Set environment variable
export RUN_INTEGRATION_TESTS=true

# Run integration tests
dotnet test --filter "Category=Integration"
```

### Option 2: Temporal CLI
```bash
# Install Temporal CLI
brew install temporal

# Start local Temporal server
temporal server start-dev

# In another terminal, run tests
export RUN_INTEGRATION_TESTS=true
dotnet test --filter "Category=Integration"
```

### Option 3: Use Environment File
```bash
# Create .env file
cp env.template .env

# Edit .env and set:
# RUN_INTEGRATION_TESTS=true
# TEMPORAL_SERVER_URL=localhost:7233

# Run tests
dotnet test
```

## Test Results Summary

```
✅ 30 Total Tests
   └─ 14 Unit Tests (Always run)
   └─ 16 Integration Tests
      ├─ 12 HTTP Tests (WireMock - Always run)
      └─ 4 Temporal Tests (Conditional)
```

## Common Commands

```bash
# Build tests
dotnet build

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test
dotnet test --filter "FullyQualifiedName~GetWithRetryAsync"

# Generate code coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Troubleshooting

### All Tests Failing
- Ensure you've run `dotnet restore`
- Check that Xians.Lib builds successfully

### HTTP Integration Tests Failing
- Check for port conflicts (WireMock uses random ports)
- Ensure no firewall blocking localhost connections

### Temporal Tests Skipped
- This is normal if `RUN_INTEGRATION_TESTS=false`
- Set `RUN_INTEGRATION_TESTS=true` to enable

### Temporal Tests Failing
- Ensure Temporal server is running on `localhost:7233`
- Check Temporal server logs for connection issues
- Verify namespace "test" exists (auto-created by default)

## CI/CD Integration

Example GitHub Actions workflow:

```yaml
- name: Run Unit Tests
  run: dotnet test --filter "Category!=Integration"

- name: Start Temporal
  run: docker run -d -p 7233:7233 temporalio/auto-setup:latest

- name: Run All Tests
  env:
    RUN_INTEGRATION_TESTS: true
  run: dotnet test
```

