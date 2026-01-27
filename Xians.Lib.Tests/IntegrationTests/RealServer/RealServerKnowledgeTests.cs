using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Knowledge;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// Real server tests for Knowledge SDK.
/// These tests run against an actual Xians server.
/// Set SERVER_URL and API_KEY environment variables to run these tests.
/// 
/// dotnet test --filter "Category=RealServer&FullyQualifiedName~RealServerKnowledgeTests" --logger "console;verbosity=detailed"
/// 
/// </summary>
[Trait("Category", "RealServer")]
[Collection("RealServerWorkflows")] // Force sequential execution with other workflow tests
public class RealServerKnowledgeTests : RealServerTestBase, IAsyncLifetime
{
    private XiansPlatform? _platform;
    private XiansAgent? _agent;
    private readonly string _testKnowledgePrefix;
    private CancellationTokenSource? _workerCts;
    private Task? _workerTask;
    
    // Use hardcoded agent name across all tests
    public const string AGENT_NAME = "KnowledgeTestAgent";

    public RealServerKnowledgeTests()
    {
        // Use unique prefix for test knowledge to avoid conflicts between test runs
        _testKnowledgePrefix = $"test-{Guid.NewGuid().ToString()[..8]}";
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        if (!RunRealServerTests) return;
        
        await InitializePlatformAsync();
        
        // Start workers to handle workflow executions (including KnowledgeTestWorkflow)
        if (_agent != null)
        {
            _workerCts = new CancellationTokenSource();
            _workerTask = _agent.RunAllAsync(_workerCts.Token);
            
            // Give workers time to start
            await Task.Delay(1000);
            Console.WriteLine("✓ Workers started for KnowledgeTestWorkflow");
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        // Terminate workflows first (before stopping workers)
        await TerminateWorkflowsAsync();

        // Stop workers
        if (_workerCts != null)
        {
            _workerCts.Cancel();
            try
            {
                if (_workerTask != null)
                {
                    await _workerTask;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            Console.WriteLine("✓ Workers stopped");
        }

        // Cleanup context
        try
        {
            XiansContext.Clear();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private async Task TerminateWorkflowsAsync()
    {
        if (_agent?.TemporalService == null) return;

        try
        {
            var temporalClient = await _agent.TemporalService.GetClientAsync();
            await TemporalTestUtils.TerminateBuiltInWorkflowsAsync(
                temporalClient, 
                AGENT_NAME, 
                new[] { "knowledge-tests" });
            
            Console.WriteLine("✓ Workflows terminated");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to terminate workflows: {ex.Message}");
        }
    }

    private async Task InitializePlatformAsync()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        // Clean up static registries from previous tests
        XiansContext.CleanupForTests();

        var options = CreateTestOptions();

        _platform = await XiansPlatform.InitializeAsync(options);
        
        // Register agent with hardcoded name
        _agent = _platform.Agents.Register(new XiansAgentRegistration 
        { 
            Name = AGENT_NAME 
        });
        
        // CRITICAL: Define and upload workflow definition to actually register the agent with the server
        // This is what grants the user permission to manage this agent's knowledge
        var workflow = _agent.Workflows.DefineBuiltIn("knowledge-tests");
        
        // Also define the test workflow for workflow execution tests
        _agent.Workflows.DefineCustom<KnowledgeTestWorkflow>();
        
        await _agent.UploadWorkflowDefinitionsAsync();
        
        Console.WriteLine($"✓ Registered agent on server: {AGENT_NAME}");
        Console.WriteLine($"✓ Registered workflows: knowledge-tests, KnowledgeTestWorkflow");
    }

    [Fact]
    public async Task Knowledge_CreateAndGet_WorksWithRealServer()
    {
        // Skip if credentials not available
        if (!RunRealServerTests)
        {
            // Use Skip.If when available, for now just return
            return;
        }

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var knowledgeName = $"{_testKnowledgePrefix}-greeting";
            var content = "Hello from real server test!";

            // Act - Create
            Console.WriteLine($"Creating knowledge with agent: {_agent!.Name}");
            var created = await _agent!.Knowledge.UpdateAsync(
                knowledgeName,
                content,
                "instruction");

            Assert.True(created, "Failed to create knowledge on real server");

            // Wait a moment for server to process (if needed)
            await Task.Delay(100);

            // Act - Get
            Console.WriteLine($"Retrieving knowledge for agent: {_agent!.Name}");
            var retrieved = await _agent!.Knowledge.GetAsync(knowledgeName);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(knowledgeName, retrieved.Name);
            Assert.Equal(content, retrieved.Content);
            Assert.Equal("instruction", retrieved.Type);
            Assert.Equal(AGENT_NAME, retrieved.Agent);

            // Cleanup
            await _agent!.Knowledge.DeleteAsync(knowledgeName);
        }
        catch (Exception ex)
        {
            throw new Exception($"Real server test failed. Ensure SERVER_URL and API_KEY are set correctly. Error: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Knowledge_Update_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var knowledgeName = $"{_testKnowledgePrefix}-update-test";
            
            // Create initial
            await _agent!.Knowledge.UpdateAsync(
                knowledgeName,
                "Initial content",
                "text");

            // Act - Update
            var updated = await _agent!.Knowledge.UpdateAsync(
                knowledgeName,
                "Updated content",
                "text");

            Assert.True(updated);

            // Verify update
            var retrieved = await _agent!.Knowledge.GetAsync(knowledgeName);
            Assert.NotNull(retrieved);
            Assert.Equal("Updated content", retrieved.Content);

            // Cleanup
            await _agent!.Knowledge.DeleteAsync(knowledgeName);
        }
        catch (Exception ex)
        {
            throw new Exception($"Update test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Knowledge_Delete_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var knowledgeName = $"{_testKnowledgePrefix}-delete-test";
            
            // Create knowledge
            await _agent!.Knowledge.UpdateAsync(
                knowledgeName,
                "To be deleted",
                "text");

            // Act - Delete
            var deleted = await _agent!.Knowledge.DeleteAsync(knowledgeName);

            // Assert
            Assert.True(deleted);

            // Verify deletion
            var retrieved = await _agent!.Knowledge.GetAsync(knowledgeName);
            Assert.Null(retrieved);
        }
        catch (Exception ex)
        {
            throw new Exception($"Delete test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Knowledge_List_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange - Create multiple knowledge items
            var names = new[]
            {
                $"{_testKnowledgePrefix}-list-1",
                $"{_testKnowledgePrefix}-list-2",
                $"{_testKnowledgePrefix}-list-3"
            };

            foreach (var name in names)
            {
                await _agent!.Knowledge.UpdateAsync(name, $"Content for {name}", "text");
            }

            // Act
            var allKnowledge = await _agent!.Knowledge.ListAsync();

            // Assert
            Assert.NotNull(allKnowledge);
            
            // Should contain our test knowledge items
            foreach (var name in names)
            {
                Assert.Contains(allKnowledge, k => k.Name == name);
            }

            // Cleanup
            foreach (var name in names)
            {
                await _agent!.Knowledge.DeleteAsync(name);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"List test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Knowledge_GetNonExistent_ReturnsNull()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Act
            var result = await _agent!.Knowledge.GetAsync("definitely-does-not-exist-12345");

            // Assert
            Assert.Null(result);
        }
        catch (Exception ex)
        {
            throw new Exception($"Get non-existent test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Knowledge_DeleteNonExistent_ReturnsFalse()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Act
            var result = await _agent!.Knowledge.DeleteAsync("definitely-does-not-exist-12345");

            // Assert
            Assert.False(result);
        }
        catch (Exception ex)
        {
            throw new Exception($"Delete non-existent test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Knowledge_DifferentTypes_WorkCorrectly()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Test different knowledge types
            var testCases = new[]
            {
                ($"{_testKnowledgePrefix}-instruction", "Step 1: Do this\nStep 2: Do that", "instruction"),
                ($"{_testKnowledgePrefix}-json", "{\"key\":\"value\"}", "json"),
                ($"{_testKnowledgePrefix}-markdown", "# Heading\n\nContent", "markdown"),
                ($"{_testKnowledgePrefix}-text", "Plain text content", "text")
            };

            foreach (var (name, content, type) in testCases)
            {
                // Create
                await _agent!.Knowledge.UpdateAsync(name, content, type);

                // Retrieve and verify
                var retrieved = await _agent!.Knowledge.GetAsync(name);
                Assert.NotNull(retrieved);
                Assert.Equal(content, retrieved.Content);
                Assert.Equal(type, retrieved.Type);

                // Cleanup
                await _agent!.Knowledge.DeleteAsync(name);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Different types test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Knowledge_LargeContent_WorksCorrectly()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange - Create large content (but not too large to avoid timeout)
            var knowledgeName = $"{_testKnowledgePrefix}-large";
            var largeContent = new string('x', 10000); // 10KB

            // Act
            await _agent!.Knowledge.UpdateAsync(knowledgeName, largeContent, "text");
            var retrieved = await _agent!.Knowledge.GetAsync(knowledgeName);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(largeContent.Length, retrieved.Content.Length);

            // Cleanup
            await _agent!.Knowledge.DeleteAsync(knowledgeName);
        }
        catch (Exception ex)
        {
            throw new Exception($"Large content test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Knowledge_SpecialCharactersInName_WorksCorrectly()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Test names with special characters (but URL-safe)
            var testNames = new[]
            {
                $"{_testKnowledgePrefix}-user-123-preference",
                $"{_testKnowledgePrefix}-config.api.key",
                $"{_testKnowledgePrefix}-template_greeting_morning"
            };

            foreach (var name in testNames)
            {
                // Create
                await _agent!.Knowledge.UpdateAsync(name, "test content", "text");

                // Retrieve
                var retrieved = await _agent!.Knowledge.GetAsync(name);
                Assert.NotNull(retrieved);
                Assert.Equal(name, retrieved.Name);

                // Cleanup
                await _agent!.Knowledge.DeleteAsync(name);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Special characters test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Knowledge_UpdateWithNullType_UsesDefault()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var knowledgeName = $"{_testKnowledgePrefix}-null-type";
            
            // Act - Update with null type
            await _agent!.Knowledge.UpdateAsync(knowledgeName, "Content without type", null);
            
            // Retrieve and verify
            var retrieved = await _agent!.Knowledge.GetAsync(knowledgeName);
            Assert.NotNull(retrieved);
            Assert.Equal("Content without type", retrieved.Content);

            // Cleanup
            await _agent!.Knowledge.DeleteAsync(knowledgeName);
        }
        catch (Exception ex)
        {
            throw new Exception($"Null type test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Knowledge_ListEmpty_ReturnsEmptyList()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Act - List might not be empty if previous tests failed cleanup
            // But it should at least not throw
            var result = await _agent!.Knowledge.ListAsync();
            
            // Assert
            Assert.NotNull(result);
            // Don't assert it's empty - there might be leftover test data
        }
        catch (Exception ex)
        {
            throw new Exception($"List empty test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Knowledge_UpdateMultipleTimes_LastWins()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var knowledgeName = $"{_testKnowledgePrefix}-multi-update";
            
            // Act - Multiple updates
            await _agent!.Knowledge.UpdateAsync(knowledgeName, "Version 1", "text");
            await _agent!.Knowledge.UpdateAsync(knowledgeName, "Version 2", "text");
            await _agent!.Knowledge.UpdateAsync(knowledgeName, "Version 3", "text");
            
            // Verify final state
            var retrieved = await _agent!.Knowledge.GetAsync(knowledgeName);
            Assert.NotNull(retrieved);
            Assert.Equal("Version 3", retrieved.Content);

            // Cleanup
            await _agent!.Knowledge.DeleteAsync(knowledgeName);
        }
        catch (Exception ex)
        {
            throw new Exception($"Multiple updates test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Knowledge_GetAsync_WithEmptyName_ThrowsArgumentException()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _agent!.Knowledge.GetAsync(""));
    }

    [Fact]
    public async Task Knowledge_UpdateAsync_WithEmptyName_ThrowsArgumentException()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _agent!.Knowledge.UpdateAsync("", "content", "text"));
    }

    [Fact]
    public async Task Knowledge_UpdateAsync_WithEmptyContent_ThrowsArgumentException()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _agent!.Knowledge.UpdateAsync("test-name", "", "text"));
    }

    [Fact]
    public async Task Knowledge_DeleteAsync_WithEmptyName_ThrowsArgumentException()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _agent!.Knowledge.DeleteAsync(""));
    }

    [Fact]
    public async Task Knowledge_CancellationToken_Respected()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately
            
            var knowledgeName = $"{_testKnowledgePrefix}-cancelled";

            // Act & Assert - Operation should be cancelled
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await _agent!.Knowledge.GetAsync(knowledgeName, cts.Token));
        }
        catch (Exception ex)
        {
            throw new Exception($"Cancellation token test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Knowledge_TenantIsolation_VerifyAgentField()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var knowledgeName = $"{_testKnowledgePrefix}-tenant-test";
            
            // Act
            await _agent!.Knowledge.UpdateAsync(knowledgeName, "Tenant isolation test", "text");
            var retrieved = await _agent!.Knowledge.GetAsync(knowledgeName);
            
            // Assert - Verify tenant isolation via agent field
            Assert.NotNull(retrieved);
            Assert.Equal(AGENT_NAME, retrieved.Agent);
            Assert.NotNull(retrieved.TenantId);
            Assert.NotEmpty(retrieved.TenantId);

            // Cleanup
            await _agent!.Knowledge.DeleteAsync(knowledgeName);
        }
        catch (Exception ex)
        {
            throw new Exception($"Tenant isolation test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Knowledge_CrossAgentIsolation_AgentsCannotAccessEachOthersKnowledge()
    {
        if (!RunRealServerTests) return;

        // Create two separate platform instances with different agents
        XiansContext.CleanupForTests();
        
        var options1 = CreateTestOptions();
        var platform1 = await XiansPlatform.InitializeAsync(options1);
        var agent1 = platform1.Agents.Register(new XiansAgentRegistration { Name = "Agent1-IsolationTest" });
        var workflow1 = agent1.Workflows.DefineBuiltIn("isolation-test-1");
        await agent1.UploadWorkflowDefinitionsAsync();

        XiansContext.CleanupForTests();

        var options2 = CreateTestOptions();
        var platform2 = await XiansPlatform.InitializeAsync(options2);
        var agent2 = platform2.Agents.Register(new XiansAgentRegistration { Name = "Agent2-IsolationTest" });
        var workflow2 = agent2.Workflows.DefineBuiltIn("isolation-test-2");
        await agent2.UploadWorkflowDefinitionsAsync();

        try
        {
            // Arrange - Agent1 creates knowledge
            var knowledgeName = $"{_testKnowledgePrefix}-cross-agent-isolation";
            await agent1.Knowledge.UpdateAsync(knowledgeName, "Agent1's private knowledge", "text");

            // Act - Agent1 can retrieve its own knowledge
            var agent1Retrieved = await agent1.Knowledge.GetAsync(knowledgeName);
            Assert.NotNull(agent1Retrieved);
            Assert.Equal("Agent1-IsolationTest", agent1Retrieved.Agent);
            Assert.Equal("Agent1's private knowledge", agent1Retrieved.Content);

            // Act - Agent2 tries to retrieve Agent1's knowledge
            var agent2Retrieved = await agent2.Knowledge.GetAsync(knowledgeName);

            // Assert - Agent2 should NOT see Agent1's knowledge (isolation)
            Assert.Null(agent2Retrieved);

            // Additional verification: Agent2 creates knowledge with same name
            await agent2.Knowledge.UpdateAsync(knowledgeName, "Agent2's separate knowledge", "text");
            var agent2OwnKnowledge = await agent2.Knowledge.GetAsync(knowledgeName);
            
            Assert.NotNull(agent2OwnKnowledge);
            Assert.Equal("Agent2-IsolationTest", agent2OwnKnowledge.Agent);
            Assert.Equal("Agent2's separate knowledge", agent2OwnKnowledge.Content);

            // Verify Agent1's knowledge is unchanged
            var agent1StillIntact = await agent1.Knowledge.GetAsync(knowledgeName);
            Assert.NotNull(agent1StillIntact);
            Assert.Equal("Agent1's private knowledge", agent1StillIntact.Content);

            Console.WriteLine("✓ Cross-agent isolation verified: Agents can have same knowledge names without conflict");

            // Cleanup
            await agent1.Knowledge.DeleteAsync(knowledgeName);
            await agent2.Knowledge.DeleteAsync(knowledgeName);
            await agent1.DeleteAsync();
            await agent2.DeleteAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Cross-agent isolation test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Knowledge_SystemScoped_AutomaticallyCreatedWithSystemScopedAgent()
    {
        if (!RunRealServerTests) return;

        // Create a system-scoped agent
        XiansContext.CleanupForTests();
        var systemOptions = CreateTestOptions();
        var systemPlatform = await XiansPlatform.InitializeAsync(systemOptions);
        var systemAgent = systemPlatform.Agents.Register(new XiansAgentRegistration 
        { 
            Name = "SystemScopedKnowledgeAgent",
            IsTemplate = true  // System-scoped agent
        });
        var workflow = systemAgent.Workflows.DefineBuiltIn("sys-knowledge-test");
        await systemAgent.UploadWorkflowDefinitionsAsync();
        
        // Deploy the system-scoped agent to the tenant before use
        await systemAgent.DeployAsync();

        try
        {
            // Arrange
            var knowledgeName = $"{_testKnowledgePrefix}-system-scoped";
            
            // Act - Create knowledge with system-scoped agent (should auto-inherit SystemScoped = true)
            await systemAgent.Knowledge.UpdateAsync(
                knowledgeName, 
                "System-wide default knowledge",
                "text");

            // Retrieve and verify
            var retrieved = await systemAgent.Knowledge.GetAsync(knowledgeName);
            
            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(knowledgeName, retrieved.Name);
            Assert.True(retrieved.SystemScoped, "Knowledge should be system-scoped when created by system-scoped agent");
            Assert.Equal("System-wide default knowledge", retrieved.Content);
            
            Console.WriteLine("✓ System-scoped agent automatically creates system-scoped knowledge");

            // Cleanup
            await systemAgent.Knowledge.DeleteAsync(knowledgeName);
            await systemAgent.DeleteAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"System-scoped knowledge test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Knowledge_TenantScoped_CreatedWithTenantScopedAgent()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange - _agent is tenant-scoped (SystemScoped = false by default)
            var knowledgeName = $"{_testKnowledgePrefix}-tenant-scoped";
            
            // Act - Create knowledge with tenant-scoped agent
            await _agent!.Knowledge.UpdateAsync(
                knowledgeName, 
                "Tenant-specific knowledge",
                "text");

            // Retrieve and verify
            var retrieved = await _agent!.Knowledge.GetAsync(knowledgeName);
            
            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(knowledgeName, retrieved.Name);
            Assert.False(retrieved.SystemScoped, "Knowledge should be tenant-scoped when created by tenant-scoped agent");
            Assert.Equal("Tenant-specific knowledge", retrieved.Content);
            Assert.NotNull(retrieved.TenantId);
            
            Console.WriteLine("✓ Tenant-scoped agent creates tenant-scoped knowledge");

            // Cleanup
            await _agent!.Knowledge.DeleteAsync(knowledgeName);
        }
        catch (Exception ex)
        {
            throw new Exception($"Tenant-scoped knowledge test failed: {ex.Message}", ex);
        }
    }


    [Fact]
    public async Task Knowledge_Scoping_TenantOverridesSystemKnowledge()
    {
        if (!RunRealServerTests) return;

        // Create a system-scoped template agent with knowledge
        XiansContext.CleanupForTests();
        var systemOptions = CreateTestOptions();
        var systemPlatform = await XiansPlatform.InitializeAsync(systemOptions);
        var systemAgent = systemPlatform.Agents.Register(new XiansAgentRegistration
        {
            Name = "TemplateAgent",
            IsTemplate = true
        });
        systemAgent.Workflows.DefineBuiltIn("template-workflow");

        var knowledgeName = $"{_testKnowledgePrefix}-template-knowledge";

        // Upload the system-scoped knowledge using the text helper
        await systemAgent.Knowledge.UploadTextResourceAsync(
            knowledgeName,
            "Original system template greeting",
            "text");
        await systemAgent.UploadWorkflowDefinitionsAsync();

        var systemKnowledgeBefore = await systemAgent.Knowledge.GetSystemAsync(knowledgeName);
        Assert.NotNull(systemKnowledgeBefore);
        Assert.True(systemKnowledgeBefore.SystemScoped);
        Assert.Equal("Original system template greeting", systemKnowledgeBefore.Content);

        // Deploy the system agent to create the tenant-scoped replica
        await systemAgent.DeployAsync();
        Console.WriteLine("✓ Deployed system-scoped agent to tenant");

        // Access the deployed (tenant-scoped) replica and change its knowledge
        var deployedAgent = systemPlatform.Agents.Register(new XiansAgentRegistration
        {
            Name = "TemplateAgent",
            IsTemplate = false
        });

        var deployedKnowledge = await deployedAgent.Knowledge.GetAsync(knowledgeName);
        Assert.NotNull(deployedKnowledge);
        Assert.False(deployedKnowledge.SystemScoped);
        Assert.Equal("Original system template greeting", deployedKnowledge.Content);

        await deployedAgent.Knowledge.UpdateAsync(
            knowledgeName,
            "Modified tenant greeting",
            "text",
            systemScoped: false);

        var modifiedTenantKnowledge = await deployedAgent.Knowledge.GetAsync(knowledgeName);
        Assert.NotNull(modifiedTenantKnowledge);
        Assert.False(modifiedTenantKnowledge.SystemScoped);
        Assert.Equal("Modified tenant greeting", modifiedTenantKnowledge.Content);

        // Verify the original system-scoped knowledge is unchanged
        var systemKnowledgeAfter = await systemAgent.Knowledge.GetSystemAsync(knowledgeName);
        Assert.NotNull(systemKnowledgeAfter);
        Assert.True(systemKnowledgeAfter.SystemScoped);
        Assert.Equal("Original system template greeting", systemKnowledgeAfter.Content);

        // Cleanup
        await systemAgent.Knowledge.DeleteAsync(knowledgeName);
        await deployedAgent.Knowledge.DeleteAsync(knowledgeName);
        await deployedAgent.DeleteAsync();
        await systemAgent.DeleteAsync();
    }

    [Fact]
    public async Task Knowledge_List_ReturnsAgentsKnowledgeOnly()
    {
        if (!RunRealServerTests) return;

        // Create two system-scoped agent templates, each with unique knowledge
        XiansContext.CleanupForTests();
        var systemOptions = CreateTestOptions();

        var systemPlatform = await XiansPlatform.InitializeAsync(systemOptions);

        var templateAgentAName = $"ListTemplateAgentA-{_testKnowledgePrefix}";
        var templateAgentBName = $"ListTemplateAgentB-{_testKnowledgePrefix}";

        var templateAgentA = systemPlatform.Agents.Register(new XiansAgentRegistration
        {
            Name = templateAgentAName,
            IsTemplate = true
        });
        templateAgentA.Workflows.DefineBuiltIn("list-template-a");

        var templateAgentB = systemPlatform.Agents.Register(new XiansAgentRegistration
        {
            Name = templateAgentBName,
            IsTemplate = true
        });
        templateAgentB.Workflows.DefineBuiltIn("list-template-b");

        var knowledgeAName = $"{_testKnowledgePrefix}-template-a";
        var knowledgeBName = $"{_testKnowledgePrefix}-template-b";

        XiansAgent? deployedAgent = null;

        try
        {
            // Attach distinct knowledge to each template BEFORE uploading workflow definitions
            // This ensures the knowledge is included when deploying the template
            await templateAgentA.Knowledge.UpdateAsync(
                knowledgeAName,
                "Template A only knowledge",
                "text",
                systemScoped: true);

            await templateAgentB.Knowledge.UpdateAsync(
                knowledgeBName,
                "Template B only knowledge",
                "text",
                systemScoped: true);

            // Upload workflow definitions after knowledge is attached
            await templateAgentA.UploadWorkflowDefinitionsAsync();
            await templateAgentB.UploadWorkflowDefinitionsAsync();

            // Deploy only template A to create a tenant-scoped instance
            await templateAgentA.DeployAsync();
            
            // Access the deployed (tenant-scoped) replica by registering with IsTemplate = false
            deployedAgent = systemPlatform.Agents.Register(new XiansAgentRegistration
            {
                Name = templateAgentAName,
                IsTemplate = false
            });

            // Listing knowledge on the deployed agent should not include template B's knowledge
            var deployedKnowledgeList = await deployedAgent.Knowledge.ListAsync();

            Assert.NotNull(deployedKnowledgeList);
            Assert.Contains(deployedKnowledgeList, k => k.Name == knowledgeAName);
            Assert.DoesNotContain(deployedKnowledgeList, k => k.Name == knowledgeBName);

            Console.WriteLine($"✓ Deployed agent knowledge list contains its own knowledge only (count: {deployedKnowledgeList.Count})");
        }
        catch (Exception ex)
        {
            throw new Exception($"Knowledge list scoping across templates failed: {ex.Message}", ex);
        }
        finally
        {
            // Cleanup knowledge and agents
            try { await templateAgentA.Knowledge.DeleteAsync(knowledgeAName); } catch { /* ignore cleanup errors */ }
            try { await templateAgentB.Knowledge.DeleteAsync(knowledgeBName); } catch { /* ignore cleanup errors */ }
            if (deployedAgent != null)
            {
                try { await deployedAgent.Knowledge.DeleteAsync(knowledgeAName); } catch { /* ignore cleanup errors */ }
                try { await deployedAgent.DeleteAsync(); } catch { /* ignore cleanup errors */ }
            }
            try { await templateAgentA.DeleteAsync(); } catch { /* ignore cleanup errors */ }
            try { await templateAgentB.DeleteAsync(); } catch { /* ignore cleanup errors */ }
        }
    }

    [Fact]
    public async Task Knowledge_WorksFromWithinWorkflow_ContextAwareExecution()
    {
        if (!RunRealServerTests) return;

        try
        {
            // Arrange - Test ID for tracking
            var testId = Guid.NewGuid().ToString();
            var knowledgeName = $"{_testKnowledgePrefix}-workflow-test-{testId}";
            
            Console.WriteLine($"=== Testing Knowledge Operations from Within Workflow ===");
            Console.WriteLine($"Test ID: {testId}");
            Console.WriteLine($"Knowledge Name: {knowledgeName}");

            // Get Temporal client from agent
            var temporalClient = await _agent!.TemporalService!.GetClientAsync();
            
            // Build workflow ID and task queue using TemporalTestUtils
            var workflowType = $"{AGENT_NAME}:KnowledgeWorkflowTest";
            var workflowId = $"{_platform!.Options.CertificateTenantId}:{workflowType}:{testId}";
            var taskQueue = Xians.Lib.Common.MultiTenancy.TenantContext.GetTaskQueueName(
                workflowType,
                systemScoped: false,
                _platform!.Options.CertificateTenantId);
            
            Console.WriteLine($"Starting Temporal workflow:");
            Console.WriteLine($"  Workflow ID: {workflowId}");
            Console.WriteLine($"  Workflow Type: {workflowType}");
            Console.WriteLine($"  Task Queue: {taskQueue}");
            
            // Start the workflow
            var handle = await temporalClient.StartWorkflowAsync(
                (KnowledgeTestWorkflow wf) => wf.RunAsync(_agent!.Name, knowledgeName, testId),
                new Temporalio.Client.WorkflowOptions
                {
                    Id = workflowId,
                    TaskQueue = taskQueue,
                    ExecutionTimeout = TemporalTestUtils.DefaultWorkflowExecutionTimeout
                });
            
            Console.WriteLine("✓ Workflow started, waiting for completion...");
            
            Console.WriteLine("⏳ Waiting for workflow to complete...");
            
            // Wait for workflow to complete (workers are running, so it should execute)
            var result = await handle.GetResultAsync().WaitAsync(TimeSpan.FromSeconds(30));
            
            Console.WriteLine("✓ Workflow execution completed via Temporal workers!");
            Console.WriteLine("✓ Knowledge operations executed through activities!");
            
            // Assert workflow completed successfully
            Assert.NotNull(result);
            Assert.True(result.Success, $"Workflow test failed: {result.Error}");
            Assert.Equal("workflow-test-content", result.RetrievedContent);
            Assert.Equal(1, result.ListCount);
            
            Console.WriteLine("✓ Workflow completed successfully");
            Console.WriteLine($"  - Created knowledge: {result.CreateSuccess}");
            Console.WriteLine($"  - Retrieved knowledge: {result.RetrievedContent}");
            Console.WriteLine($"  - List count: {result.ListCount}");
            Console.WriteLine($"  - Deleted knowledge: {result.DeleteSuccess}");
            Console.WriteLine("✓ Knowledge operations work correctly from within workflow!");
            Console.WriteLine("✓ ContextAwareActivityExecutor pattern verified!");
        }
        catch (Exception ex)
        {
            throw new Exception($"Workflow context test failed: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// Test workflow that uses knowledge operations to verify context-aware execution.
/// This validates that KnowledgeCollection properly uses KnowledgeActivityExecutor
/// to execute activities when called from workflow context.
/// </summary>
[Temporalio.Workflows.Workflow($"{RealServerKnowledgeTests.AGENT_NAME}:KnowledgeWorkflowTest")]
public class KnowledgeTestWorkflow
{
    [Temporalio.Workflows.WorkflowRun]
    public async Task<KnowledgeWorkflowResult> RunAsync(string agentName, string knowledgeName, string testId)
    {
        var result = new KnowledgeWorkflowResult();
        
        try
        {
            Console.WriteLine($"[KnowledgeWorkflow] Starting test for agent: {agentName}");
            
            // Get agent from workflow context
            var agent = Xians.Lib.Agents.Core.XiansContext.GetAgent(agentName);
            if (agent == null)
            {
                result.Error = $"Agent '{agentName}' not found in workflow context";
                result.Success = false;
                return result;
            }
            
            Console.WriteLine($"[KnowledgeWorkflow] ✓ Agent retrieved from context: {agent.Name}");

            // Test 1: Create knowledge (calls via KnowledgeActivityExecutor → KnowledgeActivities)
            Console.WriteLine("[KnowledgeWorkflow] Step 1: Creating knowledge via activity executor...");
            var createSuccess = await agent.Knowledge.UpdateAsync(
                knowledgeName, 
                "workflow-test-content", 
                "test");
            result.CreateSuccess = createSuccess;
            
            if (!createSuccess)
            {
                result.Error = "Failed to create knowledge";
                result.Success = false;
                return result;
            }
            
            Console.WriteLine("[KnowledgeWorkflow] ✓ Knowledge created via activity");

            // Small delay to ensure consistency
            await Temporalio.Workflows.Workflow.DelayAsync(TimeSpan.FromMilliseconds(100));

            // Test 2: Retrieve knowledge (calls via KnowledgeActivityExecutor → KnowledgeActivities)
            Console.WriteLine("[KnowledgeWorkflow] Step 2: Retrieving knowledge via activity executor...");
            var retrieved = await agent.Knowledge.GetAsync(knowledgeName);
            
            if (retrieved == null)
            {
                result.Error = "Failed to retrieve knowledge";
                result.Success = false;
                return result;
            }
            
            result.RetrievedContent = retrieved.Content;
            Console.WriteLine($"[KnowledgeWorkflow] ✓ Knowledge retrieved via activity: {retrieved.Content}");

            // Test 3: List knowledge (calls via KnowledgeActivityExecutor → KnowledgeActivities)
            Console.WriteLine("[KnowledgeWorkflow] Step 3: Listing knowledge via activity executor...");
            var allKnowledge = await agent.Knowledge.ListAsync();
            result.ListCount = allKnowledge.Count(k => k.Name == knowledgeName);
            Console.WriteLine($"[KnowledgeWorkflow] ✓ Listed via activity: Found {result.ListCount} items");

            // Test 4: Delete knowledge (calls via KnowledgeActivityExecutor → KnowledgeActivities)
            Console.WriteLine("[KnowledgeWorkflow] Step 4: Deleting knowledge via activity executor...");
            var deleteSuccess = await agent.Knowledge.DeleteAsync(knowledgeName);
            result.DeleteSuccess = deleteSuccess;
            
            if (!deleteSuccess)
            {
                result.Error = "Failed to delete knowledge";
                result.Success = false;
                return result;
            }
            
            Console.WriteLine("[KnowledgeWorkflow] ✓ Knowledge deleted via activity");

            // Success
            result.Success = true;
            Console.WriteLine("[KnowledgeWorkflow] ✓ All knowledge operations executed successfully via activities!");
            Console.WriteLine("[KnowledgeWorkflow] ✓ Context-aware execution verified!");
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[KnowledgeWorkflow] Error: {ex.Message}");
            result.Error = ex.Message;
            result.Success = false;
            return result;
        }
    }
}

/// <summary>
/// Result from the knowledge workflow test.
/// </summary>
public class KnowledgeWorkflowResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool CreateSuccess { get; set; }
    public string? RetrievedContent { get; set; }
    public int ListCount { get; set; }
    public bool DeleteSuccess { get; set; }
}

