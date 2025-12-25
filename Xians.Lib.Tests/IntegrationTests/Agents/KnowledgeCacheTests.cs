using System.Net;
using System.Net.Http.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xians.Lib.Agents;
using Xians.Lib.Agents.Models;
using Xians.Lib.Common;
using Xians.Lib.Configuration.Models;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.IntegrationTests.Agents;

[Trait("Category", "Integration")]
public class KnowledgeCacheTests : IAsyncLifetime
{
    private WireMockServer? _mockServer;
    private XiansPlatform? _platform;
    private XiansAgent? _agent;

    public async Task InitializeAsync()
    {
        // Setup mock HTTP server
        _mockServer = WireMockServer.Start();

        // Mock the settings endpoint
        _mockServer
            .Given(Request.Create().WithPath("/api/agent/settings/flowserver").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(@"{
                    ""temporalServerUrl"": ""localhost:7233"",
                    ""temporalNamespace"": ""default""
                }"));

        // Initialize platform with caching enabled (default)
        var options = new XiansOptions
        {
            ServerUrl = _mockServer.Url!,
            ApiKey = TestCertificateGenerator.GetTestCertificate(),
            TemporalConfiguration = new TemporalConfiguration
            {
                ServerUrl = "localhost:7233",
                Namespace = "default"
            }
            // Cache uses defaults: Enabled=true, Knowledge.TtlMinutes=5
        };

        _platform = await XiansPlatform.InitializeAsync(options);
        _agent = _platform.Agents.Register(new XiansAgentRegistration { Name = "cache-test-agent" });
    }

    [Fact]
    public async Task GetAsync_CalledTwice_SecondCallUsesCache()
    {
        // Arrange
        var knowledge = new Knowledge
        {
            Name = "cached-item",
            Content = "Cached content",
            Agent = "cache-test-agent"
        };

        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/knowledge/latest")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(knowledge));

        // Act - Call twice
        var result1 = await _agent!.Knowledge.GetAsync("cached-item");
        var result2 = await _agent!.Knowledge.GetAsync("cached-item");

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal("Cached content", result1.Content);
        Assert.Equal("Cached content", result2.Content);
        
        // Verify only 1 request was made (check WireMock logs)
        var requests = _mockServer.LogEntries.Count(e => e.RequestMessage.Path.Contains("/api/agent/knowledge/latest"));
        Assert.Equal(1, requests); // Second call used cache
    }

    [Fact]
    public async Task UpdateAsync_InvalidatesCache_NextGetHitsServer()
    {
        // Arrange
        var knowledge = new Knowledge
        {
            Name = "update-cache-test",
            Content = "Original",
            Agent = "cache-test-agent"
        };

        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/knowledge/latest")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(knowledge));

        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/knowledge")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200));

        // Act
        var result1 = await _agent!.Knowledge.GetAsync("update-cache-test"); // Cache it
        var result2 = await _agent!.Knowledge.GetAsync("update-cache-test"); // From cache
        
        var requestsBefore = _mockServer.LogEntries.Count(e => e.RequestMessage.Path.Contains("/api/agent/knowledge/latest"));
        
        await _agent!.Knowledge.UpdateAsync("update-cache-test", "Updated"); // Invalidate
        
        var result3 = await _agent!.Knowledge.GetAsync("update-cache-test"); // Server again
        
        var requestsAfter = _mockServer.LogEntries.Count(e => e.RequestMessage.Path.Contains("/api/agent/knowledge/latest"));

        // Assert - Should have hit server twice (initial + after update)
        Assert.Equal(1, requestsBefore);
        Assert.Equal(2, requestsAfter);
    }

    [Fact]
    public async Task DeleteAsync_InvalidatesCache()
    {
        // Arrange
        var knowledge = new Knowledge
        {
            Name = "delete-cache-test",
            Content = "Content",
            Agent = "cache-test-agent"
        };

        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/knowledge/latest")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(knowledge));

        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/knowledge")
                .UsingDelete())
            .RespondWith(Response.Create()
                .WithStatusCode(200));

        // Act
        await _agent!.Knowledge.GetAsync("delete-cache-test"); // Cache it
        var requestsBefore = _mockServer.LogEntries.Count(e => e.RequestMessage.Path.Contains("/api/agent/knowledge/latest"));
        
        await _agent!.Knowledge.DeleteAsync("delete-cache-test"); // Invalidate
        await _agent!.Knowledge.GetAsync("delete-cache-test"); // Should hit server
        
        var requestsAfter = _mockServer.LogEntries.Count(e => e.RequestMessage.Path.Contains("/api/agent/knowledge/latest"));

        // Assert
        Assert.Equal(1, requestsBefore);
        Assert.Equal(2, requestsAfter); // GET + GET after delete
    }

    [Fact]
    public async Task Cache_WithDisabledGlobally_AlwaysHitsServer()
    {
        // Arrange - Create new platform with cache disabled
        var optionsNoCache = new XiansOptions
        {
            ServerUrl = _mockServer!.Url!,
            ApiKey = TestCertificateGenerator.GetTestCertificate(),
            TemporalConfiguration = new TemporalConfiguration
            {
                ServerUrl = "localhost:7233",
                Namespace = "default"
            },
            Cache = new CacheOptions
            {
                Enabled = false // Disable globally
            }
        };

        var platformNoCache = await XiansPlatform.InitializeAsync(optionsNoCache);
        var agentNoCache = platformNoCache.Agents.Register(new XiansAgentRegistration 
        { 
            Name = "no-cache-agent" 
        });

        var knowledge = new Knowledge
        {
            Name = "no-cache-item",
            Content = "Content",
            Agent = "no-cache-agent"
        };

        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/knowledge/latest")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(knowledge));

        // Act - Call twice
        await agentNoCache.Knowledge.GetAsync("no-cache-item");
        await agentNoCache.Knowledge.GetAsync("no-cache-item");

        // Assert - Both calls should hit server (no caching)
        var requests = _mockServer.LogEntries.Count(e => 
            e.RequestMessage.Path.Contains("/api/agent/knowledge/latest"));
        Assert.True(requests >= 2, $"Expected at least 2 requests, got {requests}");
    }

    [Fact]
    public void Platform_ExposesCache_ForManualControl()
    {
        // Assert - Platform exposes cache for manual operations
        Assert.NotNull(_platform!.Cache);
        
        var stats = _platform.Cache.GetStatistics();
        Assert.True(stats.IsEnabled);
        
        // Can clear cache manually
        _platform.Cache.Clear();
        var statsAfter = _platform.Cache.GetStatistics();
        Assert.Equal(0, statsAfter.Count);
    }

    public Task DisposeAsync()
    {
        _mockServer?.Stop();
        _mockServer?.Dispose();
        return Task.CompletedTask;
    }
}
