using Xians.Lib.Agents.Core;
using Xians.Lib.Common.Caching;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Configuration.Models;

namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// Tests for workflow and agent functionality against a real server.
/// These tests verify end-to-end integration with the Xians platform.
/// </summary>
[Trait("Category", "RealServer")]
public class RealServerWorkflowTests : RealServerTestBase
{
    // Use fixed agent name to avoid spamming the server with test agents
    private const string AGENT_NAME = "WorkflowTestAgent";
    
    //dotnet test --filter "FullyQualifiedName~RealServerWorkflowTests"

    [Fact]
    public async Task RealServer_EndToEndTest()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        Console.WriteLine($"Testing against REAL server: {ServerUrl}");
        
        // Step 1: Create HTTP client
        var config = new ServerConfiguration
        {
            ServerUrl = ServerUrl!,
            ApiKey = ApiKey!
        };
        
        using var httpService = ServiceFactory.CreateHttpClientService(config);
        
        // Step 2: Test connection
        var isHealthy = await httpService.IsHealthyAsync();
        Assert.True(isHealthy, "Server should be reachable");
        Console.WriteLine("✓ Step 1: HTTP connection successful");
        
        // Step 3: Fetch settings
        var settings = await SettingsService.GetSettingsAsync(httpService);
        Assert.NotNull(settings);
        Assert.NotEmpty(settings.FlowServerUrl);
        Console.WriteLine($"✓ Step 2: Settings fetched - Temporal: {settings.FlowServerUrl}");
        
        // Step 4: Create Temporal client with fetched settings
        var temporalConfig = settings.ToTemporalConfiguration();
        using var temporalService = ServiceFactory.CreateTemporalClientService(temporalConfig);
        Assert.NotNull(temporalService);
        Console.WriteLine("✓ Step 3: Temporal client created");
        
        // Note: We don't try to connect to Temporal here as it may not be accessible
        // The important test is that we successfully fetched the config from the server
        
        Console.WriteLine("✓ End-to-end test PASSED - Successfully connected to real server!");
    }

    [Fact]
    public async Task WorkflowDefinition_ShouldUploadToRealServer()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        Console.WriteLine($"Testing workflow definition upload to REAL server: {ServerUrl}");
        
        // Arrange - Initialize XiansPlatform with real server credentials
        var platform = XiansPlatform.Initialize(new XiansOptions
        {
            ServerUrl = ServerUrl!,
            ApiKey = ApiKey!
        });

        // Act - Register agent with fixed name to avoid spamming server
        var agent = platform.Agents.Register(new XiansAgentRegistration
        {
            Name = AGENT_NAME,
            SystemScoped = false
        });
        
        Console.WriteLine($"✓ Step 1: Registered agent '{AGENT_NAME}'");

        // Define workflows
        var workflow = agent.Workflows.DefineBuiltIn(workers: 1, name: "Conversational");
        var workflow2 = agent.Workflows.DefineBuiltIn(workers: 1, name: "Webhooks");
        
        // Upload all workflow definitions to server
        await agent.UploadWorkflowDefinitionsAsync();
        
        Console.WriteLine("✓ Step 2: Defined and uploaded workflow definitions");
        
        // Assert
        Assert.NotNull(workflow);
        Assert.Equal($"{AGENT_NAME}:BuiltIn Workflow - Conversational", workflow.WorkflowType);
        Assert.Equal("Conversational", workflow.Name);
        Assert.Equal(1, workflow.Workers);
        Assert.NotNull(workflow2);
        Assert.Equal($"{AGENT_NAME}:BuiltIn Workflow - Webhooks", workflow2.WorkflowType);
        Assert.Equal("Webhooks", workflow2.Name);
        Assert.Equal(1, workflow2.Workers);
    }
}

