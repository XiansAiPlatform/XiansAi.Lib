# Usage Tracking Tests

Comprehensive test suite for the usage tracking functionality in Xians.Lib.

## Test Structure

### 📊 Test Count: **28 Tests** across 3 levels

| Test Level | File | Count | Speed | Dependencies |
|------------|------|-------|-------|--------------|
| **Unit Tests** | `UnitTests/Common/UsageTrackingTests.cs` | 13 | ⚡ Very Fast (<1s) | None (Moq only) |
| **Integration Tests** | `IntegrationTests/Common/UsageTrackingIntegrationTests.cs` | 9 | 🏃 Fast (~5s) | WireMock (local mock) |
| **Real Server Tests** | `IntegrationTests/RealServer/RealServerUsageTrackingTests.cs` | 6 | 🌐 Variable | Real Xians server |

---

## 1. Unit Tests (13 tests)

**File**: `UnitTests/Common/UsageTrackingTests.cs`

**Purpose**: Test usage tracking components in isolation with mocked HTTP

**Coverage**:

### UsageEventsClient Tests
- ✅ `ReportAsync_WithValidRecord_SendsToCorrectEndpoint`
- ✅ `ReportAsync_WithTenantId_IncludesTenantHeader`
- ✅ `ReportAsync_WithCorrectPayload_SerializesProperlyToCamelCase`
- ✅ `ReportAsync_WhenHttpServiceNotAvailable_DoesNotThrow`
- ✅ `ReportAsync_WhenServerReturnsError_DoesNotThrow`

### Token Extraction Tests
- ✅ `ExtractUsageFromSemanticKernelResponses_WithNoResponses_ReturnsZeros`
- ✅ `ExtractUsageFromSemanticKernelResponses_WithNullResponses_ReturnsZeros`
- ✅ `ExtractUsageFromSemanticKernelResponses_WithMockUsageData_ExtractsCorrectly`

### UsageTracker Tests
- ✅ `UsageTracker_MeasuresElapsedTime`
- ✅ `UsageTracker_WithMessageCount_IncludesInReport`

### Extension Method Tests
- ✅ `ReportUsageAsync_ExtensionMethod_WithMessageCount_IncludesInReport`
- ✅ `ReportUsageAsync_ExtensionMethod_DefaultMessageCount_UsesOne`
- ✅ `MultipleReports_AllGetSentSuccessfully`

**Run Command**:
```bash
# Run only unit tests
dotnet test --filter "FullyQualifiedName~UsageTrackingTests&Category!=Integration&Category!=RealServer"

# With verbosity
dotnet test --filter "FullyQualifiedName~UsageTrackingTests&Category!=Integration&Category!=RealServer" --logger "console;verbosity=detailed"
```

**Key Validations**:
- ✅ Correct HTTP endpoint (`/api/agent/usage/report`)
- ✅ Proper JSON serialization (camelCase)
- ✅ Tenant header inclusion
- ✅ Exception handling (no throws)
- ✅ Token extraction from mock responses
- ✅ MessageCount parameter handling

---

## 2. Integration Tests (9 tests)

**File**: `IntegrationTests/Common/UsageTrackingIntegrationTests.cs`

**Purpose**: Test HTTP request/response cycle with WireMock (no real server needed)

**Coverage**:

### HTTP Integration
- ✅ `ReportAsync_WithValidRecord_SendsCorrectPayload`
- ✅ `ReportAsync_WithTenantId_IncludesTenantHeader`
- ✅ `ReportAsync_WhenServerReturns500_DoesNotThrow`
- ✅ `ReportAsync_WhenServerReturns400_DoesNotThrow`

### End-to-End Mock Tests
- ✅ `UsageTracker_ReportsWithTiming`
- ✅ `ExtensionMethod_ReportUsageAsync_SendsCorrectData`
- ✅ `MultipleReports_AllGetSentSuccessfully`
- ✅ `UsageTracker_WithMetadata_IncludesInPayload`

**Run Command**:
```bash
# Run integration tests
dotnet test --filter "FullyQualifiedName~UsageTrackingIntegrationTests&Category=Integration"

# With verbosity
dotnet test --filter "FullyQualifiedName~UsageTrackingIntegrationTests&Category=Integration" --logger "console;verbosity=detailed"
```

**Key Validations**:
- ✅ Full HTTP request/response with WireMock
- ✅ Payload structure matches server expectations
- ✅ Error handling (4xx, 5xx responses)
- ✅ Timing measurement accuracy
- ✅ Multiple concurrent requests
- ✅ Metadata serialization

---

## 3. Real Server Tests (6 tests)

**File**: `IntegrationTests/RealServer/RealServerUsageTrackingTests.cs`

**Purpose**: Test against actual Xians server with real workflows

**Coverage**:

### Basic Functionality
- ✅ `UsageTracking_WithSingleMessage_ReportsCorrectly`
  - Sends message, verifies usage tracking executed

### Conversation History
- ✅ `UsageTracking_WithConversationHistory_IncludesCorrectMessageCount`
  - Builds conversation history
  - Verifies message count tracking

### UsageTracker
- ✅ `UsageTracker_WithTiming_WorksEndToEnd`
  - Tests automatic timing measurement

### Custom Metadata
- ✅ `UsageTracking_WithMetadata_ReportsSuccessfully`
  - Verifies custom metadata support

### Multiple LLM Calls
- ✅ `UsageTracking_WithMultipleLLMCalls_ReportsEachSeparately`
  - Simulates multiple LLM calls in one handler
  - Each reported separately

### Resilience
- ✅ `UsageTracking_WhenServerError_DoesNotBreakWorkflow`
  - Verifies workflow continues if usage reporting fails

**Prerequisites**:
```bash
# Setup .env file
cp env.template .env

# Edit .env
SERVER_URL=https://your-server.com
API_KEY=<base64-encoded-certificate>
```

**Run Command**:
```bash
# Run real server tests
dotnet test --filter "FullyQualifiedName~RealServerUsageTrackingTests&Category=RealServer"

# With verbosity
dotnet test --filter "FullyQualifiedName~RealServerUsageTrackingTests&Category=RealServer" --logger "console;verbosity=detailed"
```

**Key Validations**:
- ✅ End-to-end with real Temporal workflows
- ✅ Actual HTTP requests to server
- ✅ Message history integration
- ✅ Multiple usage reports in single handler
- ✅ Workflow resilience

---

## Running All Usage Tracking Tests

### Run All Levels Together
```bash
dotnet test --filter "FullyQualifiedName~UsageTracking"
```

### Run Unit + Integration (Skip Real Server)
```bash
dotnet test --filter "FullyQualifiedName~UsageTracking&Category!=RealServer"
```

### Run Only Real Server Tests
```bash
dotnet test --filter "Category=RealServer&FullyQualifiedName~UsageTracking"
```

---

## Test Patterns Used

### 1. Mock-based Unit Testing
```csharp
// Mock HTTP handler to capture requests
_httpMessageHandler.Protected()
    .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
    .Callback<HttpRequestMessage, CancellationToken>((req, ct) => 
        capturedRequest = req)
    .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
```

### 2. WireMock Integration Testing
```csharp
// Setup mock server endpoint
_mockServer
    .Given(Request.Create()
        .WithPath("/api/agent/usage/report")
        .UsingPost())
    .RespondWith(Response.Create()
        .WithStatusCode(200));
```

### 3. Real Server Testing
```csharp
// Real workflow with usage tracking
workflow.OnUserChatMessage(async (context) =>
{
    // Simulate LLM call
    await Task.Delay(100);
    
    // Track usage
    await context.ReportUsageAsync(
        model: "gpt-4",
        promptTokens: 150,
        completionTokens: 75,
        totalTokens: 225
    );
    
    await context.ReplyAsync("Response");
});
```

---

## What's Tested

### ✅ Functionality
- Correct HTTP endpoint and method
- Payload serialization (camelCase JSON)
- Tenant header inclusion
- Message count parameter
- Custom metadata support
- Response time measurement
- Multiple reports per handler

### ✅ Error Handling
- Server errors (4xx, 5xx) don't throw
- Missing HTTP service handled gracefully
- Workflow continues if tracking fails
- No exceptions break agent operation

### ✅ Integration
- Works with UserMessageContext
- Works with conversation history
- Works with UsageTracker helper
- Works with extension methods
- Works with real Temporal workflows

---

## Test Results Example

### Unit Tests Output
```
Test run for Xians.Lib.Tests.dll
Starting test execution, please wait...

✅ ReportAsync_WithValidRecord_SendsToCorrectEndpoint
✅ ReportAsync_WithTenantId_IncludesTenantHeader
✅ ReportAsync_WithCorrectPayload_SerializesProperlyToCamelCase
✅ ExtractUsageFromSemanticKernelResponses_WithMockUsageData_ExtractsCorrectly
✅ UsageTracker_MeasuresElapsedTime
✅ ReportUsageAsync_ExtensionMethod_WithMessageCount_IncludesInReport
... (7 more tests)

Passed!  - Failed: 0, Passed: 13, Skipped: 0, Total: 13
Duration: < 1 s
```

### Integration Tests Output
```
Test run for Xians.Lib.Tests.dll
Starting test execution, please wait...

✅ ReportAsync_WithValidRecord_SendsCorrectPayload
   → WireMock server: http://localhost:12345
   → Verified payload structure
   
✅ UsageTracker_ReportsWithTiming
   → Measured: 52ms (expected >= 50ms)
   
... (7 more tests)

Passed!  - Failed: 0, Passed: 9, Skipped: 0, Total: 9
Duration: 4.8 s
```

### Real Server Tests Output
```
Test run for Xians.Lib.Tests.dll
Starting test execution, please wait...

✅ UsageTracking_WithSingleMessage_ReportsCorrectly
   → Agent: UsageTrackingTestAgent
   → Workflow: UsageTestWorkflow
   → Message processed successfully
   → Usage tracking executed
   
✅ UsageTracking_WithConversationHistory_IncludesCorrectMessageCount
   → Built history: 4 messages
   → Processed 4 messages with conversation history
   
... (4 more tests)

Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6
Duration: 18.3 s
```

---

## CI/CD Integration

### GitHub Actions Example
```yaml
- name: Run Usage Tracking Unit Tests
  run: dotnet test --filter "FullyQualifiedName~UsageTrackingTests&Category!=Integration&Category!=RealServer"

- name: Run Usage Tracking Integration Tests
  run: dotnet test --filter "FullyQualifiedName~UsageTrackingIntegration&Category=Integration"

- name: Run Usage Tracking Real Server Tests (Optional)
  if: ${{ env.RUN_REAL_SERVER_TESTS == 'true' }}
  env:
    SERVER_URL: ${{ secrets.SERVER_URL }}
    API_KEY: ${{ secrets.API_KEY }}
  run: dotnet test --filter "FullyQualifiedName~RealServerUsageTracking&Category=RealServer"
```

---

## Troubleshooting

### Unit Tests Failing
- Check that Moq is properly configured
- Verify test helper classes are accessible
- Ensure XiansContext is cleaned up between tests

### Integration Tests Failing
- Verify WireMock.Net package is installed
- Check for port conflicts
- Ensure proper cleanup in DisposeAsync

### Real Server Tests Failing
- Verify `.env` file has valid credentials
- Check SERVER_URL is accessible
- Ensure Temporal server is running
- Verify agent/workflow names are unique

---

## Summary

**Complete test coverage** for usage tracking functionality:
- ✅ 28 tests across 3 levels
- ✅ Unit tests for isolated component testing
- ✅ Integration tests with mock HTTP
- ✅ Real server tests for end-to-end validation
- ✅ All following existing Xians.Lib.Tests patterns
- ✅ Zero linting errors
- ✅ Ready for CI/CD

The test suite ensures usage tracking is **reliable**, **resilient**, and **production-ready**! 🚀

