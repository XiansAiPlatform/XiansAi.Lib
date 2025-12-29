using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Knowledge.Models;
using Xians.Lib.Agents.Messaging.Models;
using Xians.Lib.Agents.Workflows.Models;

namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// Real server tests for Knowledge SDK.
/// These tests run against an actual Xians server.
/// Set SERVER_URL and API_KEY environment variables to run these tests.
/// </summary>
[Trait("Category", "RealServer")]
public class RealServerKnowledgeTests : RealServerTestBase
{
    private XiansPlatform? _platform;
    private XiansAgent? _agent;
    private readonly string _testKnowledgePrefix;
    
    // Use hardcoded agent name across all tests
    private const string AGENT_NAME = "KnowledgeTestAgent";

    public RealServerKnowledgeTests()
    {
        // Use unique prefix for test knowledge to avoid conflicts between test runs
        _testKnowledgePrefix = $"test-{Guid.NewGuid().ToString()[..8]}";
    }

    private async Task InitializePlatformAsync()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var options = new XiansOptions
        {
            ServerUrl = ServerUrl!,
            ApiKey = ApiKey!
        };

        _platform = await XiansPlatform.InitializeAsync(options);
        
        // Register agent with hardcoded name
        _agent = _platform.Agents.Register(new XiansAgentRegistration 
        { 
            Name = AGENT_NAME 
        });
        
        // CRITICAL: Define and upload workflow definition to actually register the agent with the server
        // This is what grants the user permission to manage this agent's knowledge
        var workflow = _agent.Workflows.DefineBuiltIn("knowledge-tests");
        await _agent.UploadWorkflowDefinitionsAsync();
        
        Console.WriteLine($"✓ Registered agent on server: {AGENT_NAME}");
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
}

