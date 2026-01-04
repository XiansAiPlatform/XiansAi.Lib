using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// Real server tests for Webhook SDK.
/// These tests run against an actual Xians server to verify:
/// - Webhook handler registration
/// - Webhook HTTP endpoint
/// - Webhook response handling
/// - Error handling in webhooks
/// 
/// dotnet test --filter "Category=RealServer&FullyQualifiedName~RealServerWebhookTests" --logger "console;verbosity=detailed"
/// 
/// Set environment variables to run these tests:
/// - SERVER_URL: The Xians server URL (e.g., http://localhost:5005)
/// - API_KEY: Base64-encoded X.509 certificate for agent authentication
/// - WEBHOOK_API_KEY: API key for webhook endpoint (format: sk-Xnai-...)
/// </summary>
[Trait("Category", "RealServer")]
[Collection("RealServerWebhook")] // Force sequential execution
public class RealServerWebhookTests : RealServerTestBase, IAsyncLifetime
{
    private XiansPlatform? _platform;
    private XiansAgent? _agent;
    private readonly string _testParticipantId;
    private readonly string _testScope;
    private readonly string? _webhookApiKey;
    
    // Use unique agent name per test instance to avoid conflicts
    private readonly string _agentName;
    private const string WORKFLOW_NAME = "WebhookTestWorkflow";

    public RealServerWebhookTests()
    {
        // Use unique IDs for test isolation
        _testParticipantId = $"webhook-test-{Guid.NewGuid().ToString()[..8]}";
        _testScope = $"webhook-scope-{Guid.NewGuid().ToString()[..8]}";
        _agentName = $"WebhookTestAgent-{Guid.NewGuid().ToString()[..8]}";
        
        // Get webhook API key from environment (different from the certificate API_KEY)
        _webhookApiKey = Environment.GetEnvironmentVariable("WEBHOOK_API_KEY");
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        // No initialization needed - tests call InitializePlatformAsync as needed
        await Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        // Terminate workflows
        await TerminateWorkflowsAsync();

        // Clear the context to allow other tests to register agents
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
                _agentName, 
                new[] { WORKFLOW_NAME });
            
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

        var options = new XiansOptions
        {
            ServerUrl = ServerUrl!,
            ApiKey = ApiKey!
        };

        _platform = await XiansPlatform.InitializeAsync(options);
        
        // Register agent with unique name
        _agent = _platform.Agents.Register(new XiansAgentRegistration 
        { 
            Name = _agentName 
        });

        // Proactive cleanup: Terminate any lingering workflows from previous failed test runs
        // This prevents message queue congestion
        try
        {
            var temporalClient = await _agent.TemporalService!.GetClientAsync();
            await TemporalTestUtils.TerminateBuiltInWorkflowsAsync(
                temporalClient, 
                _agentName, 
                new[] { WORKFLOW_NAME });
            Console.WriteLine($"✓ Cleaned up any lingering workflows for {_agentName}");
        }
        catch
        {
            // Ignore if no workflows to clean up
        }
    }

    [Fact]
    public async Task Webhook_SuccessResponse_ShouldReturnCorrectData()
    {
        // Skip if no credentials
        if (!RunRealServerTests)
        {
            Console.WriteLine("Skipping test - no SERVER_URL or API_KEY configured");
            return;
        }

        if (string.IsNullOrEmpty(_webhookApiKey))
        {
            Console.WriteLine("Skipping test - no WEBHOOK_API_KEY configured");
            Console.WriteLine("Set WEBHOOK_API_KEY environment variable to an API key starting with 'sk-Xnai-'");
            return;
        }

        await InitializePlatformAsync();

        // Define workflow with webhook handler
        var workflow = _agent!.Workflows.DefineBuiltIn(name: WORKFLOW_NAME);
        
        var receivedWebhookName = "";
        var receivedParticipantId = "";
        var receivedScope = "";
        
        workflow.OnWebhook(async (context) =>
        {
            receivedWebhookName = context.Webhook.Name;
            receivedParticipantId = context.Webhook.ParticipantId;
            receivedScope = context.Webhook.Scope ?? "";
            
            // Set successful response with custom headers
            context.Response = new WebhookResponse
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonSerializer.Serialize(new 
                { 
                    message = "Webhook processed successfully",
                    webhookName = context.Webhook.Name,
                    agent = _agentName,
                    timestamp = DateTime.UtcNow
                }),
                ContentType = "application/json",
                Headers = new Dictionary<string, string[]>
                {
                    ["X-Webhook-Processed"] = new[] { "true" },
                    ["X-Agent-Name"] = new[] { _agentName }
                }
            };
            
            await Task.CompletedTask;
        });

        // Upload workflow definitions to server
        await _agent.UploadWorkflowDefinitionsAsync();
        Console.WriteLine($"✓ Workflow definition uploaded for {_agentName}");
        
        // Wait for definition to propagate
        await Task.Delay(500);

        // Start workflow in background
        var cts = new CancellationTokenSource();
        var workflowTask = Task.Run(() => workflow.RunAsync(cts.Token));
        
        // Wait longer for workflow to be ready (especially important when running many tests)
        await Task.Delay(5000);

        try
        {
            // Use plain HTTP client for webhook (external system simulation)
            // Webhook endpoint uses API key auth, not certificate auth
            using var httpClient = new HttpClient();
            var webhookName = "EmailReceived";
            var webhookUrl = $"{ServerUrl}/api/user/webhooks/builtin" +
                $"?apikey={_webhookApiKey}" +
                $"&timeoutSeconds=30" +
                $"&agentName={_agentName}" +
                $"&workflowName={WORKFLOW_NAME}" +
                $"&webhookName={webhookName}" +
                $"&scope={_testScope}" +
                $"&authorization=test-jwt-token" +
                $"&participantId={_testParticipantId}";

            var requestBody = new
            {
                email = "test@example.com",
                subject = "Test Email",
                body = "This is a test email from webhook"
            };

            var response = await httpClient.PostAsJsonAsync(webhookUrl, requestBody);

            // Validate HTTP response status code (applied directly from WebhookResponse)
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Validate content type header
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

            // Validate custom headers
            Assert.True(response.Headers.TryGetValues("X-Webhook-Processed", out var processedValues));
            Assert.Equal("true", processedValues.First());
            
            Assert.True(response.Headers.TryGetValues("X-Agent-Name", out var agentValues));
            Assert.Equal(_agentName, agentValues.First());

            // Validate response content (applied directly, not wrapped)
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response Content: {responseContent}");
            
            Assert.Contains("Webhook processed successfully", responseContent);
            Assert.Contains(webhookName, responseContent);
            Assert.Contains(_agentName, responseContent);

            // Verify webhook context received correct data
            Assert.Equal(webhookName, receivedWebhookName);
            Assert.Equal(_testParticipantId, receivedParticipantId);
            Assert.Equal(_testScope, receivedScope);
        }
        finally
        {
            cts.Cancel();
            try
            {
                await workflowTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }
    }

    [Fact]
    public async Task Webhook_ErrorInHandler_ShouldReturnErrorResponse()
    {
        // Skip if no credentials
        if (!RunRealServerTests)
        {
            Console.WriteLine("Skipping test - no SERVER_URL or API_KEY configured");
            return;
        }

        if (string.IsNullOrEmpty(_webhookApiKey))
        {
            Console.WriteLine("Skipping test - no WEBHOOK_API_KEY configured");
            return;
        }

        await InitializePlatformAsync();

        // Define workflow with webhook handler that throws error
        var workflow = _agent!.Workflows.DefineBuiltIn(name: WORKFLOW_NAME);
        
        #pragma warning disable CS1998 // Async method lacks 'await' operators
        workflow.OnWebhook(async (context) =>
        {
            // Simulate an error
            throw new InvalidOperationException("Test error in webhook handler");
        });
        #pragma warning restore CS1998

        // Upload workflow definitions to server
        await _agent.UploadWorkflowDefinitionsAsync();
        
        // Wait for definition to propagate
        await Task.Delay(500);

        // Start workflow in background
        var cts = new CancellationTokenSource();
        var workflowTask = Task.Run(() => workflow.RunAsync(cts.Token));
        
        // Wait longer for workflow to be ready (especially important when running many tests)
        await Task.Delay(5000);

        try
        {
            // Use plain HTTP client for webhook (external system simulation)
            // Webhook endpoint uses API key auth, not certificate auth
            using var httpClient = new HttpClient();
            var webhookUrl = $"{ServerUrl}/api/user/webhooks/builtin" +
                $"?apikey={_webhookApiKey}" +
                $"&timeoutSeconds=30" +
                $"&agentName={_agentName}" +
                $"&workflowName={WORKFLOW_NAME}" +
                $"&webhookName=ErrorTest" +
                $"&participantId={_testParticipantId}";

            var response = await httpClient.PostAsJsonAsync(webhookUrl, new { test = "data" });

            // Validate HTTP response status code (should be 500 from automatic error handling)
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Error Response: {responseContent}");
            Console.WriteLine($"Error Status: {response.StatusCode}");

            // Validate error content
            Assert.NotNull(responseContent);
            Assert.Contains("error", responseContent.ToLower());
        }
        finally
        {
            cts.Cancel();
            try
            {
                await workflowTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }
    }

    [Fact]
    public async Task Webhook_CustomErrorResponse_ShouldReturnCustomError()
    {
        // Skip if no credentials
        if (!RunRealServerTests)
        {
            Console.WriteLine("Skipping test - no SERVER_URL or API_KEY configured");
            return;
        }

        if (string.IsNullOrEmpty(_webhookApiKey))
        {
            Console.WriteLine("Skipping test - no WEBHOOK_API_KEY configured");
            return;
        }

        await InitializePlatformAsync();

        // Define workflow with webhook handler that returns custom error
        var workflow = _agent!.Workflows.DefineBuiltIn(name: WORKFLOW_NAME);
        
        workflow.OnWebhook(async (context) =>
        {
            // Validate and return custom error - check for empty or null payload
            var payloadStr = context.Webhook.Payload?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(payloadStr))
            {
                context.Response = WebhookResponse.BadRequest("Payload is required");
                return;
            }
            
            // Success response
            context.Response = WebhookResponse.Ok(new { status = "success" });
            await Task.CompletedTask;
        });

        // Upload workflow definitions to server
        await _agent.UploadWorkflowDefinitionsAsync();
        
        // Wait for definition to propagate
        await Task.Delay(500);

        // Start workflow in background
        var cts = new CancellationTokenSource();
        var workflowTask = Task.Run(() => workflow.RunAsync(cts.Token));
        
        // Wait longer for workflow to be ready (especially important when running many tests)
        await Task.Delay(5000);

        try
        {
            // Use plain HTTP client for webhook (external system simulation)
            // Webhook endpoint uses API key auth, not certificate auth
            using var httpClient = new HttpClient();
            var webhookUrl = $"{ServerUrl}/api/user/webhooks/builtin" +
                $"?apikey={_webhookApiKey}" +
                $"&timeoutSeconds=30" +
                $"&agentName={_agentName}" +
                $"&workflowName={WORKFLOW_NAME}" +
                $"&webhookName=ValidationTest" +
                $"&participantId={_testParticipantId}";

            // Send empty body
            var response = await httpClient.PostAsync(webhookUrl, new StringContent(""));

            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"=== Validation Test Response ===");
            Console.WriteLine($"Status: {response.StatusCode}");
            Console.WriteLine($"Content: {responseContent}");
            Console.WriteLine($"================================");

            // Validate HTTP response status code (should be 400 from custom error)
            // If this fails, the server had an error deserializing the webhook response
            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                throw new Exception($"Server returned 500 error instead of webhook response. Response: {responseContent}");
            }

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            // Validate error message in content
            Assert.NotNull(responseContent);
            Assert.Contains("Payload is required", responseContent);
        }
        finally
        {
            cts.Cancel();
            try
            {
                await workflowTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }
    }

    [Fact]
    public async Task Webhook_WithAuthorization_ShouldReceiveAuthToken()
    {
        // Skip if no credentials
        if (!RunRealServerTests)
        {
            Console.WriteLine("Skipping test - no SERVER_URL or API_KEY configured");
            return;
        }

        if (string.IsNullOrEmpty(_webhookApiKey))
        {
            Console.WriteLine("Skipping test - no WEBHOOK_API_KEY configured");
            return;
        }

        await InitializePlatformAsync();

        // Define workflow with webhook handler
        var workflow = _agent!.Workflows.DefineBuiltIn(name: WORKFLOW_NAME);
        
        var receivedAuthorization = "";
        
        workflow.OnWebhook(async (context) =>
        {
            receivedAuthorization = context.Webhook.Authorization ?? "";
            
            context.Response = WebhookResponse.Ok(new 
            { 
                authorized = !string.IsNullOrEmpty(context.Webhook.Authorization)
            });
            
            await Task.CompletedTask;
        });

        // Upload workflow definitions to server
        await _agent.UploadWorkflowDefinitionsAsync();
        
        // Wait for definition to propagate
        await Task.Delay(500);

        // Start workflow in background
        var cts = new CancellationTokenSource();
        var workflowTask = Task.Run(() => workflow.RunAsync(cts.Token));
        
        // Wait longer for workflow to be ready (especially important when running many tests)
        await Task.Delay(5000);

        try
        {
            // Use plain HTTP client for webhook (external system simulation)
            // Webhook endpoint uses API key auth, not certificate auth
            using var httpClient = new HttpClient();
            var authToken = "Bearer test-jwt-token-12345";
            var webhookUrl = $"{ServerUrl}/api/user/webhooks/builtin" +
                $"?apikey={_webhookApiKey}" +
                $"&timeoutSeconds=30" +
                $"&agentName={_agentName}" +
                $"&workflowName={WORKFLOW_NAME}" +
                $"&webhookName=AuthTest" +
                $"&authorization={Uri.EscapeDataString(authToken)}" +
                $"&participantId={_testParticipantId}";

            var response = await httpClient.PostAsJsonAsync(webhookUrl, new { test = "data" });

            // Validate HTTP response
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Auth Response Content: {responseContent}");
            
            // Validate response indicates authorized
            Assert.NotNull(responseContent);
            Assert.Contains("\"authorized\":true", responseContent);

            // Verify authorization token was received
            Assert.Equal(authToken, receivedAuthorization);
        }
        finally
        {
            cts.Cancel();
            try
            {
                await workflowTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }
    }

    [Fact]
    public async Task Webhook_HelperMethods_ShouldWorkCorrectly()
    {
        // Skip if no credentials
        if (!RunRealServerTests)
        {
            Console.WriteLine("Skipping test - no SERVER_URL or API_KEY configured");
            return;
        }

        if (string.IsNullOrEmpty(_webhookApiKey))
        {
            Console.WriteLine("Skipping test - no WEBHOOK_API_KEY configured");
            return;
        }

        await InitializePlatformAsync();

        // Define workflow with webhook handler using helper methods
        var workflow = _agent!.Workflows.DefineBuiltIn(name: WORKFLOW_NAME);
        
        workflow.OnWebhook(async (context) =>
        {
            // Use Respond helper method
            context.Respond(new 
            { 
                status = "processed",
                webhookName = context.Webhook.Name,
                processedAt = DateTime.UtcNow
            });
            
            await Task.CompletedTask;
        });

        // Upload workflow definitions to server
        await _agent.UploadWorkflowDefinitionsAsync();
        
        // Wait for definition to propagate
        await Task.Delay(500);

        // Start workflow in background
        var cts = new CancellationTokenSource();
        var workflowTask = Task.Run(() => workflow.RunAsync(cts.Token));
        
        // Wait longer for workflow to be ready (especially important when running many tests)
        await Task.Delay(5000);

        try
        {
            // Use plain HTTP client for webhook (external system simulation)
            // Webhook endpoint uses API key auth, not certificate auth
            using var httpClient = new HttpClient();
            var webhookUrl = $"{ServerUrl}/api/user/webhooks/builtin" +
                $"?apikey={_webhookApiKey}" +
                $"&timeoutSeconds=30" +
                $"&agentName={_agentName}" +
                $"&workflowName={WORKFLOW_NAME}" +
                $"&webhookName=HelperTest" +
                $"&participantId={_testParticipantId}";

            var response = await httpClient.PostAsJsonAsync(webhookUrl, new { data = "test" });

            // Validate HTTP response
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Helper Response Content: {responseContent}");
            
            // Validate response content (applied directly)
            Assert.NotNull(responseContent);
            Assert.Contains("processed", responseContent);
            Assert.Contains("HelperTest", responseContent);
        }
        finally
        {
            cts.Cancel();
            try
            {
                await workflowTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }
    }
}

