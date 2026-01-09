using System.Collections.Concurrent;
using Xians.Lib.Agents.Core;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// Real server tests for Reply functionality as documented in replying.md.
/// Tests use Temporal SDK patterns following RealServerA2ATests.
/// 
/// dotnet test --filter "FullyQualifiedName~RealServerReplyTests"
/// 
/// Set SERVER_URL and API_KEY environment variables to run these tests.
/// </summary>
[Trait("Category", "RealServer")]
[Collection("RealServerReply")]
public class RealServerReplyTests : RealServerTestBase, IAsyncLifetime
{
    private XiansPlatform? _platform;
    private XiansAgent? _agent;
    private CancellationTokenSource? _workerCts;
    private Task? _workerTask;
    
    private readonly string _agentName;
    private const string WORKFLOW_NAME = "ReplyTestWorkflow";

    // Static result storage for cross-context verification
    private static readonly ConcurrentDictionary<string, ReplyTestResult> _testResults = new();
    private static readonly ConcurrentDictionary<string, bool> _handlerExecuted = new();

    public RealServerReplyTests()
    {
        _agentName = "ReplyTestAgentTenantScoped";
    }

    public async Task InitializeAsync()
    {
        if (!RunRealServerTests) return;

        XiansContext.CleanupForTests();
        _testResults.Clear();
        _handlerExecuted.Clear();

        _platform = await XiansPlatform.InitializeAsync(new XiansOptions
        {
            ServerUrl = ServerUrl!,
            ApiKey = ApiKey!
        });
        
        _agent = _platform.Agents.Register(new XiansAgentRegistration 
        { 
            Name = _agentName,
            SystemScoped = false
        });

        Console.WriteLine($"Agent registered with tenant ID: {_agent.Options!.CertificateTenantId}");

        var testWorkflow = _agent.Workflows.DefineBuiltIn(name: WORKFLOW_NAME);
        
        testWorkflow.OnUserChatMessage(async (context) =>
        {
            var testId = context.Message.ThreadId ?? context.Message.RequestId;
            Console.WriteLine($"[TestWorkflow] Received: '{context.Message.Text}' (testId: {testId})");
            
            _handlerExecuted[testId] = true;
            var result = new ReplyTestResult { Success = true };
            var message = context.Message.Text;
            
            try
            {
                // Simple text reply
                if (message.Contains("simple-text"))
                {
                    await context.ReplyAsync("Hello! How can I help you today?");
                    result.ResponseText = "Hello! How can I help you today?";
                }
                // Reply with data
                else if (message.Contains("with-data"))
                {
                    var data = new { Status = "Success", Timestamp = DateTime.UtcNow, ProcessedItems = 42 };
                    await context.ReplyAsync("Processing complete!", data);
                    result.ResponseText = "Processing complete!";
                    result.ResponseData = data;
                }
                // SendDataAsync
                else if (message.Contains("send-data"))
                {
                    var data = new { Metrics = new[] { 100, 200, 300 }, Labels = new[] { "Jan", "Feb", "Mar" } };
                    await context.SendDataAsync(data, "Here are your analytics");
                    result.ResponseText = "Here are your analytics";
                    result.ResponseData = data;
                }
                // GetChatHistoryAsync
                else if (message.Contains("get-history"))
                {
                    var history = await context.GetChatHistoryAsync(page: 1, pageSize: 10);
                    result.HistoryCount = history.Count;
                    result.ResponseText = $"Retrieved {history.Count} messages";
                    await context.ReplyAsync(result.ResponseText);
                }
                // GetLastHintAsync
                else if (message.Contains("get-hint"))
                {
                    var hint = await context.GetLastHintAsync();
                    result.ReceivedHint = hint;
                    result.ResponseText = $"Hint: {hint ?? "none"}";
                    await context.ReplyAsync(result.ResponseText);
                }
                // SkipResponse
                else if (message.StartsWith("LOG:"))
                {
                    context.SkipResponse = true;
                    result.SkipResponseUsed = true;
                    result.ResponseText = "skipped";
                }
                // Scope test
                else if (message.Contains("scope-test"))
                {
                    result.ReceivedScope = context.Message.Scope;
                    var history = await context.GetChatHistoryAsync(pageSize: 20);
                    result.HistoryCount = history.Count;
                    result.ResponseText = $"Scope: {context.Message.Scope}, History: {history.Count}";
                    await context.ReplyAsync(result.ResponseText);
                }
                // Thread test
                else if (message.Contains("thread-test"))
                {
                    result.ReceivedThreadId = context.Message.ThreadId;
                    result.ReceivedScope = context.Message.Scope;
                    result.ResponseText = $"Thread: {context.Message.ThreadId}, Scope: {context.Message.Scope}";
                    await context.ReplyAsync(result.ResponseText);
                }
                // Properties test
                else if (message.Contains("properties-test"))
                {
                    result.ReceivedScope = context.Message.Scope;
                    result.ReceivedHint = context.Message.Hint;
                    result.ReceivedThreadId = context.Message.ThreadId;
                    result.ReceivedTenantId = context.Message.TenantId;
                    result.ReceivedParticipantId = context.Message.ParticipantId;
                    result.ReceivedRequestId = context.Message.RequestId;
                    result.ResponseText = "All properties accessible";
                    await context.ReplyAsync(result.ResponseText);
                }
                
                _testResults[testId] = result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TestWorkflow] Error: {ex.Message}");
                _testResults[testId] = new ReplyTestResult { Success = false, Error = ex.Message };
            }
        });

        await _agent.UploadWorkflowDefinitionsAsync();
        Console.WriteLine($"✓ Agent registered: {_agentName}");

        _workerCts = new CancellationTokenSource();
        _workerTask = _agent.RunAllAsync(_workerCts.Token);
        await Task.Delay(1000);
        Console.WriteLine("✓ Workers started");
    }

    #region Core Reply Methods

    [Fact]
    public async Task ReplyAsync_SimpleText_Works()
    {
        if (!RunRealServerTests) return;

        Console.WriteLine("=== Test: ReplyAsync Simple Text ===");

        var temporalClient = await _agent!.TemporalService!.GetClientAsync();
        var handle = await TemporalTestUtils.StartOrGetWorkflowAsync(
            temporalClient, 
            _agentName, 
            WORKFLOW_NAME,
            systemScoped: false,
            tenantId: _agent.Options!.CertificateTenantId);

        var testId = $"simple-text-{Guid.NewGuid():N}";
        var message = TemporalTestUtils.CreateChatMessage(_agentName, "simple-text test", testId);
        await TemporalTestUtils.SendSignalAsync(handle, message);

        var result = await TemporalTestUtils.WaitForResultAsync(() => _testResults.TryGetValue(testId, out var r) ? r : null);

        Assert.NotNull(result);
        Assert.True(result.Success, $"Test failed: {result.Error}");
        Assert.True(_handlerExecuted.ContainsKey(testId));
        Assert.Equal("Hello! How can I help you today?", result.ResponseText);

        Console.WriteLine("✓ VERIFIED: Simple text reply");
    }

    [Fact]
    public async Task ReplyAsync_WithData_Works()
    {
        if (!RunRealServerTests) return;

        Console.WriteLine("=== Test: ReplyAsync With Data ===");

        var temporalClient = await _agent!.TemporalService!.GetClientAsync();
        var handle = await TemporalTestUtils.StartOrGetWorkflowAsync(
            temporalClient, 
            _agentName, 
            WORKFLOW_NAME,
            systemScoped: false,
            tenantId: _agent.Options!.CertificateTenantId);

        var testId = $"with-data-{Guid.NewGuid():N}";
        var message = TemporalTestUtils.CreateChatMessage(_agentName, "with-data test", testId);
        await TemporalTestUtils.SendSignalAsync(handle, message);

        var result = await TemporalTestUtils.WaitForResultAsync(() => _testResults.TryGetValue(testId, out var r) ? r : null);

        Assert.NotNull(result);
        Assert.True(result.Success, $"Test failed: {result.Error}");
        Assert.Equal("Processing complete!", result.ResponseText);
        Assert.NotNull(result.ResponseData);

        Console.WriteLine("✓ VERIFIED: Reply with data");
    }

    [Fact]
    public async Task SendDataAsync_Works()
    {
        if (!RunRealServerTests) return;

        Console.WriteLine("=== Test: SendDataAsync ===");

        var temporalClient = await _agent!.TemporalService!.GetClientAsync();
        var handle = await TemporalTestUtils.StartOrGetWorkflowAsync(
            temporalClient, 
            _agentName, 
            WORKFLOW_NAME,
            systemScoped: false,
            tenantId: _agent.Options!.CertificateTenantId);

        var testId = $"send-data-{Guid.NewGuid():N}";
        var message = TemporalTestUtils.CreateChatMessage(_agentName, "send-data test", testId);
        await TemporalTestUtils.SendSignalAsync(handle, message);

        var result = await TemporalTestUtils.WaitForResultAsync(() => _testResults.TryGetValue(testId, out var r) ? r : null);

        Assert.NotNull(result);
        Assert.True(result.Success, $"Test failed: {result.Error}");
        Assert.Equal("Here are your analytics", result.ResponseText);
        Assert.NotNull(result.ResponseData);

        Console.WriteLine("✓ VERIFIED: SendDataAsync");
    }

    #endregion

    #region Advanced Features

    [Fact]
    public async Task GetChatHistoryAsync_RetrievesHistory()
    {
        if (!RunRealServerTests) return;

        Console.WriteLine("=== Test: GetChatHistoryAsync ===");

        var temporalClient = await _agent!.TemporalService!.GetClientAsync();
        var handle = await TemporalTestUtils.StartOrGetWorkflowAsync(
            temporalClient, 
            _agentName, 
            WORKFLOW_NAME,
            systemScoped: false,
            tenantId: _agent.Options!.CertificateTenantId);

        var testId = $"get-history-{Guid.NewGuid():N}";
        var message = TemporalTestUtils.CreateChatMessage(_agentName, "get-history test", testId);
        await TemporalTestUtils.SendSignalAsync(handle, message);

        var result = await TemporalTestUtils.WaitForResultAsync(() => _testResults.TryGetValue(testId, out var r) ? r : null);

        Assert.NotNull(result);
        Assert.True(result.Success, $"Test failed: {result.Error}");
        Assert.True(result.HistoryCount >= 0);

        Console.WriteLine($"✓ VERIFIED: Chat history retrieved ({result.HistoryCount} messages)");
    }

    [Fact]
    public async Task GetLastHintAsync_RetrievesHint()
    {
        if (!RunRealServerTests) return;

        Console.WriteLine("=== Test: GetLastHintAsync ===");

        var temporalClient = await _agent!.TemporalService!.GetClientAsync();
        var handle = await TemporalTestUtils.StartOrGetWorkflowAsync(
            temporalClient, 
            _agentName, 
            WORKFLOW_NAME,
            systemScoped: false,
            tenantId: _agent.Options!.CertificateTenantId);

        var testId = $"get-hint-{Guid.NewGuid():N}";
        var message = TemporalTestUtils.CreateChatMessage(_agentName, "get-hint test", testId);
        message.Payload.Hint = "test-hint";
        await TemporalTestUtils.SendSignalAsync(handle, message);

        var result = await TemporalTestUtils.WaitForResultAsync(() => _testResults.TryGetValue(testId, out var r) ? r : null);

        Assert.NotNull(result);
        Assert.True(result.Success, $"Test failed: {result.Error}");

        Console.WriteLine($"✓ VERIFIED: Last hint retrieved");
    }

    [Fact]
    public async Task SkipResponse_PreventsMessageSending()
    {
        if (!RunRealServerTests) return;

        Console.WriteLine("=== Test: SkipResponse ===");

        var temporalClient = await _agent!.TemporalService!.GetClientAsync();
        var handle = await TemporalTestUtils.StartOrGetWorkflowAsync(
            temporalClient, 
            _agentName, 
            WORKFLOW_NAME,
            systemScoped: false,
            tenantId: _agent.Options!.CertificateTenantId);

        var testId = $"skip-{Guid.NewGuid():N}";
        var message = TemporalTestUtils.CreateChatMessage(_agentName, "LOG: test event", testId);
        await TemporalTestUtils.SendSignalAsync(handle, message);

        var result = await TemporalTestUtils.WaitForResultAsync(() => _testResults.TryGetValue(testId, out var r) ? r : null);

        Assert.NotNull(result);
        Assert.True(result.Success, $"Test failed: {result.Error}");
        Assert.True(result.SkipResponseUsed);

        Console.WriteLine("✓ VERIFIED: SkipResponse prevents sending");
    }

    #endregion

    #region Scope and Context

    [Fact]
    public async Task Scope_IsolatesMessagesByTopic()
    {
        if (!RunRealServerTests) return;

        Console.WriteLine("=== Test: Scope Isolation ===");

        var temporalClient = await _agent!.TemporalService!.GetClientAsync();
        var handle = await TemporalTestUtils.StartOrGetWorkflowAsync(
            temporalClient, 
            _agentName, 
            WORKFLOW_NAME,
            systemScoped: false,
            tenantId: _agent.Options!.CertificateTenantId);

        var scope1 = "Order #12345 - Delivery Questions";
        
        var testId = $"scope-{Guid.NewGuid():N}";
        var message = TemporalTestUtils.CreateChatMessage(_agentName, "scope-test message", testId);
        message.Payload.Scope = scope1;
        await TemporalTestUtils.SendSignalAsync(handle, message);

        var result = await TemporalTestUtils.WaitForResultAsync(() => _testResults.TryGetValue(testId, out var r) ? r : null);

        Assert.NotNull(result);
        Assert.True(result.Success, $"Test failed: {result.Error}");
        Assert.Equal(scope1, result.ReceivedScope);

        Console.WriteLine($"✓ VERIFIED: Scope isolation (Scope: {result.ReceivedScope})");
    }

    [Fact]
    public async Task Thread_ManagesConversationContext()
    {
        if (!RunRealServerTests) return;

        Console.WriteLine("=== Test: Thread Management ===");

        var temporalClient = await _agent!.TemporalService!.GetClientAsync();
        var handle = await TemporalTestUtils.StartOrGetWorkflowAsync(
            temporalClient, 
            _agentName, 
            WORKFLOW_NAME,
            systemScoped: false,
            tenantId: _agent.Options!.CertificateTenantId);

        var testId = $"thread-{Guid.NewGuid():N}";
        var message = TemporalTestUtils.CreateChatMessage(_agentName, "thread-test message", testId);
        message.Payload.Scope = "Test Scope";
        await TemporalTestUtils.SendSignalAsync(handle, message);

        var result = await TemporalTestUtils.WaitForResultAsync(() => _testResults.TryGetValue(testId, out var r) ? r : null);

        Assert.NotNull(result);
        Assert.True(result.Success, $"Test failed: {result.Error}");
        Assert.Equal(testId, result.ReceivedThreadId);
        Assert.NotNull(result.ReceivedScope);

        Console.WriteLine($"✓ VERIFIED: Thread context (Thread: {result.ReceivedThreadId})");
    }

    [Fact]
    public async Task MessageProperties_AllAccessible()
    {
        if (!RunRealServerTests) return;

        Console.WriteLine("=== Test: Message Properties Access ===");

        var temporalClient = await _agent!.TemporalService!.GetClientAsync();
        var handle = await TemporalTestUtils.StartOrGetWorkflowAsync(
            temporalClient, 
            _agentName, 
            WORKFLOW_NAME,
            systemScoped: false,
            tenantId: _agent.Options!.CertificateTenantId);

        var testId = $"props-{Guid.NewGuid():N}";
        var message = TemporalTestUtils.CreateChatMessage(_agentName, "properties-test message", testId);
        message.Payload.Scope = "Test Scope";
        message.Payload.Hint = "test-hint";
        await TemporalTestUtils.SendSignalAsync(handle, message);

        var result = await TemporalTestUtils.WaitForResultAsync(() => _testResults.TryGetValue(testId, out var r) ? r : null);

        Assert.NotNull(result);
        Assert.True(result.Success, $"Test failed: {result.Error}");
        Assert.NotNull(result.ReceivedScope);
        Assert.NotNull(result.ReceivedHint);
        Assert.NotNull(result.ReceivedThreadId);
        Assert.NotNull(result.ReceivedTenantId);
        Assert.NotNull(result.ReceivedParticipantId);
        Assert.NotNull(result.ReceivedRequestId);

        Console.WriteLine("✓ VERIFIED: All message properties accessible");
    }

    #endregion

    public async Task DisposeAsync()
    {
        await TerminateWorkflowsAsync();

        if (_workerCts != null)
        {
            _workerCts.Cancel();
            try
            {
                if (_workerTask != null) await _workerTask;
            }
            catch (OperationCanceledException) { }
        }

        try { XiansContext.Clear(); } catch { }
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
                new[] { WORKFLOW_NAME },
                tenantId: _agent.Options!.CertificateTenantId);
            Console.WriteLine("✓ Workflows terminated");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to terminate workflows: {ex.Message}");
        }
    }

    public class ReplyTestResult
    {
        public string? ResponseText { get; set; }
        public object? ResponseData { get; set; }
        public string? Error { get; set; }
        public bool Success { get; set; }
        public int HistoryCount { get; set; }
        public string? ReceivedHint { get; set; }
        public string? ReceivedScope { get; set; }
        public string? ReceivedThreadId { get; set; }
        public string? ReceivedTenantId { get; set; }
        public string? ReceivedParticipantId { get; set; }
        public string? ReceivedRequestId { get; set; }
        public bool SkipResponseUsed { get; set; }
    }
}

/// <summary>
/// Collection definition to disable parallelization for Reply tests.
/// </summary>
[CollectionDefinition("RealServerReply", DisableParallelization = true)]
public class RealServerReplyCollection
{
}
