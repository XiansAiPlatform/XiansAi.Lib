using System.Reflection;
using Microsoft.Extensions.Logging;
using Server;
using DotNetEnv;
using XiansAi.Flow;
using XiansAi.Activity;
using Temporalio.Workflows;

namespace XiansAi.Lib.Tests.IntegrationTests;

public class FlowDefinitionUploaderTests
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly FlowDefinitionUploader _flowDefinitionUploader;
    private readonly string _certificateBase64;
    private readonly string _serverUrl;
    private readonly ILogger<FlowDefinitionUploaderTests> _logger;

    /*
    dotnet test --filter "FullyQualifiedName~FlowDefinitionUploaderTests"
    */
    public FlowDefinitionUploaderTests()
    {
        // Load environment variables
        Env.Load();

        // Get values from environment for SecureApi
        _certificateBase64 = Environment.GetEnvironmentVariable("APP_SERVER_API_KEY") ?? 
            throw new InvalidOperationException("APP_SERVER_API_KEY environment variable is not set");
        _serverUrl = Environment.GetEnvironmentVariable("APP_SERVER_URL") ?? 
            throw new InvalidOperationException("APP_SERVER_URL environment variable is not set");

        // Set up logger
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = _loggerFactory.CreateLogger<FlowDefinitionUploaderTests>();

        // Initialize SecureApi with real credentials
        SecureApi.InitializeClient(_certificateBase64, _serverUrl);
        var secureApiClient = SecureApi.Instance;

        // Create the flow definition uploader with real SecureApi
        _flowDefinitionUploader = new FlowDefinitionUploader();
    }

    /*
    dotnet test --filter "FullyQualifiedName~FlowDefinitionUploaderTests.UploadFlowDefinition_ShouldUploadWithActivities_WhenFlowWithActivitiesProvided"
    */
    [Fact]
    public async Task UploadFlowDefinition_ShouldUploadWithActivities_WhenFlowWithActivitiesProvided()
    {
        // Arrange - Create a test workflow with test activities
        var testFlow = new FlowInfo<TestWorkflowWithActivities>();
        
        // Add activities stub
        testFlow.AddActivities<ITestActivities>(new TestActivitiesImpl());

        // Act & Assert
        // The test passes if no exception is thrown during upload
        try {
            await _flowDefinitionUploader.UploadFlowDefinition(testFlow);
            _logger.LogInformation("Successfully uploaded workflow with activities definition to server");
        }
        catch (Exception ex)
        {
            if (ex is InvalidOperationException && ex.Message.Contains("Another user has already used this flow")) {
                _logger.LogInformation("Flow already exists, skipping upload");
            } else {
                _logger.LogError(ex, "Failed to upload workflow with activities definition to server");
                throw;
            }
        }
    }

}

// Test workflows and activities for testing

[Workflow("TestWorkflowWithActivities")]
public class TestWorkflowWithActivities
{
    [WorkflowRun]
    public async Task<string> RunAsync(string input)
    {
        // In the real implementation, the Workflow.ActivityStubs would be provided by the Temporal runtime
        // We don't need to actually execute the workflow for this test,
        // we're just testing that the definition uploads correctly
        await Task.Delay(1000);
        return $"Would process: {input}";
    }
}

[AgentTool("Test Agent Tool", AgentToolType.Custom)]
public interface ITestActivities
{
    [Temporalio.Activities.Activity]
    [Knowledge("Test Knowledge")]
    Task<string> DoSomethingAsync(string input);
}

public class TestActivitiesImpl : ActivityBase, ITestActivities
{
    public Task<string> DoSomethingAsync(string input)
    {
        return Task.FromResult($"Activity processed: {input}");
    }
} 