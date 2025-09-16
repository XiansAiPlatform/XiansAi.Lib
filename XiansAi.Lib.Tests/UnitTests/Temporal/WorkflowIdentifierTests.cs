using Temporal;

namespace Agentri.SDK.Tests.UnitTests.Temporal
{
    /*
    dotnet test --filter "FullyQualifiedName~WorkflowIdentifierTests"
    */
    public class WorkflowIdentifierTests
    {
        private const string TestTenantId = "test-tenant";
        private const string TestAgentName = "My Agent v1.3.1";
        private const string TestFlowName = "Router Bot";
        private const string TestIdPostfix = "ebbb57bd-8428-458f-9618-d8fe3bef103c";
        private const string TestWorkflowType = TestAgentName + ":" + TestFlowName;
        private const string TestWorkflowIdWithPostfix = TestTenantId + ":" + TestAgentName + ":" + TestFlowName + ":" + TestIdPostfix;
        private const string TestWorkflowIdWithoutPostfix = TestTenantId + ":" + TestWorkflowType;

        [Fact]
        public void Constructor_WithWorkflowIdWithPostfix_SetsAllPropertiesCorrectly()
        {
            // Arrange & Act
            var identifier = new WorkflowIdentifier(TestWorkflowIdWithPostfix, TestTenantId);

            // Assert
            Assert.Equal(TestWorkflowIdWithPostfix, identifier.WorkflowId);
            Assert.Equal(TestWorkflowType, identifier.WorkflowType);
            Assert.Equal(TestAgentName, identifier.AgentName);
        }

        [Fact]
        public void Constructor_WithWorkflowIdWithoutPostfix_SetsAllPropertiesCorrectly()
        {
            // Arrange & Act
            var identifier = new WorkflowIdentifier(TestWorkflowIdWithoutPostfix, TestTenantId);

            // Assert
            Assert.Equal(TestWorkflowIdWithoutPostfix, identifier.WorkflowId);
            Assert.Equal(TestWorkflowType, identifier.WorkflowType);
            Assert.Equal(TestAgentName, identifier.AgentName);
        }

        [Fact]
        public void Constructor_WithWorkflowType_SetsAllPropertiesCorrectly()
        {
            // Arrange & Act
            var identifier = new WorkflowIdentifier(TestWorkflowType, TestTenantId);

            // Assert
            Assert.Equal(TestWorkflowIdWithoutPostfix, identifier.WorkflowId);
            Assert.Equal(TestWorkflowType, identifier.WorkflowType);
            Assert.Equal(TestAgentName, identifier.AgentName);
        }

        [Fact]
        public void Constructor_WithWorkflowIdWithPostfix_WrongTenantId_ThrowsException()
        {
            // Arrange
            var wrongTenantId = "wrong-tenant";
            
            // Act & Assert
            var exception = Assert.Throws<Exception>(() => 
                new WorkflowIdentifier(TestWorkflowIdWithPostfix, wrongTenantId));
            
            Assert.Contains($"Invalid workflow identifier `{TestWorkflowIdWithPostfix}`", exception.Message);
            Assert.Contains($"Expected to start with tenant id `{wrongTenantId}`", exception.Message);
        }

        [Fact]
        public void Constructor_WithWorkflowIdWithoutPostfix_WrongTenantId_ThrowsException()
        {
            // Arrange
            var wrongTenantId = "wrong-tenant";
            
            // Act & Assert
            var exception = Assert.Throws<Exception>(() => 
                new WorkflowIdentifier(TestWorkflowIdWithoutPostfix, wrongTenantId));
            
            Assert.Contains($"Invalid workflow identifier `{TestWorkflowIdWithoutPostfix}`", exception.Message);
            Assert.Contains($"Expected to start with tenant id `{wrongTenantId}`", exception.Message);
        }

        [Fact]
        public void Constructor_WithMinimalWorkflowId_ThreeColons_ParsesCorrectly()
        {
            // Arrange
            var minimalWorkflowId = "tenant:agent:flow:id";
            
            // Act
            var identifier = new WorkflowIdentifier(minimalWorkflowId, "tenant");

            // Assert
            Assert.Equal(minimalWorkflowId, identifier.WorkflowId);
            Assert.Equal("agent:flow", identifier.WorkflowType);
            Assert.Equal("agent", identifier.AgentName);
        }

        [Fact]
        public void Constructor_WithExtraColonsInWorkflowId_ParsesCorrectly()
        {
            // Arrange
            var workflowIdWithExtraColons = "tenant:agent:flow-name:id-with:colons";
            
            // Act
            var identifier = new WorkflowIdentifier(workflowIdWithExtraColons, "tenant");

            // Assert
            Assert.Equal(workflowIdWithExtraColons, identifier.WorkflowId);
            Assert.Equal("agent:flow-name", identifier.WorkflowType);
            Assert.Equal("agent", identifier.AgentName);
        }

        [Fact]
        public void GetWorkflowId_WithWorkflowType_ReturnsWorkflowIdWithoutPostfix()
        {
            // Act
            var result = WorkflowIdentifier.GetWorkflowId(TestWorkflowType, TestTenantId);

            // Assert
            Assert.Equal(TestWorkflowIdWithoutPostfix, result);
            Assert.Equal(TestTenantId + ":" + TestWorkflowType, result);
            // Verify it does NOT include the postfix
            Assert.DoesNotContain(TestIdPostfix, result);
        }

        [Fact]
        public void GetWorkflowType_WithWorkflowIdWithPostfix_ReturnsCorrectType()
        {
            // Act
            var result = WorkflowIdentifier.GetWorkflowType(TestWorkflowIdWithPostfix);

            // Assert
            Assert.Equal(TestWorkflowType, result);
        }

        [Fact]
        public void GetWorkflowType_WithWorkflowIdWithoutPostfix_ReturnsCorrectType()
        {
            // Act
            var result = WorkflowIdentifier.GetWorkflowType(TestWorkflowIdWithoutPostfix);

            // Assert
            Assert.Equal(TestWorkflowType, result);
        }

        [Fact]
        public void GetWorkflowType_WithWorkflowType_ReturnsItself()
        {
            // Act
            var result = WorkflowIdentifier.GetWorkflowType(TestWorkflowType);

            // Assert
            Assert.Equal(TestWorkflowType, result);
        }

        [Fact]
        public void GetWorkflowType_WithInvalidFormat_ThrowsException()
        {
            // Arrange
            var invalidWorkflow = "no-colons";
            
            // Act & Assert
            var exception = Assert.Throws<Exception>(() => 
                WorkflowIdentifier.GetWorkflowType(invalidWorkflow));
            
            Assert.Contains($"Invalid workflow identifier `{invalidWorkflow}`", exception.Message);
            Assert.Contains("Expected to have at least 1 `:`", exception.Message);
        }

        [Fact]
        public void GetAgentName_WithWorkflowType_ReturnsCorrectAgentName()
        {
            // Act
            var result = WorkflowIdentifier.GetAgentName(TestWorkflowType);

            // Assert
            Assert.Equal(TestAgentName, result);
        }

        [Fact]
        public void GetAgentName_WithWorkflowIdWithPostfix_ReturnsCorrectAgentName()
        {
            // Act
            var result = WorkflowIdentifier.GetAgentName(TestWorkflowIdWithPostfix);

            // Assert
            Assert.Equal(TestTenantId, result); // First part of workflow ID is tenant, not agent
        }

        [Fact]
        public void GetAgentName_WithWorkflowIdWithoutPostfix_ReturnsCorrectAgentName()
        {
            // Act
            var result = WorkflowIdentifier.GetAgentName(TestWorkflowIdWithoutPostfix);

            // Assert
            Assert.Equal(TestTenantId, result); // First part of workflow ID is tenant, not agent
        }

        [Fact]
        public void GetAgentName_WithInvalidFormat_ThrowsException()
        {
            // Arrange
            var invalidWorkflow = "no-colons";
            
            // Act & Assert
            var exception = Assert.Throws<Exception>(() => 
                WorkflowIdentifier.GetAgentName(invalidWorkflow));
            
            Assert.Contains($"Invalid workflow identifier `{invalidWorkflow}`", exception.Message);
            Assert.Contains("Expected to have at least 1 `:`", exception.Message);
        }

        [Theory]
        [InlineData("custom-tenant")]
        [InlineData("production")]
        [InlineData("dev-environment")]
        public void Constructor_WithDifferentTenantIds_WorksCorrectly(string tenantId)
        {
            // Arrange
            var workflowType = "Agent:Flow";
            
            // Act
            var identifier = new WorkflowIdentifier(workflowType, tenantId);

            // Assert
            Assert.Equal(tenantId + ":" + workflowType, identifier.WorkflowId);
            Assert.Equal(workflowType, identifier.WorkflowType);
            Assert.Equal("Agent", identifier.AgentName);
        }

        [Fact]
        public void Constructor_WithComplexAgentAndFlowNames_ParsesCorrectly()
        {
            // Arrange
            var complexAgentName = "Complex Agent Name v2.1.0-beta";
            var complexFlowName = "Multi Word Flow Name";
            var complexWorkflowType = complexAgentName + ":" + complexFlowName;
            
            // Act
            var identifier = new WorkflowIdentifier(complexWorkflowType, TestTenantId);

            // Assert
            Assert.Equal(TestTenantId + ":" + complexWorkflowType, identifier.WorkflowId);
            Assert.Equal(complexWorkflowType, identifier.WorkflowType);
            Assert.Equal(complexAgentName, identifier.AgentName);
        }

        [Fact]
        public void WorkflowId_TwoVariants_DemonstratesDifference()
        {
            // This test demonstrates the two WorkflowId formats:
            // 1. With postfix: tenant:agent:flow:postfix (full WorkflowId)
            // 2. Without postfix: tenant:agent:flow (generated by GetWorkflowId method)
            
            // Arrange & Act
            var identifierFromFullId = new WorkflowIdentifier(TestWorkflowIdWithPostfix, TestTenantId);
            var identifierFromType = new WorkflowIdentifier(TestWorkflowType, TestTenantId);
            var generatedId = WorkflowIdentifier.GetWorkflowId(TestWorkflowType, TestTenantId);

            // Assert
            // Both should have the same WorkflowType and AgentName
            Assert.Equal(TestWorkflowType, identifierFromFullId.WorkflowType);
            Assert.Equal(TestWorkflowType, identifierFromType.WorkflowType);
            Assert.Equal(TestAgentName, identifierFromFullId.AgentName);
            Assert.Equal(TestAgentName, identifierFromType.AgentName);
            
            // But different WorkflowIds
            Assert.Equal(TestWorkflowIdWithPostfix, identifierFromFullId.WorkflowId);
            Assert.Equal(TestWorkflowIdWithoutPostfix, identifierFromType.WorkflowId);
            Assert.Equal(TestWorkflowIdWithoutPostfix, generatedId);
            
            // The generated ID should NOT contain the postfix
            Assert.DoesNotContain(TestIdPostfix, generatedId);
            Assert.Contains(TestIdPostfix, TestWorkflowIdWithPostfix);
        }
    }
}
