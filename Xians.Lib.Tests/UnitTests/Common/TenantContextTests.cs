using Xunit;
using Xians.Lib.Common.MultiTenancy.Exceptions;
using Xians.Lib.Common.MultiTenancy;
using Xians.Lib.Common.Caching;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Common.Models;
using Xians.Lib.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Xians.Lib.Tests.UnitTests.Common;

public class TenantContextTests
{
    [Theory]
    [InlineData("acme-corp:CustomerService:uuid-123", "acme-corp")]
    [InlineData("contoso:GlobalNotifications:Alerts:uuid-456", "contoso")]
    [InlineData("tenant-123:MyAgent:Flow", "tenant-123")]
    [InlineData("a:b:c:d:e:f", "a")] // Multiple colons
    public void ExtractTenantId_ValidWorkflowId_ReturnsTenantId(string workflowId, string expectedTenantId)
    {
        // Act
        var tenantId = TenantContext.ExtractTenantId(workflowId);

        // Assert
        Assert.Equal(expectedTenantId, tenantId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void ExtractTenantId_NullOrEmptyWorkflowId_ThrowsException(string? workflowId)
    {
        // Act & Assert
        var exception = Assert.Throws<WorkflowException>(() =>
            TenantContext.ExtractTenantId(workflowId!));
        
        Assert.Contains("cannot be null or empty", exception.Message);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("no-colons-here")]
    public void ExtractTenantId_InvalidFormat_ThrowsException(string workflowId)
    {
        // Act & Assert
        var exception = Assert.Throws<WorkflowException>(() =>
            TenantContext.ExtractTenantId(workflowId));
        
        Assert.Contains("Invalid WorkflowId format", exception.Message);
        Assert.Contains(workflowId, exception.Message);
    }

    [Fact]
    public void ExtractTenantId_MinimalFormat_ReturnsTenantId()
    {
        // Arrange - Minimal valid format is TenantId:WorkflowType
        var workflowId = "tenant:workflow";

        // Act
        var tenantId = TenantContext.ExtractTenantId(workflowId);

        // Assert
        Assert.Equal("tenant", tenantId);
    }

    [Theory]
    [InlineData("acme-corp:CustomerService:s:uuid-123", "CustomerService")]
    [InlineData("contoso:GlobalNotifications:Alerts:uuid-456", "GlobalNotifications")]
    [InlineData("tenant-123:MyAgent:Flow", "MyAgent")]
    public void ExtractWorkflowType_ValidWorkflowId_ReturnsWorkflowType(string workflowId, string expectedWorkflowType)
    {
        // Act
        var workflowType = TenantContext.ExtractWorkflowType(workflowId);

        // Assert
        Assert.Equal(expectedWorkflowType, workflowType);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void ExtractWorkflowType_NullOrEmptyWorkflowId_ThrowsException(string? workflowId)
    {
        // Act & Assert
        var exception = Assert.Throws<WorkflowException>(() =>
            TenantContext.ExtractWorkflowType(workflowId!));
        
        Assert.Contains("cannot be null or empty", exception.Message);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("no-colons-here")]
    public void ExtractWorkflowType_InvalidFormat_ThrowsException(string workflowId)
    {
        // Act & Assert
        var exception = Assert.Throws<WorkflowException>(() =>
            TenantContext.ExtractWorkflowType(workflowId));
        
        Assert.Contains("Invalid WorkflowId format", exception.Message);
    }

    [Fact]
    public void ExtractWorkflowType_MinimalFormat_ReturnsWorkflowType()
    {
        // Arrange - Minimal valid format is TenantId:WorkflowType
        var workflowId = "tenant:workflow";

        // Act
        var workflowType = TenantContext.ExtractWorkflowType(workflowId);

        // Assert
        Assert.Equal("workflow", workflowType);
    }

    [Fact]
    public void GetTaskQueueName_SystemScoped_ReturnsWorkflowType()
    {
        // Arrange
        var workflowType = "GlobalNotifications:BuiltIn Workflow";

        // Act
        var taskQueue = TenantContext.GetTaskQueueName(
            workflowType, 
            systemScoped: true, 
            tenantId: null);

        // Assert
        Assert.Equal(workflowType, taskQueue);
    }

    [Fact]
    public void GetTaskQueueName_NonSystemScoped_ReturnsTenantIdColonWorkflowType()
    {
        // Arrange
        var workflowType = "CustomerService:BuiltIn Workflow";
        var tenantId = "acme-corp";

        // Act
        var taskQueue = TenantContext.GetTaskQueueName(
            workflowType, 
            systemScoped: false, 
            tenantId: tenantId);

        // Assert
        Assert.Equal("acme-corp:CustomerService:BuiltIn Workflow", taskQueue);
    }

    [Fact]
    public void GetTaskQueueName_NonSystemScopedWithoutTenantId_ThrowsException()
    {
        // Arrange
        var workflowType = "CustomerService:BuiltIn Workflow";

        // Act & Assert
        var exception = Assert.Throws<TenantIsolationException>(() =>
            TenantContext.GetTaskQueueName(workflowType, systemScoped: false, tenantId: null));
        
        Assert.Contains("TenantId is required", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void GetTaskQueueName_NonSystemScopedWithEmptyTenantId_ThrowsException(string emptyTenantId)
    {
        // Arrange
        var workflowType = "CustomerService:BuiltIn Workflow";

        // Act & Assert
        var exception = Assert.Throws<TenantIsolationException>(() =>
            TenantContext.GetTaskQueueName(workflowType, systemScoped: false, tenantId: emptyTenantId));
        
        Assert.Contains("TenantId is required", exception.Message);
    }

    [Fact]
    public void ValidateTenantIsolation_SystemScoped_AlwaysReturnsTrue()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();

        // Act & Assert - Different tenants should still pass for system-scoped
        Assert.True(TenantContext.ValidateTenantIsolation(
            workflowTenantId: "tenant-1", 
            expectedTenantId: "tenant-2", 
            systemScoped: true,
            logger: mockLogger.Object));

        Assert.True(TenantContext.ValidateTenantIsolation(
            workflowTenantId: "any-tenant", 
            expectedTenantId: null, 
            systemScoped: true,
            logger: mockLogger.Object));
    }

    [Fact]
    public void ValidateTenantIsolation_NonSystemScopedMatchingTenant_ReturnsTrue()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var tenantId = "acme-corp";

        // Act
        var result = TenantContext.ValidateTenantIsolation(
            workflowTenantId: tenantId, 
            expectedTenantId: tenantId, 
            systemScoped: false,
            logger: mockLogger.Object);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateTenantIsolation_NonSystemScopedMismatchedTenant_ReturnsFalse()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();

        // Act
        var result = TenantContext.ValidateTenantIsolation(
            workflowTenantId: "tenant-1", 
            expectedTenantId: "tenant-2", 
            systemScoped: false,
            logger: mockLogger.Object);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateTenantIsolation_WithLogger_LogsAppropriateMessages()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();

        // Act - System scoped
        TenantContext.ValidateTenantIsolation(
            workflowTenantId: "tenant-1", 
            expectedTenantId: "tenant-2", 
            systemScoped: true,
            logger: mockLogger.Object);

        // Assert - Should log debug for system-scoped
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("System-scoped")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Reset
        mockLogger.Reset();

        // Act - Non-system scoped with mismatch
        TenantContext.ValidateTenantIsolation(
            workflowTenantId: "tenant-1", 
            expectedTenantId: "tenant-2", 
            systemScoped: false,
            logger: mockLogger.Object);

        // Assert - Should log error for mismatch
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("isolation violation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Parse_ValidWorkflowId_ReturnsWorkflowIdentifier()
    {
        // Arrange
        var workflowId = "acme-corp:CustomerService:BuiltIn Workflow:uuid-123";

        // Act
        var identifier = TenantContext.Parse(workflowId);

        // Assert
        Assert.NotNull(identifier);
        Assert.Equal("acme-corp", identifier.TenantId);
        Assert.Equal("CustomerService", identifier.WorkflowType);
        Assert.Equal(workflowId, identifier.WorkflowId);
    }

    [Fact]
    public void Parse_InvalidWorkflowId_ThrowsException()
    {
        // Arrange
        var invalidWorkflowId = "invalid-format";

        // Act & Assert
        Assert.Throws<WorkflowException>(() =>
            TenantContext.Parse(invalidWorkflowId));
    }

    [Fact]
    public void WorkflowIdentifier_ToString_ReturnsFormattedString()
    {
        // Arrange
        var workflowId = "acme-corp:CustomerService:BuiltIn Workflow:uuid-123";
        var identifier = TenantContext.Parse(workflowId);

        // Act
        var result = identifier.ToString();

        // Assert
        Assert.Contains("WorkflowId=", result);
        Assert.Contains("TenantId=", result);
        Assert.Contains("WorkflowType=", result);
        Assert.Contains("acme-corp", result);
        Assert.Contains("CustomerService", result);
    }

    [Theory]
    [InlineData("tenant-with-dashes:Agent:Flow")]
    [InlineData("tenant_with_underscores:Agent:Flow")]
    [InlineData("tenant.with.dots:Agent:Flow")]
    [InlineData("TENANT-UPPERCASE:Agent:Flow")]
    public void ExtractTenantId_VariousTenantIdFormats_ExtractsCorrectly(string workflowId)
    {
        // Act
        var tenantId = TenantContext.ExtractTenantId(workflowId);

        // Assert
        Assert.Equal(workflowId.Split(':')[0], tenantId);
    }

    [Fact]
    public void GetTaskQueueName_SystemScopedWithTenantId_IgnoresTenantId()
    {
        // Arrange
        var workflowType = "GlobalNotifications:BuiltIn Workflow";
        var tenantId = "some-tenant";

        // Act - System-scoped should ignore tenantId even if provided
        var taskQueue = TenantContext.GetTaskQueueName(
            workflowType, 
            systemScoped: true, 
            tenantId: tenantId);

        // Assert - Should only return workflowType, not include tenantId
        Assert.Equal(workflowType, taskQueue);
        Assert.DoesNotContain(tenantId, taskQueue);
    }

    [Fact]
    public void GetTaskQueueName_PlatformWorkflow_ReplacesWithAgentName_SystemScoped()
    {
        // Arrange
        var workflowType = "Platform:Task Workflow";

        // Act
        var taskQueue = TenantContext.GetTaskQueueName(
            workflowType,
            systemScoped: true,
            tenantId: "hasith");

        // Assert - Platform should be replaced with agent name; Task Workflow gets hitl_task: prefix
        Assert.Equal("hitl_task:Platform:Task Workflow", taskQueue);
    }

    [Fact]
    public void GetTaskQueueName_PlatformWorkflow_ReplacesWithAgentName_NonSystemScoped()
    {
        // Arrange
        var workflowType = "TestAgent:Builtin Workflow";
        var tenantId = "tenant-123";

        // Act
        var taskQueue = TenantContext.GetTaskQueueName(
            workflowType,
            systemScoped: false,
            tenantId: tenantId);

        // Assert - Should include tenant ID and replace Platform with agent name
        Assert.Equal("tenant-123:TestAgent:Builtin Workflow", taskQueue);
    }

    [Fact]
    public void GetTaskQueueName_PlatformWorkflow_WithoutAgentName_KeepsPlatform()
    {
        // Arrange
        var workflowType = "MyAgent:Task Workflow";

        // Act
        var taskQueue = TenantContext.GetTaskQueueName(
            workflowType,
            systemScoped: true,
            tenantId: null);

        // Assert - Should keep workflow type; Task Workflow gets hitl_task: prefix
        Assert.Equal("hitl_task:MyAgent:Task Workflow", taskQueue);
    }

    [Fact]
    public void GetTaskQueueName_BuiltinWorkflow_WithName_AppendsNameWithDash()
    {
        // Arrange
        var workflowType = "MyAgent:Builtin Workflow";

        // Act
        var taskQueue = TenantContext.GetTaskQueueName(
            workflowType,
            systemScoped: true,
            tenantId: null);

        // Assert - Should replace Platform and append workflow name
        Assert.Equal("MyAgent:Builtin Workflow", taskQueue);
    }

    [Fact]
    public void GetTaskQueueName_BuiltinWorkflow_WithName_NonSystemScoped()
    {
        // Arrange
        var workflowType = "MyAgent:Builtin Workflow";
        var tenantId = "tenant-123";

        // Act
        var taskQueue = TenantContext.GetTaskQueueName(
            workflowType,
            systemScoped: false,
            tenantId: tenantId);

        // Assert - Should include tenant ID, replace Platform, and append workflow name
        Assert.Equal("tenant-123:MyAgent:Builtin Workflow", taskQueue);
    }

    [Fact]
    public void GetTaskQueueName_BuiltinWorkflow_WithoutName_NoSuffix()
    {
        // Arrange
        var workflowType = "MyAgent:Builtin Workflow";

        // Act
        var taskQueue = TenantContext.GetTaskQueueName(
            workflowType,
            systemScoped: true,
            tenantId: null);

        // Assert - Should replace Platform but not append anything
        Assert.Equal("MyAgent:Builtin Workflow", taskQueue);
    }

    [Fact]
    public void GetTaskQueueName_TaskWorkflow_WithName_IgnoresName()
    {
        // Arrange
        var workflowType = "Platform:Task Workflow";

        // Act
        var taskQueue = TenantContext.GetTaskQueueName(
            workflowType,
            systemScoped: true,
            tenantId: null);

        // Assert - Only Builtin Workflow type should append name, not Task Workflow; Task Workflow gets hitl_task: prefix
        Assert.Equal("hitl_task:Platform:Task Workflow", taskQueue);
    }
}

