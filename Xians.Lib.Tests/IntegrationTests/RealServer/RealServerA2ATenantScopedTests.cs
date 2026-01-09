using System.Collections.Concurrent;
using Xians.Lib.Agents.A2A;
using Xians.Lib.Agents.Core;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// Real server integration tests for A2A with TENANT-SCOPED agents.
/// 
/// This test class verifies that A2A works correctly when SystemScoped = FALSE:
/// - Task queues are prefixed with tenant ID
/// - Tenant isolation validation is enforced
/// - Workflow IDs must use the tenant ID from the API key certificate
/// 
/// Compare with RealServerA2ATests which uses SystemScoped = TRUE.
/// 
/// dotnet test --filter "FullyQualifiedName~RealServerA2ATenantScopedTests"
/// 
/// Set SERVER_URL and API_KEY environment variables to run these tests.
/// </summary>
[Trait("Category", "RealServer")]
[Collection("RealServerA2ATenantScoped")] // Force sequential execution, separate from system-scoped tests
public class RealServerA2ATenantScopedTests : RealServerTestBase, IAsyncLifetime
{
    private XiansPlatform? _platform;
    private XiansAgent? _agent;
    private CancellationTokenSource? _workerCts;
    private Task? _workerTask;
    
    private readonly string _agentName;
    private const string CHAT_TARGET_WORKFLOW = "A2AChatTargetTenantScoped";
    private const string SENDER_WORKFLOW = "A2ASenderTenantScoped";

    // Static result storage for cross-context verification
    private static readonly ConcurrentDictionary<string, A2ATestResult> _testResults = new();
    private static readonly ConcurrentDictionary<string, bool> _targetWorkflowExecuted = new();

    public RealServerA2ATenantScopedTests()
    {
        _agentName = "A2ATenantScopedTestAgent";
    }

    public async Task InitializeAsync()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        // Clean up static registries from previous tests
        XiansContext.CleanupForTests();

        // Clear any previous results
        _testResults.Clear();
        _targetWorkflowExecuted.Clear();

        // Initialize platform
        var options = new XiansOptions
        {
            ServerUrl = ServerUrl!,
            ApiKey = ApiKey!
        };

        _platform = await XiansPlatform.InitializeAsync(options);
        
        // IMPORTANT: SystemScoped = FALSE to test tenant-scoped behavior
        _agent = _platform.Agents.Register(new XiansAgentRegistration 
        { 
            Name = _agentName,
            SystemScoped = false  // ← This is the key difference!
        });

        Console.WriteLine($"Agent registered with tenant ID: {_agent.Options!.CertificateTenantId}");

        // Define CHAT TARGET workflow
        var chatTargetWorkflow = _agent.Workflows.DefineBuiltIn(name: CHAT_TARGET_WORKFLOW);
        chatTargetWorkflow.OnUserChatMessage(async (context) =>
        {
            var testId = context.Message.ThreadId ?? context.Message.RequestId;
            Console.WriteLine($"[ChatTarget-TenantScoped] Received: {context.Message.Text} (testId: {testId})");
            _targetWorkflowExecuted[testId] = true;
            
            var response = context.Message.Text + " world";
            await context.ReplyAsync(response);
        });

        // Define SENDER workflow
        var senderWorkflow = _agent.Workflows.DefineBuiltIn(name: SENDER_WORKFLOW);
        senderWorkflow.OnUserChatMessage(async (context) =>
        {
            var testId = context.Message.ThreadId ?? context.Message.RequestId;
            var text = context.Message.Text.StartsWith("chat:") 
                ? context.Message.Text[5..] 
                : context.Message.Text;
            
            try
            {
                var a2aMessage = A2AMessage.FromContext(context, text: text);
                var response = await XiansContext.A2A.SendChatToBuiltInAsync(
                    CHAT_TARGET_WORKFLOW,
                    a2aMessage);
                
                _testResults[testId] = new A2ATestResult
                {
                    ResponseText = response.Text,
                    Success = true
                };
                
                await context.ReplyAsync($"OK: {response.Text}");
            }
            catch (Exception ex)
            {
                _testResults[testId] = new A2ATestResult { Error = ex.Message, Success = false };
                await context.ReplyAsync($"ERROR: {ex.Message}");
            }
        });

        await _agent.UploadWorkflowDefinitionsAsync();
        Console.WriteLine($"✓ Tenant-scoped agent registered: {_agentName}");
        Console.WriteLine($"✓ Tenant ID from API key: {_agent.Options!.CertificateTenantId}");

        // Start workers
        _workerCts = new CancellationTokenSource();
        _workerTask = _agent.RunAllAsync(_workerCts.Token);
        
        await Task.Delay(1000);
        Console.WriteLine("✓ Workers started (tenant-scoped mode)");
        Console.WriteLine($"✓ Task queue format: {_agent.Options!.CertificateTenantId}:AgentName:WorkflowName");
    }

    [Fact]
    public async Task TenantScoped_A2A_ChatMessage_UsesCorrectTenantId()
    {
        if (!RunRealServerTests) return;

        Console.WriteLine("=== TENANT-SCOPED A2A Test ===");
        Console.WriteLine($"Agent tenant ID: {_agent!.Options!.CertificateTenantId}");

        var temporalClient = await _agent!.TemporalService!.GetClientAsync();
        
        // CRITICAL: Must pass systemScoped: false AND tenantId from API key
        var handle = await TemporalTestUtils.StartOrGetWorkflowAsync(
            temporalClient, 
            _agentName, 
            SENDER_WORKFLOW,
            systemScoped: false,  // ← Tenant-scoped
            tenantId: _agent.Options!.CertificateTenantId  // ← Use actual tenant from API key
        );

        Console.WriteLine($"✓ Workflow started: {handle.Id}");
        Console.WriteLine($"✓ Expected task queue: {_agent.Options!.CertificateTenantId}:{_agentName}:{SENDER_WORKFLOW}");

        var testId = $"tenant-scoped-test-{Guid.NewGuid():N}";
        var message = TemporalTestUtils.CreateChatMessage(_agentName, "chat:hello", testId);

        await TemporalTestUtils.SendSignalAsync(handle, message);

        var result = await TemporalTestUtils.WaitForResultAsync(
            () => _testResults.TryGetValue(testId, out var r) ? r : null);

        Assert.NotNull(result);
        Assert.True(result.Success, $"A2A failed: {result.Error}");
        Assert.True(_targetWorkflowExecuted.ContainsKey(testId), 
            "Chat target workflow did not execute");
        Assert.Equal("hello world", result.ResponseText);

        Console.WriteLine("✅ TENANT-SCOPED test PASSED!");
        Console.WriteLine("   - Task queue matched (tenant prefix required)");
        Console.WriteLine("   - Tenant isolation validation passed");
    }

    public async Task DisposeAsync()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        // Stop workers
        _workerCts?.Cancel();
        if (_workerTask != null)
        {
            try
            {
                await _workerTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _workerCts?.Dispose();
        Console.WriteLine("✓ Tenant-scoped test cleanup complete");
    }
}

// Shared test result class (same as main A2A tests)
public class A2ATestResult
{
    public string? ResponseText { get; set; }
    public object? ResponseData { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ReceivedScope { get; set; }
    public string? ReceivedHint { get; set; }
    public string? ReceivedTenantId { get; set; }
    public string? ReceivedThreadId { get; set; }
    public string? ReceivedParticipantId { get; set; }
    public string? ReceivedRequestId { get; set; }
    public string? ReceivedAuthorization { get; set; }
    public Dictionary<string, string>? ReceivedMetadata { get; set; }
}

/// <summary>
/// Test collection to force sequential execution.
/// </summary>
[CollectionDefinition("RealServerA2ATenantScoped", DisableParallelization = true)]
public class RealServerA2ATenantScopedCollection
{
}
