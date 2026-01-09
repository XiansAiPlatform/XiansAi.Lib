# Xians.Lib Testing Utilities

This directory contains testing utilities that help ensure test isolation and prevent test contamination.

## Problem Statement

Xians.Lib uses static state in several places for performance and ambient context:
- Agent and workflow registries (`XiansContext`)
- Workflow handlers (`BuiltinWorkflow`)
- Static activity services (`KnowledgeActivities`)
- Caches (settings, certificates)

Without proper cleanup between tests, this static state can cause:
- ❌ Test contamination (one test affects another)
- ❌ Non-deterministic test failures
- ❌ Tests that pass individually but fail when run together

## Solution

### 1. `TestCleanup` - Centralized Cleanup

Single point for resetting all static state:

```csharp
using Xians.Lib.Common.Testing;

[Fact]
public void MyTest()
{
    // Clean state before test
    TestCleanup.ResetAllStaticState();
    
    // Your test code
    
    // Clean state after test (optional but recommended)
    TestCleanup.ResetAllStaticState();
}
```

**Available Methods:**
- `ResetAllStaticState()` - Full cleanup (recommended)
- `ResetWorkflowState()` - Workflow registries only
- `ResetCaches()` - Cache state only

### 2. `XiansTestFixture` - Automatic Cleanup Base Class

For synchronous tests:

```csharp
using Xians.Lib.Common.Testing;

public class MyTests : XiansTestFixture
{
    [Fact]
    public void MyTest()
    {
        // Static state is automatically clean
        // Test code here
    }
    // Cleanup happens automatically in Dispose
}
```

### 3. `XiansAsyncTestFixture` - Async Test Support

For xUnit `IAsyncLifetime` tests:

```csharp
using Xians.Lib.Common.Testing;
using Xunit;

public class MyAsyncTests : XiansAsyncTestFixture, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await base.InitializeAsync();  // Clean state
        // Your async setup
    }
    
    public async Task DisposeAsync()
    {
        // Your async cleanup
        await base.DisposeAsync();  // Clean static state
    }
    
    [Fact]
    public async Task MyTest()
    {
        // Test code
    }
}
```

## Migration Guide

### Before (Manual Cleanup)

```csharp
public class MyTests
{
    [Fact]
    public void Test1()
    {
        // Manual cleanup scattered everywhere
        XiansContext.CleanupForTests();
        BuiltinWorkflow.ClearHandlersForTests();
        KnowledgeActivities.ClearStaticServicesForTests();
        SettingsService.ResetCache();
        CertificateCache.Clear();
        
        // Test code
    }
}
```

### After (Automatic)

```csharp
public class MyTests : XiansTestFixture
{
    [Fact]
    public void Test1()
    {
        // Clean state automatically!
        // Test code
    }
}
```

## Best Practices

### ✅ DO:
- Inherit from `XiansTestFixture` for simple tests
- Use `TestCleanup.ResetAllStaticState()` in setup/teardown
- Call `base.Dispose(disposing)` when overriding Dispose
- Use `XiansAsyncTestFixture` for async initialization

### ❌ DON'T:
- Call individual cleanup methods manually (use centralized)
- Forget to cleanup in teardown
- Share state between tests
- Use `TestCleanup` in production code (tests only!)

## Example: Real Server Tests

```csharp
public abstract class RealServerTestBase : IDisposable
{
    protected RealServerTestBase()
    {
        // Clean state before test
        TestCleanup.ResetAllStaticState();
        
        // Load credentials...
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            TestCleanup.ResetAllStaticState();
        }
    }
}

public class MyRealServerTests : RealServerTestBase
{
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Custom cleanup
        }
        base.Dispose(disposing);  // Important!
    }
}
```

## Performance

- **Fast**: Static cleanup is O(1) for each registry/cache
- **Safe**: No impact on production code
- **Isolated**: Each test gets clean state

## Troubleshooting

### Tests still contaminating each other?

Check that you're calling cleanup in BOTH setup and teardown:

```csharp
public MyTest()
{
    TestCleanup.ResetAllStaticState();  // Before test
}

public void Dispose()
{
    TestCleanup.ResetAllStaticState();  // After test
}
```

### "Object reference not set" errors?

Ensure you're initializing your platform/agents AFTER cleanup:

```csharp
public MyTest()
{
    TestCleanup.ResetAllStaticState();  // Clean first
    _platform = XiansPlatform.Initialize(...);  // Then initialize
}
```

---

## ⚠️ Important Note

These utilities are **for testing only**. Never use `TestCleanup` in production code as it resets global state which would break running applications.
