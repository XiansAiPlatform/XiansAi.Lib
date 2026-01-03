using System.Net;
using System.Net.Http.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Knowledge.Models;
using Xians.Lib.Agents.Workflows.Models;
using Xians.Lib.Common.Caching;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Configuration.Models;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.IntegrationTests.Agents;

[Collection("Sequential")]
[Trait("Category", "Integration")]
public class KnowledgeIntegrationTests : IAsyncLifetime
{
    private WireMockServer? _mockServer;
    private XiansPlatform? _platform;
    private XiansAgent? _agent;

    public async Task InitializeAsync()
    {
        // Clean up static registries from previous tests
        XiansContext.CleanupForTests();
        
        // Setup mock HTTP server
        _mockServer = WireMockServer.Start();

        // Mock the settings endpoint (required for platform initialization)
        _mockServer
            .Given(Request.Create().WithPath("/api/agent/settings/flowserver").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(@"{
                    ""temporalServerUrl"": ""localhost:7233"",
                    ""temporalNamespace"": ""default"",
                    ""temporalCertificate"": null,
                    ""temporalPrivateKey"": null
                }"));

        // Initialize platform with mock server
        var options = new XiansOptions
        {
            ServerUrl = _mockServer.Url!,
            ApiKey = TestCertificateGenerator.GetTestCertificate(),
            TemporalConfiguration = new TemporalConfiguration
            {
                ServerUrl = "localhost:7233",
                Namespace = "default"
            }
        };

        _platform = await XiansPlatform.InitializeAsync(options);
        _agent = _platform.Agents.Register(new XiansAgentRegistration 
        { 
            Name = "test-agent" 
        });
    }

    [Fact]
    public async Task GetAsync_WithExistingKnowledge_ReturnsKnowledge()
    {
        // Arrange
        var knowledgeResponse = new Knowledge
        {
            Id = "123",
            Name = "greeting",
            Content = "Hello, World!",
            Type = "instruction",
            Agent = "test-agent",
            TenantId = "test-tenant",
            CreatedAt = DateTime.UtcNow
        };

        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/knowledge/latest")
                .WithParam("name", "greeting")
                .WithParam("agent", "test-agent")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(knowledgeResponse));

        // Act
        var result = await _agent!.Knowledge.GetAsync("greeting");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("greeting", result.Name);
        Assert.Equal("Hello, World!", result.Content);
        Assert.Equal("instruction", result.Type);
    }

    [Fact]
    public async Task GetAsync_WithNonExistentKnowledge_ReturnsNull()
    {
        // Arrange
        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/knowledge/latest")
                .WithParam("name", "non-existent")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404));

        // Act
        var result = await _agent!.Knowledge.GetAsync("non-existent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_WithNewKnowledge_CreatesSuccessfully()
    {
        // Arrange
        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/knowledge")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201));

        // Act
        var success = await _agent!.Knowledge.UpdateAsync(
            "config", 
            "{\"theme\":\"dark\"}", 
            "json");

        // Assert
        Assert.True(success);
    }

    [Fact]
    public async Task UpdateAsync_WithExistingKnowledge_UpdatesSuccessfully()
    {
        // Arrange
        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/knowledge")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)); // 200 OK for update

        // Act
        var success = await _agent!.Knowledge.UpdateAsync(
            "existing", 
            "updated content");

        // Assert
        Assert.True(success);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingKnowledge_DeletesSuccessfully()
    {
        // Arrange
        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/knowledge")
                .WithParam("name", "old-knowledge")
                .WithParam("agent", "test-agent")
                .UsingDelete())
            .RespondWith(Response.Create()
                .WithStatusCode(200));

        // Act
        var deleted = await _agent!.Knowledge.DeleteAsync("old-knowledge");

        // Assert
        Assert.True(deleted);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistent_ReturnsFalse()
    {
        // Arrange
        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/knowledge")
                .WithParam("name", "non-existent")
                .UsingDelete())
            .RespondWith(Response.Create()
                .WithStatusCode(404));

        // Act
        var deleted = await _agent!.Knowledge.DeleteAsync("non-existent");

        // Assert
        Assert.False(deleted);
    }

    [Fact]
    public async Task ListAsync_WithMultipleKnowledge_ReturnsAll()
    {
        // Arrange
        var knowledgeList = new List<Knowledge>
        {
            new Knowledge 
            { 
                Name = "instruction-1", 
                Content = "Content 1",
                Type = "instruction",
                Agent = "test-agent"
            },
            new Knowledge 
            { 
                Name = "config-1", 
                Content = "{}",
                Type = "json",
                Agent = "test-agent"
            }
        };

        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/knowledge/list")
                .WithParam("agent", "test-agent")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(knowledgeList));

        // Act
        var result = await _agent!.Knowledge.ListAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, k => k.Name == "instruction-1");
        Assert.Contains(result, k => k.Name == "config-1");
    }

    [Fact]
    public async Task ListAsync_WithNoKnowledge_ReturnsEmptyList()
    {
        // Arrange
        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/knowledge/list")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new List<Knowledge>()));

        // Act
        var result = await _agent!.Knowledge.ListAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Knowledge_WithSpecialCharactersInName_HandlesCorrectly()
    {
        // Arrange
        var knowledgeName = "user-preference:theme";
        
        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/knowledge/latest")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new Knowledge 
                { 
                    Name = knowledgeName, 
                    Content = "dark" 
                }));

        // Act
        var result = await _agent!.Knowledge.GetAsync(knowledgeName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(knowledgeName, result.Name);
    }

    [Fact]
    public async Task Knowledge_FullCRUDCycle_WorksCorrectly()
    {
        // Arrange - Create
        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/knowledge")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201));

        // Act - Create
        var created = await _agent!.Knowledge.UpdateAsync(
            "test-cycle", 
            "initial content",
            "text");
        Assert.True(created);

        // Arrange - Read
        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/knowledge/latest")
                .WithParam("name", "test-cycle")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new Knowledge 
                { 
                    Name = "test-cycle", 
                    Content = "initial content",
                    Type = "text"
                }));

        // Act - Read
        var read = await _agent!.Knowledge.GetAsync("test-cycle");
        Assert.NotNull(read);
        Assert.Equal("initial content", read.Content);

        // Arrange - Update
        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/knowledge")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200));

        // Act - Update
        var updated = await _agent!.Knowledge.UpdateAsync(
            "test-cycle", 
            "updated content");
        Assert.True(updated);

        // Arrange - Delete
        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/knowledge")
                .WithParam("name", "test-cycle")
                .UsingDelete())
            .RespondWith(Response.Create()
                .WithStatusCode(200));

        // Act - Delete
        var deleted = await _agent!.Knowledge.DeleteAsync("test-cycle");
        Assert.True(deleted);
    }

    public Task DisposeAsync()
    {
        _mockServer?.Stop();
        _mockServer?.Dispose();
        XiansContext.CleanupForTests();
        return Task.CompletedTask;
    }
}

