# Understanding Test Types in Xians.Lib.Tests

## 🎭 Test Categories Explained

### ❌ What You Thought Was Happening
```
.env (SERVER_URL=https://my-server.com) 
  ↓
Integration Tests → Connects to my-server.com
```

### ✅ What Actually Happens

```
Integration Tests → WireMock (localhost mock server)
  ↑
  └─ NEVER reads .env!
```

## 📊 Test Type Breakdown

| Test Type | Count | Uses .env? | Connects to Real Server? | Speed |
|-----------|-------|------------|-------------------------|-------|
| **Unit Tests** | 14 | ❌ No | ❌ No | ⚡ Very Fast (<1s) |
| **Integration Tests** | 16 | ❌ No* | ❌ No (uses WireMock) | 🏃 Fast (~10s) |
| **Real Server Tests** | 5 | ✅ Yes | ✅ YES! | 🐌 Depends on server |

*Only Temporal tests load .env, and only if `RUN_INTEGRATION_TESTS=true`

## 🧪 Test Type Details

### 1. Unit Tests (14 tests)
**Location**: `UnitTests/Configuration/`

**What they test**:
- Configuration validation
- Input validation
- Logic without external dependencies

**Example**:
```csharp
// Tests configuration objects in isolation
var config = new ServerConfiguration { 
    ServerUrl = "", 
    ApiKey = "test" 
};
Assert.Throws<InvalidOperationException>(() => config.Validate());
```

**Run command**:
```bash
dotnet test --filter "Category!=Integration&Category!=RealServer"
```

### 2. Integration Tests (16 tests) - MOCK BASED
**Location**: `IntegrationTests/Http/`, `IntegrationTests/Temporal/`, `IntegrationTests/Common/`

**What they test**:
- HTTP retry logic
- Request/response handling
- Service creation
- **All using MOCK servers (WireMock)**

**Example**:
```csharp
// Creates a LOCAL mock server
_mockServer = WireMockServer.Start();  // ← localhost:random-port

var config = new ServerConfiguration {
    ServerUrl = _mockServer.Url!,  // ← NOT from .env!
    ApiKey = "test-api-key"
};
```

**Why mock?**
- ✅ Fast and reliable
- ✅ No external dependencies
- ✅ Can simulate failures
- ✅ Works in CI/CD without credentials

**Run command**:
```bash
dotnet test --filter "Category=Integration"
```

### 3. Real Server Tests (5 tests) - ACTUALLY CONNECTS! ⭐
**Location**: `IntegrationTests/RealServer/`

**What they test**:
- ✅ **Actually connects to SERVER_URL from .env**
- ✅ **Uses API_KEY from .env**
- ✅ Fetches real settings from `/api/agent/settings/flowserver`
- ✅ Verifies end-to-end integration

**Example**:
```csharp
// Loads .env file
Env.Load();

// Uses YOUR actual server
_serverUrl = Environment.GetEnvironmentVariable("SERVER_URL");
_apiKey = Environment.GetEnvironmentVariable("API_KEY");

// Makes REAL HTTP request
var httpService = ServiceFactory.CreateHttpClientService(config);
var settings = await SettingsService.GetSettingsAsync(httpService);
// ↑ This actually calls: GET https://your-server.com/api/agent/settings/flowserver
```

**Run command**:
```bash
dotnet test --filter "Category=RealServer"
```

## 🎯 When to Use Each Test Type

### During Development (Fast Feedback)
```bash
# Run unit tests only - very fast
dotnet test --filter "Category!=Integration&Category!=RealServer"
```

### Before Commit (Default Loop)
```bash
# Unit + mock integration. RealServer is excluded automatically.
dotnet test
```

### Optional: Verify Against a Server You Own
```bash
# Setup .env with your actual server
cp env.template .env
# Edit .env: SERVER_URL=..., API_KEY=...

# Platform path: Server Lib-backed cycles (XiansAi.Server)
#   dotnet test --filter "FullyQualifiedName~EchoAgent"

# Lib-side opt-in against SERVER_URL
dotnet test --filter "Category=RealServer"
```

### Everything, Including RealServer
```bash
dotnet test --filter "Category!=RealServer|Category=RealServer"
```

## 🔍 How to Verify Tests Are Using Real Server

### Test with Invalid Credentials

**1. Set invalid SERVER_URL in .env**:
```bash
# .env
SERVER_URL=https://this-does-not-exist.invalid
API_KEY=fake-key
```

**2. Run integration tests (should PASS because they use mocks)**:
```bash
dotnet test --filter "Category=Integration"
# ✅ PASSES - uses WireMock, ignores .env
```

**3. Run real server tests (should FAIL)**:
```bash
dotnet test --filter "Category=RealServer"
# ❌ FAILS - actually tries to connect to https://this-does-not-exist.invalid
```

## 📝 Example Test Output

### Integration Tests (Mock-based)
```
Test run for Xians.Lib.Tests.dll
Starting test execution, please wait...

✅ GetWithRetryAsync_WithSuccessfulResponse_ShouldReturnData
   → Uses: WireMock server at http://localhost:12345
   → NOT your .env server!

Passed!  - Failed: 0, Passed: 16, Skipped: 0
```

### Real Server Tests
```
Test run for Xians.Lib.Tests.dll
Starting test execution, please wait...

✅ HttpClient_ShouldConnectToRealServer
   → Connected to: https://dev.xians.ai
   → Using API key from .env

✅ HttpClient_ShouldFetchSettingsFromRealServer
   → FlowServerUrl: temporal.example.com:7233
   → FlowServerNamespace: production

✅ RealServer_EndToEndTest
   → Step 1: HTTP connection successful
   → Step 2: Settings fetched - Temporal: temporal.example.com:7233
   → Step 3: Temporal client created
   → End-to-end test PASSED!

Passed!  - Failed: 0, Passed: 5, Skipped: 0
```

## 🚨 Common Mistakes

### ❌ Thinking Integration Tests Use .env
```bash
# This does NOT test against your real server:
dotnet test --filter "Category=Integration"
```

### ✅ Use Real Server Tests Instead
```bash
# This DOES test against your real server:
dotnet test --filter "Category=RealServer"
```

## 💡 Quick Reference

```bash
# Default loop (unit + mock). RealServer is excluded.
dotnet test

# Fast unit tests (no server)
dotnet test --filter "Category!=Integration&Category!=RealServer"

# Mock integration tests (WireMock, no real server)
dotnet test --filter "Category=Integration"

# REAL server tests (uses .env, connects to actual server) — opt-in
dotnet test --filter "Category=RealServer"

# Absolutely everything (includes RealServer)
dotnet test --filter "Category!=RealServer|Category=RealServer"
```

## 🎓 Summary

- **Unit Tests** = Test code logic in isolation
- **Integration Tests** = Test component integration with MOCKS (WireMock)
- **Real Server Tests** = Test against YOUR ACTUAL SERVER from .env

The integration tests passing with a fake URL was by design - they're testing the library's logic, not your server connectivity. To test your actual server, use `--filter "Category=RealServer"` (not part of a plain `dotnet test`). Live agent-against-Server coverage for platform PRs is in XiansAi.Server. See [RUNNING_TESTS.md](RUNNING_TESTS.md).

