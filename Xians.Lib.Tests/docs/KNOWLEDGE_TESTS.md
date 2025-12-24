# Knowledge SDK Tests

## Overview

Comprehensive test suite for the Knowledge SDK in `Xians.Lib`, covering unit tests, integration tests, and real server tests.

## Test Structure

```
Xians.Lib.Tests/
├── UnitTests/
│   └── Agents/
│       └── KnowledgeCollectionTests.cs         (18 tests)
├── IntegrationTests/
│   ├── Agents/
│   │   └── KnowledgeIntegrationTests.cs        (11 tests)
│   └── RealServer/
│       └── RealServerKnowledgeTests.cs         (10 tests)
```

**Total: 39 tests**

## Test Categories

### 1. Unit Tests (`KnowledgeCollectionTests`)

Tests the `KnowledgeCollection` class in isolation using mocked HTTP services.

**Coverage:**
- ✅ Get knowledge (success, not found, validation)
- ✅ Update knowledge (create, update, validation)
- ✅ Delete knowledge (success, not found)
- ✅ List knowledge (with results, empty)
- ✅ Input validation (null, empty, too long)
- ✅ Error handling (HTTP errors, missing service)
- ✅ Constructor validation

**Test Count: 18**

**Run Command:**
```bash
dotnet test --filter "FullyQualifiedName~KnowledgeCollectionTests"
```

### 2. Integration Tests (`KnowledgeIntegrationTests`)

Tests the full SDK integration using WireMock to simulate server responses.

**Coverage:**
- ✅ Get existing knowledge
- ✅ Get non-existent knowledge
- ✅ Create new knowledge
- ✅ Update existing knowledge
- ✅ Delete knowledge
- ✅ List multiple knowledge items
- ✅ List empty knowledge
- ✅ Special characters in names
- ✅ Full CRUD cycle

**Test Count: 11**

**Run Command:**
```bash
dotnet test --filter "Category=Integration&FullyQualifiedName~KnowledgeIntegrationTests"
```

### 3. Real Server Tests (`RealServerKnowledgeTests`)

Tests against an actual Xians server. Requires valid credentials.

**Coverage:**
- ✅ Create and retrieve knowledge
- ✅ Update knowledge
- ✅ Delete knowledge
- ✅ List knowledge
- ✅ Non-existent knowledge handling
- ✅ Different knowledge types (instruction, json, markdown, text)
- ✅ Large content (10KB)
- ✅ Special characters in names

**Test Count: 10**

**Requirements:**
- Valid `SERVER_URL` environment variable
- Valid `API_KEY` environment variable

**Setup:**
```bash
# Create .env file in Xians.Lib.Tests/
cat > .env << EOF
SERVER_URL=https://your-server.com
API_KEY=your-api-key
EOF
```

**Run Command:**
```bash
dotnet test --filter "Category=RealServer"
```

## Running Tests

### Run All Tests
```bash
cd Xians.Lib.Tests
dotnet test
```

### Run Only Unit Tests
```bash
dotnet test --filter "FullyQualifiedName~UnitTests"
```

### Run Only Integration Tests (Mock Server)
```bash
dotnet test --filter "Category=Integration"
```

### Run Only Real Server Tests
```bash
# Set environment variables first
export SERVER_URL=https://your-server.com
export API_KEY=your-api-key

dotnet test --filter "Category=RealServer"
```

### Run with Detailed Output
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run with Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Test Details

### Unit Tests

#### KnowledgeCollectionTests

| Test | Description |
|------|-------------|
| `GetAsync_WithValidKnowledge_ReturnsKnowledge` | Verifies successful knowledge retrieval |
| `GetAsync_WithNotFound_ReturnsNull` | Verifies 404 handling |
| `GetAsync_WithNullOrEmpty_ThrowsArgumentException` | Validates null/empty input |
| `GetAsync_WithTooLongName_ThrowsArgumentException` | Validates length limits (256 chars) |
| `UpdateAsync_WithValidData_ReturnsTrue` | Verifies successful update |
| `UpdateAsync_WithNullContent_ThrowsArgumentException` | Validates content requirement |
| `UpdateAsync_WithServerError_ThrowsHttpRequestException` | Verifies error handling |
| `DeleteAsync_WithExistingKnowledge_ReturnsTrue` | Verifies successful deletion |
| `DeleteAsync_WithNotFound_ReturnsFalse` | Verifies 404 handling on delete |
| `ListAsync_WithKnowledge_ReturnsList` | Verifies listing multiple items |
| `ListAsync_WithEmptyResult_ReturnsEmptyList` | Verifies empty list handling |
| `Constructor_WithNullAgent_ThrowsArgumentNullException` | Validates constructor |
| `GetAsync_WithNullHttpService_ThrowsInvalidOperationException` | Validates service requirement |

### Integration Tests

#### KnowledgeIntegrationTests

| Test | Description |
|------|-------------|
| `GetAsync_WithExistingKnowledge_ReturnsKnowledge` | End-to-end get with WireMock |
| `GetAsync_WithNonExistentKnowledge_ReturnsNull` | 404 handling with WireMock |
| `UpdateAsync_WithNewKnowledge_CreatesSuccessfully` | Create flow with payload capture |
| `UpdateAsync_WithExistingKnowledge_UpdatesSuccessfully` | Update flow |
| `DeleteAsync_WithExistingKnowledge_DeletesSuccessfully` | Delete flow |
| `DeleteAsync_WithNonExistent_ReturnsFalse` | Delete 404 handling |
| `ListAsync_WithMultipleKnowledge_ReturnsAll` | List multiple items |
| `ListAsync_WithNoKnowledge_ReturnsEmptyList` | Empty list handling |
| `Knowledge_WithSpecialCharactersInName_HandlesCorrectly` | Special chars in names |
| `Knowledge_FullCRUDCycle_WorksCorrectly` | Complete CRUD workflow |

### Real Server Tests

#### RealServerKnowledgeTests

| Test | Description |
|------|-------------|
| `Knowledge_CreateAndGet_WorksWithRealServer` | Create and retrieve on real server |
| `Knowledge_Update_WorksWithRealServer` | Update on real server |
| `Knowledge_Delete_WorksWithRealServer` | Delete on real server |
| `Knowledge_List_WorksWithRealServer` | List on real server |
| `Knowledge_GetNonExistent_ReturnsNull` | 404 handling on real server |
| `Knowledge_DeleteNonExistent_ReturnsFalse` | Delete 404 on real server |
| `Knowledge_DifferentTypes_WorkCorrectly` | Test all knowledge types |
| `Knowledge_LargeContent_WorksCorrectly` | Test with 10KB content |
| `Knowledge_SpecialCharactersInName_WorksCorrectly` | Test special chars on real server |

## Test Data

### Knowledge Types Tested
- `instruction` - Step-by-step instructions
- `json` - JSON configuration
- `markdown` - Markdown documents
- `text` - Plain text

### Special Characters Tested
- Hyphens: `user-123-preference`
- Dots: `config.api.key`
- Underscores: `template_greeting_morning`
- Colons: `user-preference:theme`

### Edge Cases Tested
- ✅ Null/empty names
- ✅ Names > 256 characters
- ✅ Null content
- ✅ Large content (10KB)
- ✅ HTTP errors (500, 404, etc.)
- ✅ Missing HTTP service
- ✅ Non-existent knowledge

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Knowledge Tests

on: [push, pull_request]

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - name: Run Unit Tests
        run: |
          cd Xians.Lib.Tests
          dotnet test --filter "FullyQualifiedName~UnitTests" --logger "trx"
      
  integration-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - name: Run Integration Tests
        run: |
          cd Xians.Lib.Tests
          dotnet test --filter "Category=Integration" --logger "trx"
  
  real-server-tests:
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'  # Only on main branch
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - name: Run Real Server Tests
        env:
          SERVER_URL: ${{ secrets.TEST_SERVER_URL }}
          API_KEY: ${{ secrets.TEST_API_KEY }}
        run: |
          cd Xians.Lib.Tests
          dotnet test --filter "Category=RealServer" --logger "trx"
```

## Troubleshooting

### Real Server Tests Not Running

**Issue**: Tests are skipped
```
Skipping real server tests - credentials not available
```

**Solution**: Set environment variables:
```bash
export SERVER_URL=https://your-server.com
export API_KEY=your-api-key
```

Or create `.env` file in `Xians.Lib.Tests/`:
```
SERVER_URL=https://your-server.com
API_KEY=your-api-key
```

### WireMock Port Conflicts

**Issue**: Integration tests fail with port binding errors

**Solution**: WireMock automatically selects available ports. If issues persist, restart your machine or kill conflicting processes.

### Authentication Failures

**Issue**: Real server tests fail with 401/403 errors

**Solution**: 
1. Verify API_KEY is valid
2. Check if key has expired
3. Ensure key has proper permissions for knowledge operations

## Test Cleanup

Real server tests automatically clean up created knowledge items. If tests are interrupted:

```bash
# Use the list endpoint to find test knowledge
# They all start with "test-" prefix
```

## Future Test Improvements

- [ ] Add performance tests (latency, throughput)
- [ ] Add concurrent access tests
- [ ] Add caching behavior tests (when implemented)
- [ ] Add tenant isolation tests
- [ ] Add pagination tests (when list pagination is added)
- [ ] Add stress tests with very large content (>1MB)
- [ ] Add malicious input tests (SQL injection, XSS)

## Related Documentation

- [Knowledge SDK Guide](../../Xians.Lib/docs/KnowledgeSDK.md)
- [Server API Requirements](../../Xians.Lib/docs/KnowledgeAPI.md)
- [Test Types Guide](./TEST_TYPES.md)

