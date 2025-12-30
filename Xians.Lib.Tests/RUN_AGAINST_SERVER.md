# Running Xians.Lib.Tests Against Actual Server

## Quick Start

```bash
# 1. Create .env file from template
cp env.template .env

# 2. Edit .env with your server credentials
nano .env  # or use your preferred editor

# 3. Run all tests
dotnet test

# Or run only unit tests (fast, no server needed)
dotnet test --filter "Category!=Integration"

# Or run only integration tests (requires server)
dotnet test --filter "Category=Integration"
```

## 📋 Configuration (.env)

### Minimum Required Configuration

Edit `.env` and set these two values (lines 8-10 from env.template):

```bash
SERVER_URL=https://your-actual-server.com
API_KEY=your-actual-api-key-here
```

### Optional: Enable Temporal Integration Tests

If you want to test against an actual Temporal server (line 22):

```bash
RUN_INTEGRATION_TESTS=true
```

### Optional: Override Temporal Server URL

If you want to use a local Temporal server instead of the one from server settings (line 18):

```bash
TEMPORAL_SERVER_URL=localhost:7233
```

## 🔧 Complete .env Example

```bash
# =============================================================================
# MANDATORY - Your actual server configuration
# =============================================================================
SERVER_URL=https://dev.xians.ai
API_KEY=sk_live_abc123xyz789

# =============================================================================
# OPTIONAL - Advanced configuration
# =============================================================================

# Override Temporal server URL (normally fetched from server)
# Uncomment to use local Temporal instead of server-provided URL
# TEMPORAL_SERVER_URL=localhost:7233

# Enable full integration tests (requires Temporal server running)
RUN_INTEGRATION_TESTS=true
```

## 🧪 Test Execution Scenarios

### Scenario 1: Quick Unit Tests Only (Default)
**No server needed, very fast (<1 second)**

```bash
# .env configuration:
SERVER_URL=https://any-url.com  # Not actually used
API_KEY=dummy-key               # Not actually used
RUN_INTEGRATION_TESTS=false     # Default

# Run:
dotnet test --filter "Category!=Integration"
```

### Scenario 2: HTTP Integration Tests
**Requires actual server with valid credentials (~1-2 seconds)**

```bash
# .env configuration:
SERVER_URL=https://dev.xians.ai  # Your actual server
API_KEY=your-real-api-key         # Your actual API key
RUN_INTEGRATION_TESTS=false       # Don't need Temporal running

# Run:
dotnet test --filter "Category=Integration&FullyQualifiedName~Http"
```

### Scenario 3: Full Integration Tests
**Requires server + Temporal running (~10 seconds)**

```bash
# .env configuration:
SERVER_URL=https://dev.xians.ai
API_KEY=your-real-api-key
RUN_INTEGRATION_TESTS=true        # Enable Temporal tests

# Ensure Temporal is accessible (either from server settings or override)
# Option A: Use server's Temporal (fetched from /api/agent/settings/flowserver)
# No additional config needed

# Option B: Override with local Temporal
TEMPORAL_SERVER_URL=localhost:7233

# Start Temporal if using local override:
docker run -d -p 7233:7233 temporalio/auto-setup:latest

# Run all tests:
dotnet test
```

## 📊 What Gets Tested

### Unit Tests (14 tests - Always Run)
- ✅ Configuration validation
- ✅ ServerConfiguration tests
- ✅ TemporalConfiguration tests
- **No external dependencies**

### HTTP Integration Tests (12 tests)
- ✅ HTTP client initialization
- ✅ Request/response handling with retry
- ✅ Authentication headers
- ✅ Health checks
- **Requires**: Valid SERVER_URL and API_KEY

### Temporal Integration Tests (4 tests)
- ✅ Temporal service creation
- ✅ Connection establishment
- ✅ Health checking
- ✅ Error handling
- **Requires**: RUN_INTEGRATION_TESTS=true + Temporal server

## 🔍 Verifying Configuration

Test your .env configuration:

```bash
# Load .env and verify
cat .env

# Quick test - run unit tests only (should always pass)
dotnet test --filter "Category!=Integration"

# Test HTTP connection (requires valid SERVER_URL + API_KEY)
dotnet test --filter "FullyQualifiedName~CreateHttpClientService"

# Test Temporal connection (requires RUN_INTEGRATION_TESTS=true + Temporal)
dotnet test --filter "FullyQualifiedName~CreateTemporalService&Category=Integration"
```

## 🚨 Troubleshooting

### Tests Fail: "SERVER_URL environment variable is required"
**Solution**: Make sure .env file exists and has SERVER_URL set

```bash
# Check if .env exists
ls -la .env

# Create from template if missing
cp env.template .env
```

### HTTP Tests Fail: Connection errors
**Solution**: Verify SERVER_URL and API_KEY are correct

```bash
# Test manually with curl
curl -H "Authorization: Bearer YOUR_API_KEY" \
     https://your-server.com/api/agent/settings/flowserver
```

### Temporal Tests Skip
**Solution**: This is normal if RUN_INTEGRATION_TESTS=false

```bash
# Enable Temporal tests in .env
echo "RUN_INTEGRATION_TESTS=true" >> .env

# Ensure Temporal server is running
docker ps | grep temporal
```

### Temporal Tests Fail: Connection refused
**Solution**: Start Temporal server

```bash
# Start Temporal with Docker
docker run -d -p 7233:7233 temporalio/auto-setup:latest

# Or use override in .env
echo "TEMPORAL_SERVER_URL=localhost:7233" >> .env
```

## 📈 Expected Results

### Successful Run Against Real Server

```
Test run for Xians.Lib.Tests.dll (.NETCoreApp,Version=v9.0)
VSTest version 17.14.1

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    30, Skipped:     0, Total:    30, Duration: ~10s

✅ All 30 tests passed
   ├─ 14 Unit Tests
   ├─ 12 HTTP Integration Tests (using your server)
   └─ 4 Temporal Integration Tests (using server's Temporal config)
```

## 🔐 Security Notes

**⚠️ Never commit .env to Git!**

The `.gitignore` file already excludes `.env`:

```bash
# Check .gitignore
cat .gitignore | grep "^.env$"

# Verify .env is ignored
git status --ignored | grep ".env"
```

## 📚 What Happens Under the Hood

```mermaid
1. Test loads .env file (SERVER_URL + API_KEY)
   ↓
2. HTTP client is created with these credentials
   ↓
3. Test calls: GET /api/agent/settings/flowserver
   ↓
4. Server returns Temporal configuration:
   - FlowServerUrl
   - FlowServerNamespace
   - FlowServerCertBase64 (optional)
   - FlowServerPrivateKeyBase64 (optional)
   ↓
5. Temporal client is created with fetched config
   ↓
6. Tests run against your actual infrastructure
```

## 🎯 Best Practices

1. **Use separate API keys for testing** - Don't use production keys
2. **Test against dev/staging first** - Before production testing
3. **Keep .env local** - Never commit credentials
4. **Run unit tests frequently** - They're fast and don't need a server
5. **Run integration tests before deployment** - Verify server compatibility

## 💡 Example Workflow

```bash
# Daily development - unit tests only (fast)
dotnet test --filter "Category!=Integration"

# Before commit - quick integration tests
SERVER_URL=https://dev.xians.ai \
API_KEY=$DEV_API_KEY \
dotnet test

# Before deployment - full integration tests
# 1. Update .env with staging credentials
# 2. Enable Temporal tests
echo "RUN_INTEGRATION_TESTS=true" > .env
echo "SERVER_URL=https://staging.xians.ai" >> .env
echo "API_KEY=$STAGING_API_KEY" >> .env

# 3. Run all tests
dotnet test

# 4. If all pass, deploy to production
```



