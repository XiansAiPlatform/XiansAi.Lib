using Xians.Lib.Common.MultiTenancy;
using Xians.Lib.Common.MultiTenancy.Exceptions;

namespace Xians.Lib.Tests.UnitTests.Common;

/// <summary>
/// Unit tests to verify task queue name generation for system-scoped vs tenant-scoped agents.
/// These tests verify the core logic WITHOUT requiring a running server.
/// </summary>
public class TaskQueueBehaviorTests
{
    private const string WORKFLOW_TYPE = "TestAgent:TestWorkflow";
    private const string TENANT_ID = "acme-corp";

    [Fact]
    public void SystemScoped_True_TaskQueue_HasNoTenantPrefix()
    {
        // Arrange
        var systemScoped = true;
        
        // Act
        var taskQueue = TenantContext.GetTaskQueueName(WORKFLOW_TYPE, systemScoped, TENANT_ID);
        
        // Assert
        Assert.Equal(WORKFLOW_TYPE, taskQueue);
        Assert.DoesNotContain(TENANT_ID, taskQueue);
        
        Console.WriteLine($"✅ System-scoped task queue: {taskQueue}");
        Console.WriteLine($"   ↳ No tenant prefix, worker pool shared across tenants");
    }

    [Fact]
    public void SystemScoped_False_TaskQueue_HasTenantPrefix()
    {
        // Arrange
        var systemScoped = false;
        
        // Act
        var taskQueue = TenantContext.GetTaskQueueName(WORKFLOW_TYPE, systemScoped, TENANT_ID);
        
        // Assert
        Assert.Equal($"{TENANT_ID}:{WORKFLOW_TYPE}", taskQueue);
        Assert.StartsWith(TENANT_ID, taskQueue);
        
        Console.WriteLine($"✅ Tenant-scoped task queue: {taskQueue}");
        Console.WriteLine($"   ↳ Tenant prefix ensures worker isolation");
    }

    [Fact]
    public void SystemScoped_False_NoTenantId_ThrowsException()
    {
        // Arrange
        var systemScoped = false;
        string? tenantId = null;
        
        // Act & Assert
        var exception = Assert.Throws<TenantIsolationException>(() =>
            TenantContext.GetTaskQueueName(WORKFLOW_TYPE, systemScoped, tenantId));
        
        Assert.Contains("TenantId is required", exception.Message);
        
        Console.WriteLine($"✅ Tenant-scoped with null tenant throws exception (as expected)");
    }

    [Fact]
    public void TenantValidation_SystemScoped_AlwaysPasses()
    {
        // Arrange
        var systemScoped = true;
        var workflowTenantId = "tenant-a";
        var expectedTenantId = "tenant-b"; // Different!
        
        // Act
        var isValid = TenantContext.ValidateTenantIsolation(
            workflowTenantId, 
            expectedTenantId, 
            systemScoped);
        
        // Assert
        Assert.True(isValid);
        
        Console.WriteLine($"✅ System-scoped: validation passes even with mismatched tenants");
        Console.WriteLine($"   ↳ Workflow tenant: {workflowTenantId}");
        Console.WriteLine($"   ↳ Expected tenant: {expectedTenantId}");
        Console.WriteLine($"   ↳ Result: PASS (validation skipped for system-scoped)");
    }

    [Fact]
    public void TenantValidation_TenantScoped_MatchingTenants_Passes()
    {
        // Arrange
        var systemScoped = false;
        var workflowTenantId = "acme-corp";
        var expectedTenantId = "acme-corp"; // Same!
        
        // Act
        var isValid = TenantContext.ValidateTenantIsolation(
            workflowTenantId, 
            expectedTenantId, 
            systemScoped);
        
        // Assert
        Assert.True(isValid);
        
        Console.WriteLine($"✅ Tenant-scoped: validation passes with matching tenants");
        Console.WriteLine($"   ↳ Workflow tenant: {workflowTenantId}");
        Console.WriteLine($"   ↳ Expected tenant: {expectedTenantId}");
        Console.WriteLine($"   ↳ Result: PASS");
    }

    [Fact]
    public void TenantValidation_TenantScoped_MismatchedTenants_Fails()
    {
        // Arrange
        var systemScoped = false;
        var workflowTenantId = "test"; // From default test utils
        var expectedTenantId = "acme-corp"; // From API key
        
        // Act
        var isValid = TenantContext.ValidateTenantIsolation(
            workflowTenantId, 
            expectedTenantId, 
            systemScoped);
        
        // Assert
        Assert.False(isValid);
        
        Console.WriteLine($"❌ Tenant-scoped: validation FAILS with mismatched tenants");
        Console.WriteLine($"   ↳ Workflow tenant: {workflowTenantId}");
        Console.WriteLine($"   ↳ Expected tenant: {expectedTenantId}");
        Console.WriteLine($"   ↳ Result: FAIL (THIS WAS THE BUG!)");
    }

    [Fact]
    public void WorkflowId_AlwaysHasTenantPrefix_RegardlessOfSystemScoped()
    {
        // This demonstrates that workflow IDs ALWAYS have tenant prefix
        // The tenantId parameter in workflow ID is separate from systemScoped
        
        var tenantId = "acme-corp";
        var workflowType = "TestAgent:TestWorkflow";
        
        // Both system-scoped and tenant-scoped use the same workflow ID format
        var workflowId = $"{tenantId}:{workflowType}";
        
        // Extract tenant from workflow ID (this always works)
        var extractedTenantId = TenantContext.ExtractTenantId(workflowId);
        
        Assert.Equal(tenantId, extractedTenantId);
        
        Console.WriteLine($"✅ Workflow IDs always have tenant prefix: {workflowId}");
        Console.WriteLine($"   ↳ Extracted tenant: {extractedTenantId}");
        Console.WriteLine($"   ↳ SystemScoped flag does NOT affect workflow ID format!");
    }

    [Theory]
    [InlineData(true, "TestAgent:TestWorkflow")]  // System-scoped
    [InlineData(false, "acme-corp:TestAgent:TestWorkflow")]  // Tenant-scoped
    public void TaskQueue_Format_DependsOnSystemScoped(bool systemScoped, string expectedTaskQueue)
    {
        // Act
        var taskQueue = TenantContext.GetTaskQueueName(
            "TestAgent:TestWorkflow", 
            systemScoped, 
            "acme-corp");
        
        // Assert
        Assert.Equal(expectedTaskQueue, taskQueue);
        
        Console.WriteLine($"✅ SystemScoped={systemScoped} → Task Queue: {taskQueue}");
    }
}
