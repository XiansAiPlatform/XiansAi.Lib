using System.Collections.Concurrent;
using System.Text.Json;
using Xians.Lib.Agents.A2A;
using Xians.Lib.Agents.Core;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// Base class for A2A tests with configurable SystemScoped setting.
/// Allows testing both system-scoped and tenant-scoped scenarios with the same test logic.
/// </summary>
public abstract class RealServerA2ATestsBase : RealServerTestBase, IAsyncLifetime
{
    protected XiansPlatform? _platform;
    protected XiansAgent? _agent;
    protected CancellationTokenSource? _workerCts;
    protected Task? _workerTask;
    
    protected readonly string _agentName;
    protected const string CHAT_TARGET_WORKFLOW = "A2AChatTarget";
    protected const string DATA_TARGET_WORKFLOW = "A2ADataTarget";
    protected const string SENDER_WORKFLOW = "A2ASender";
    protected const string BUILTIN_TO_CUSTOM_WORKFLOW = "BuiltInToCustom";

    // Static result storage for cross-context verification
    protected static readonly ConcurrentDictionary<string, A2ATestResult> _testResults = new();
    protected static readonly ConcurrentDictionary<string, bool> _targetWorkflowExecuted = new();
    
    // Track custom workflow IDs for cleanup
    protected readonly List<string> _customWorkflowIds = new();

    // Abstract property to be overridden by derived classes
    protected abstract bool UseSystemScoped { get; }
    
    // Helper to get tenant ID for workflow starts
    protected string? GetTenantIdForWorkflowStart()
    {
        return UseSystemScoped ? null : _agent!.Options!.CertificateTenantId;
    }

    protected RealServerA2ATestsBase(string agentName)
    {
        _agentName = agentName;
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
        _customWorkflowIds.Clear();

        // Initialize platform
        var options = CreateTestOptions();

        _platform = await XiansPlatform.InitializeAsync(options);
        
        Console.WriteLine($"=== Testing with SystemScoped = {UseSystemScoped} ===");
        Console.WriteLine($"Tenant ID from API key: {_platform.Options.CertificateTenantId}");
        
        // Register agent with configurable SystemScoped
        _agent = _platform.Agents.Register(new XiansAgentRegistration 
        { 
            Name = _agentName,
            IsTemplate = UseSystemScoped
        });

        if (UseSystemScoped)
        {
            Console.WriteLine($"✓ System-scoped agent: Task queues will NOT have tenant prefix");
        }
        else
        {
            Console.WriteLine($"✓ Tenant-scoped agent: Task queues WILL have tenant prefix: {_agent.Options!.CertificateTenantId}:");
        }

        // Define CHAT TARGET workflow - responds to chat messages
        var chatTargetWorkflow = _agent.Workflows.DefineBuiltIn(name: CHAT_TARGET_WORKFLOW);
        chatTargetWorkflow.OnUserChatMessage(async (context) =>
        {
            var testId = context.Message.ThreadId ?? context.Message.RequestId;
            Console.WriteLine($"[ChatTarget] Received: {context.Message.Text} (testId: {testId})");
            _targetWorkflowExecuted[testId] = true;
            
            var response = context.Message.Text + " world";
            await context.ReplyAsync(response);
        });

        // Define DATA TARGET workflow - responds to data messages
        var dataTargetWorkflow = _agent.Workflows.DefineBuiltIn(name: DATA_TARGET_WORKFLOW);
        dataTargetWorkflow.OnUserDataMessage(async (context) =>
        {
            var testId = context.Message.ThreadId ?? context.Message.RequestId;
            _targetWorkflowExecuted[testId] = true;
            
            var responseData = new { received = true, processed = true };
            await context.ReplyAsync("Data processed", data: responseData);
        });

        // Define SENDER workflow - sends A2A to both chat and data targets
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
                
                await context.ReplyAsync($"CHAT_OK: {response.Text}");
            }
            catch (Exception ex)
            {
                _testResults[testId] = new A2ATestResult { Error = ex.Message, Success = false };
                await context.ReplyAsync($"ERROR: {ex.Message}");
            }
        });
        
        senderWorkflow.OnUserDataMessage(async (context) =>
        {
            var testId = context.Message.ThreadId ?? context.Message.RequestId;
            
            try
            {
                var a2aMessage = A2AMessage.FromContext(context);
                var response = await XiansContext.A2A.SendDataToBuiltInAsync(
                    DATA_TARGET_WORKFLOW,
                    a2aMessage);
                
                _testResults[testId] = new A2ATestResult
                {
                    ResponseText = response.Text,
                    ResponseData = response.Data,
                    Success = true
                };
                
                await context.ReplyAsync($"DATA_OK: {response.Text}");
            }
            catch (Exception ex)
            {
                _testResults[testId] = new A2ATestResult { Error = ex.Message, Success = false };
                await context.ReplyAsync($"ERROR: {ex.Message}");
            }
        });

        await _agent.UploadWorkflowDefinitionsAsync();
        Console.WriteLine($"✓ Agent registered: {_agentName}");

        // Start workers to handle workflow executions
        _workerCts = new CancellationTokenSource();
        _workerTask = _agent.RunAllAsync(_workerCts.Token);
        
        await Task.Delay(1000);
        Console.WriteLine("✓ Workers started");
    }

    protected async Task RunChatMessageTest()
    {
        if (!RunRealServerTests) return;

        Console.WriteLine($"=== A2A Chat Test (SystemScoped={UseSystemScoped}) ===");

        var temporalClient = await _agent!.TemporalService!.GetClientAsync();
        var handle = await TemporalTestUtils.StartOrGetWorkflowAsync(
            temporalClient, 
            _agentName, 
            SENDER_WORKFLOW,
            systemScoped: UseSystemScoped,
            tenantId: GetTenantIdForWorkflowStart());

        var testId = $"chat-test-{Guid.NewGuid():N}";
        var message = TemporalTestUtils.CreateChatMessage(_agentName, "chat:hello", testId);

        await TemporalTestUtils.SendSignalAsync(handle, message);

        var result = await TemporalTestUtils.WaitForResultAsync(
            () => _testResults.TryGetValue(testId, out var r) ? r : null);

        Assert.NotNull(result);
        Assert.True(result.Success, $"A2A failed: {result.Error}");
        Assert.True(_targetWorkflowExecuted.ContainsKey(testId), 
            "Chat target workflow did not execute");
        Assert.Equal("hello world", result.ResponseText);

        Console.WriteLine($"✅ Chat test PASSED (SystemScoped={UseSystemScoped})");
    }

    protected async Task RunDataMessageTest()
    {
        if (!RunRealServerTests) return;

        Console.WriteLine($"=== A2A Data Test (SystemScoped={UseSystemScoped}) ===");

        var temporalClient = await _agent!.TemporalService!.GetClientAsync();
        var handle = await TemporalTestUtils.StartOrGetWorkflowAsync(
            temporalClient, 
            _agentName, 
            SENDER_WORKFLOW,
            systemScoped: UseSystemScoped,
            tenantId: GetTenantIdForWorkflowStart());

        var testId = $"data-test-{Guid.NewGuid():N}";
        var testData = new { value = "test-data" };
        var message = TemporalTestUtils.CreateDataMessage(
            _agentName, 
            testData,
            threadId: testId);

        await TemporalTestUtils.SendSignalAsync(handle, message);

        var result = await TemporalTestUtils.WaitForResultAsync(
            () => _testResults.TryGetValue(testId, out var r) ? r : null);

        Assert.NotNull(result);
        Assert.True(result.Success, $"A2A failed: {result.Error}");
        Assert.True(_targetWorkflowExecuted.ContainsKey(testId), 
            "Data target workflow did not execute");
        Assert.Equal("Data processed", result.ResponseText);
        Assert.NotNull(result.ResponseData);

        Console.WriteLine($"✅ Data test PASSED (SystemScoped={UseSystemScoped})");
    }

    public async Task DisposeAsync()
    {
        if (!RunRealServerTests)
        {
            return;
        }

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
        Console.WriteLine($"✓ Test cleanup complete (SystemScoped={UseSystemScoped})");
    }
}
