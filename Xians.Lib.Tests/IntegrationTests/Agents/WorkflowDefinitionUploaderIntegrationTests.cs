using System.Net;
using System.Text.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Knowledge.Models;
using Xians.Lib.Agents.Messaging.Models;
using Xians.Lib.Agents.Workflows.Models;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.IntegrationTests.Agents;

[Trait("Category", "Integration")]
public class WorkflowDefinitionUploaderIntegrationTests : IAsyncLifetime
{
    private WireMockServer? _mockServer;
    private XiansPlatform? _platform;

    public Task InitializeAsync()
    {
        // Setup mock HTTP server
        _mockServer = WireMockServer.Start();
        
        // Configure default responses
        _mockServer
            .Given(Request.Create()
                .WithPath("/api/agent/definitions/check")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.NotFound));

        _mockServer
            .Given(Request.Create()
                .WithPath("/api/agent/definitions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.Created)
                .WithBody("{\"success\": true}"));
        
        // Initialize platform with mock server
        _platform = XiansPlatform.Initialize(new XiansOptions
        {
            ServerUrl = _mockServer.Url!,
            ApiKey = TestCertificateGenerator.GetTestCertificate(),
            TemporalConfiguration = new Configuration.Models.TemporalConfiguration
            {
                ServerUrl = "localhost:7233",
                Namespace = "default"
            }
        });
        
        return Task.CompletedTask;
    }

    [Fact]
    public async Task DefineBuiltInWorkflow_ShouldUploadToServer()
    {
        // Arrange
        var agent = _platform!.Agents.Register(new XiansAgentRegistration
        {
            Name = "TestAgent",
            SystemScoped = false
        });

        // Act
        var workflow = agent.Workflows.DefineBuiltIn(workers: 1, name: "Conversational");
        
        // Upload workflow definitions (happens automatically in RunAllAsync, but we call it explicitly for testing)
        await agent.Workflows.UploadAllDefinitionsAsync();

        // Assert
        Assert.NotNull(workflow);
        var requests = _mockServer!.LogEntries.ToList();
        Assert.Contains(requests, r => r.RequestMessage.Path.Contains("/api/agent/definitions/check"));
        Assert.Contains(requests, r => r.RequestMessage.Path == "/api/agent/definitions" && r.RequestMessage.Method == "POST");
    }

    [Fact]
    public async Task DefineBuiltInWorkflow_ShouldSendCorrectPayload()
    {
        // Arrange
        var uniqueAgentName = $"PayloadTestAgent_{Guid.NewGuid():N}";
        var agent = _platform!.Agents.Register(new XiansAgentRegistration
        {
            Name = uniqueAgentName,
            SystemScoped = false
        });

        // Act
        var workflow = agent.Workflows.DefineBuiltIn(workers: 1, name: "Conversational");
        
        // Upload workflow definitions (happens automatically in RunAllAsync, but we call it explicitly for testing)
        await agent.Workflows.UploadAllDefinitionsAsync();

        // Assert - Find the most recent POST request
        var postRequest = _mockServer!.LogEntries
            .Where(r => r.RequestMessage.Path == "/api/agent/definitions" && r.RequestMessage.Method == "POST")
            .LastOrDefault();
        
        Assert.NotNull(postRequest);
        var capturedBody = postRequest.RequestMessage.Body;
        Assert.NotNull(capturedBody);
        
        var uploadedDefinition = JsonSerializer.Deserialize<WorkflowDefinition>(capturedBody);
        Assert.NotNull(uploadedDefinition);
        Assert.Equal(uniqueAgentName, uploadedDefinition.Agent);
        Assert.Equal($"{uniqueAgentName}:Default Workflow - Conversational", uploadedDefinition.WorkflowType);
        Assert.Equal("Conversational", uploadedDefinition.Name);
        Assert.False(uploadedDefinition.SystemScoped);
        Assert.Equal(1, uploadedDefinition.Workers);
    }

    [Fact]
    public async Task DefineBuiltInWorkflow_WithSystemScopedTrue_ShouldIncludeInPayload()
    {
        // Arrange
        var uniqueAgentName = $"SystemAgent_{Guid.NewGuid():N}";
        var agent = _platform!.Agents.Register(new XiansAgentRegistration
        {
            Name = uniqueAgentName,
            SystemScoped = true
        });

        // Act
        var workflow = agent.Workflows.DefineBuiltIn(workers: 2, name: "SystemWorkflow");
        
        // Upload workflow definitions (happens automatically in RunAllAsync, but we call it explicitly for testing)
        await agent.Workflows.UploadAllDefinitionsAsync();

        // Assert - Find the most recent POST request
        var postRequest = _mockServer!.LogEntries
            .Where(r => r.RequestMessage.Path == "/api/agent/definitions" && r.RequestMessage.Method == "POST")
            .LastOrDefault();
        
        Assert.NotNull(postRequest);
        var uploadedDefinition = JsonSerializer.Deserialize<WorkflowDefinition>(postRequest.RequestMessage.Body!);
        Assert.NotNull(uploadedDefinition);
        Assert.Equal(uniqueAgentName, uploadedDefinition.Agent);
        Assert.Equal($"{uniqueAgentName}:Default Workflow - SystemWorkflow", uploadedDefinition.WorkflowType);
        Assert.True(uploadedDefinition.SystemScoped);
        Assert.Equal(2, uploadedDefinition.Workers);
    }

    public Task DisposeAsync()
    {
        _mockServer?.Stop();
        _mockServer?.Dispose();
        return Task.CompletedTask;
    }
}
