using System.Collections.Concurrent;
using System.Text.Json;
using Xians.Lib.Agents.A2A;
using Xians.Lib.Agents.Core;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// Real server integration tests for A2A (Agent-to-Agent) communication.
/// 
/// These tests verify A2A works correctly in Temporal contexts:
/// - ✅ Chat messages (routed to OnUserChatMessage handlers)
/// - ✅ Data messages (routed to OnUserDataMessage handlers)
/// 
/// Tests run actual Temporal workers and workflows to provide proper Temporal context.
/// 
/// dotnet test --filter "FullyQualifiedName~RealServerA2ATests"
/// 
/// Set SERVER_URL and API_KEY environment variables to run these tests.
/// </summary>
[Trait("Category", "RealServer")]
[Collection("RealServerA2A")] // Force sequential execution
public class RealServerA2ATests : RealServerTestBase, IAsyncLifetime
{
    private XiansPlatform? _platform;
    private XiansAgent? _agent;
    private CancellationTokenSource? _workerCts;
    private Task? _workerTask;
    
    private readonly string _agentName;
    private const string CHAT_TARGET_WORKFLOW = "A2AChatTarget";
    private const string DATA_TARGET_WORKFLOW = "A2ADataTarget";
    private const string SENDER_WORKFLOW = "A2ASender";
    private const string BUILTIN_TO_CUSTOM_WORKFLOW = "BuiltInToCustom";

    // Static result storage for cross-context verification
    private static readonly ConcurrentDictionary<string, A2ATestResult> _testResults = new();
    private static readonly ConcurrentDictionary<string, bool> _targetWorkflowExecuted = new();
    
    // Track custom workflow IDs for cleanup
    private readonly List<string> _customWorkflowIds = new();

    public RealServerA2ATests()
    {
        // Use fixed agent name for custom workflow tests to match workflow attributes
        _agentName = "A2ATestAgent";
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
        
        // Register agent (system-scoped to avoid tenant isolation issues in tests)
        _agent = _platform.Agents.Register(new XiansAgentRegistration 
        { 
            Name = _agentName,
            IsTemplate = true
        });

        // Define CHAT TARGET workflow - responds to chat messages
        var chatTargetWorkflow = _agent.Workflows.DefineBuiltIn(name: CHAT_TARGET_WORKFLOW);
        chatTargetWorkflow.OnUserChatMessage(async (context) =>
        {
            var testId = context.Message.ThreadId ?? context.Message.RequestId;
            Console.WriteLine($"[ChatTarget] Received: {context.Message.Text} (testId: {testId})");
            Console.WriteLine($"[ChatTarget] Context - Scope: {context.Message.Scope}, Hint: {context.Message.Hint}, Auth: {context.Message.Authorization}");
            Console.WriteLine($"[ChatTarget] Metadata count: {context.Metadata?.Count ?? 0}");
            _targetWorkflowExecuted[testId] = true; // Track target execution
            
            // Capture context fields for verification including Authorization and Metadata
            var contextResult = new A2ATestResult
            {
                ReceivedScope = context.Message.Scope,
                ReceivedHint = context.Message.Hint,
                ReceivedTenantId = context.Message.TenantId,
                ReceivedThreadId = context.Message.ThreadId,
                ReceivedParticipantId = context.Message.ParticipantId,
                ReceivedRequestId = context.Message.RequestId,
                ReceivedAuthorization = context.Message.Authorization,
                ReceivedMetadata = context.Metadata,
                Success = true
            };
            _testResults[$"{testId}-context"] = contextResult;
            
            var response = context.Message.Text + " world";
            Console.WriteLine($"[ChatTarget] Responding: {response}");
            await context.ReplyAsync(response);
        });

        // Define DATA TARGET workflow - responds to data messages
        var dataTargetWorkflow = _agent.Workflows.DefineBuiltIn(name: DATA_TARGET_WORKFLOW);
        dataTargetWorkflow.OnUserDataMessage(async (context) =>
        {
            var testId = context.Message.ThreadId ?? context.Message.RequestId;
            Console.WriteLine($"[DataTarget] Received data message (testId: {testId})");
            Console.WriteLine($"[DataTarget] Context - Scope: {context.Message.Scope}, Hint: {context.Message.Hint}, Auth: {context.Message.Authorization}");
            Console.WriteLine($"[DataTarget] Metadata count: {context.Metadata?.Count ?? 0}");
            _targetWorkflowExecuted[testId] = true; // Track target execution
            
            // Capture context fields for verification including Authorization and Metadata
            var contextResult = new A2ATestResult
            {
                ReceivedScope = context.Message.Scope,
                ReceivedHint = context.Message.Hint,
                ReceivedTenantId = context.Message.TenantId,
                ReceivedThreadId = context.Message.ThreadId,
                ReceivedParticipantId = context.Message.ParticipantId,
                ReceivedRequestId = context.Message.RequestId,
                ReceivedAuthorization = context.Message.Authorization,
                ReceivedMetadata = context.Metadata,
                Success = true
            };
            _testResults[$"{testId}-context"] = contextResult;
            
            // Parse the data from JSON (data is serialized through the pipeline)
            string originalValue = "unknown";
            try
            {
                var jsonElement = JsonSerializer.SerializeToElement(context.Message.Data);
                if (jsonElement.TryGetProperty("value", out var valueElement))
                {
                    originalValue = valueElement.GetString() ?? "unknown";
                }
            }
            catch { /* ignore parse errors */ }
            
            var responseData = new { 
                received = true, 
                originalValue = originalValue,
                processed = true 
            };
            
            Console.WriteLine($"[DataTarget] Responding with data, originalValue={originalValue}");
            await context.ReplyAsync("Data processed", data: responseData);
        });

        // Define SENDER workflow - sends A2A to both chat and data targets
        var senderWorkflow = _agent.Workflows.DefineBuiltIn(name: SENDER_WORKFLOW);
        
        // Handle chat messages - send A2A to chat target
        senderWorkflow.OnUserChatMessage(async (context) =>
        {
            var testId = context.Message.ThreadId ?? context.Message.RequestId;
            var text = context.Message.Text.StartsWith("chat:") 
                ? context.Message.Text[5..] 
                : context.Message.Text;
            Console.WriteLine($"[Sender] Received chat: {text} (testId: {testId})");
            
            try
            {
                Console.WriteLine($"[Sender] Sending chat A2A: {text}");
                
                // Create A2AMessage with context fields and custom metadata/authorization
                var a2aMessage = A2AMessage.FromContext(context, text: text);
                a2aMessage.Metadata = new Dictionary<string, string>
                {
                    ["test-key"] = "test-value",
                    ["source"] = "sender-workflow"
                };
                a2aMessage.Authorization = "Bearer test-token-123";
                
                var response = await XiansContext.A2A.SendChatToBuiltInAsync(
                    CHAT_TARGET_WORKFLOW,
                    a2aMessage);
                
                Console.WriteLine($"[Sender] Chat A2A response: {response.Text}");
                
                _testResults[testId] = new A2ATestResult
                {
                    ResponseText = response.Text,
                    Success = true
                };
                
                await context.ReplyAsync($"CHAT_OK: {response.Text}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sender] Error: {ex.Message}");
                _testResults[testId] = new A2ATestResult { Error = ex.Message, Success = false };
                await context.ReplyAsync($"ERROR: {ex.Message}");
            }
        });
        
        // Handle data messages - send A2A to data target
        senderWorkflow.OnUserDataMessage(async (context) =>
        {
            var testId = context.Message.ThreadId ?? context.Message.RequestId;
            Console.WriteLine($"[Sender] Received data message (testId: {testId})");
            
            try
            {
                Console.WriteLine($"[Sender] Sending data A2A");
                
                // Create A2AMessage with context fields and custom metadata/authorization
                var a2aMessage = A2AMessage.FromContext(context);
                a2aMessage.Metadata = new Dictionary<string, string>
                {
                    ["data-key"] = "data-value",
                    ["source"] = "sender-workflow-data"
                };
                a2aMessage.Authorization = "Bearer data-token-456";
                
                var response = await XiansContext.A2A.SendDataToBuiltInAsync(
                    DATA_TARGET_WORKFLOW,
                    a2aMessage);
                
                Console.WriteLine($"[Sender] Data A2A response: text={response.Text}");
                
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
                Console.WriteLine($"[Sender] Error: {ex.Message}");
                _testResults[testId] = new A2ATestResult { Error = ex.Message, Success = false };
                await context.ReplyAsync($"ERROR: {ex.Message}");
            }
        });

        // Define BUILTIN TO CUSTOM workflow - built-in workflow that sends A2A to custom workflows
        var builtInToCustomWorkflow = _agent.Workflows.DefineBuiltIn(name: BUILTIN_TO_CUSTOM_WORKFLOW);
        builtInToCustomWorkflow.OnUserChatMessage(async (context) =>
        {
            var testId = context.Message.ThreadId ?? context.Message.RequestId;
            // Extract target workflow ID from message (format: "target:workflowId")
            var targetWorkflowId = context.Message.Text.StartsWith("target:")
                ? context.Message.Text[7..]
                : string.Empty;
                
            Console.WriteLine($"[BuiltInToCustom] Received request to communicate with {targetWorkflowId} (testId: {testId})");
            
            try
            {
                // Step 1: Send a signal
                Console.WriteLine("[BuiltInToCustom] Sending signal...");
                await XiansContext.A2A.SendSignalAsync(
                    targetWorkflowId,
                    "ProcessRequest",
                    new { requestId = testId, action = "from-builtin", source = "built-in-workflow" });

                // Give signal time to process (using Task.Delay since we're in an activity)
                await Task.Delay(TimeSpan.FromSeconds(1));

                // Step 2: Send an update
                Console.WriteLine("[BuiltInToCustom] Sending update...");
                var updateResult = await XiansContext.A2A.UpdateAsync<ProcessResult>(
                    targetWorkflowId,
                    "ProcessUpdate",
                    new { requestId = testId, value = "builtin-data", source = "built-in-workflow" });

                Console.WriteLine($"[BuiltInToCustom] Update result: {updateResult.Status}");

                // Step 3: Query the state
                Console.WriteLine("[BuiltInToCustom] Querying state...");
                var queryResult = await XiansContext.A2A.QueryAsync<WorkflowState>(
                    targetWorkflowId,
                    "GetState",
                    testId);

                Console.WriteLine($"[BuiltInToCustom] Query result: ProcessedCount={queryResult.ProcessedCount}");

                // Step 4: Signal target to complete
                Console.WriteLine("[BuiltInToCustom] Signaling target to complete...");
                await XiansContext.A2A.SendSignalAsync(targetWorkflowId, "Complete");

                _testResults[testId] = new A2ATestResult
                {
                    ResponseText = $"builtin_to_custom|signal_sent|update:{updateResult.Status}|query:{queryResult.ProcessedCount}",
                    ResponseData = updateResult,
                    Success = true
                };

                Console.WriteLine("[BuiltInToCustom] All A2A operations to custom workflow completed successfully");
                await context.ReplyAsync($"SUCCESS: Signal/Update/Query completed, ProcessedCount={queryResult.ProcessedCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BuiltInToCustom] Error: {ex.Message}");
                Console.WriteLine($"[BuiltInToCustom] Error Type: {ex.GetType().FullName}");
                Console.WriteLine($"[BuiltInToCustom] Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[BuiltInToCustom] Inner Exception: {ex.InnerException.Message}");
                }
                _testResults[testId] = new A2ATestResult { Error = ex.Message, Success = false };
                await context.ReplyAsync($"ERROR: {ex.Message}");
            }
        });

        // Define CUSTOM TARGET workflow - handles signals, queries, and updates
        _agent.Workflows.DefineCustom<CustomTargetWorkflow>();
        
        // Define CUSTOM SENDER workflow - uses new A2A signal/query/update methods
        _agent.Workflows.DefineCustom<CustomSenderWorkflow>();

        // Upload workflow definitions
        await _agent.UploadWorkflowDefinitionsAsync();
        Console.WriteLine($"✓ Agent registered: {_agentName}");

        // Start workers to handle workflow executions
        _workerCts = new CancellationTokenSource();
        _workerTask = _agent.RunAllAsync(_workerCts.Token);
        
        // Give workers time to start
        await Task.Delay(1000);
        Console.WriteLine("✓ Workers started");
    }

    #region Chat Message Tests

    [Fact]
    public async Task A2A_ChatMessage_SendHello_ReturnsHelloWorld()
    {
        if (!RunRealServerTests) return;

        Console.WriteLine("=== A2A Chat Test: hello → hello world ===");

        var temporalClient = await _agent!.TemporalService!.GetClientAsync();
        var handle = await TemporalTestUtils.StartOrGetWorkflowAsync(
            temporalClient, _agentName, SENDER_WORKFLOW, systemScoped: true);

        Console.WriteLine($"✓ Sender workflow: {handle.Id}");

        var testId = $"chat-test-{Guid.NewGuid():N}";
        var message = TemporalTestUtils.CreateChatMessage(_agentName, "chat:hello", testId);

        Console.WriteLine($"✓ Sending signal: 'chat:hello' (testId: {testId})");
        await TemporalTestUtils.SendSignalAsync(handle, message);

        var result = await TemporalTestUtils.WaitForResultAsync(
            () => _testResults.TryGetValue(testId, out var r) ? r : null);

        Assert.NotNull(result);
        Assert.True(result.Success, $"A2A failed: {result.Error}");
        Assert.True(_targetWorkflowExecuted.ContainsKey(testId), 
            "Chat target workflow did not execute");
        Assert.Equal("hello world", result.ResponseText);

        // Verify context fields were properly passed to target
        var contextResult = await TemporalTestUtils.WaitForResultAsync(
            () => _testResults.TryGetValue($"{testId}-context", out var r) ? r : null,
            timeout: TimeSpan.FromSeconds(5));
        
        Assert.NotNull(contextResult);
        Assert.Equal("A2A", contextResult.ReceivedScope);
        Assert.NotNull(contextResult.ReceivedHint); // Hint is for message processing, not agent name
        Assert.NotNull(contextResult.ReceivedTenantId);
        Assert.Equal(testId, contextResult.ReceivedThreadId);
        Assert.NotNull(contextResult.ReceivedParticipantId); // Preserved from original context
        Assert.NotNull(contextResult.ReceivedRequestId); // Preserved from original context
        
        // Verify Authorization and Metadata
        Assert.Equal("Bearer test-token-123", contextResult.ReceivedAuthorization);
        Assert.NotNull(contextResult.ReceivedMetadata);
        Assert.Equal("test-value", contextResult.ReceivedMetadata["test-key"]);
        Assert.Equal("sender-workflow", contextResult.ReceivedMetadata["source"]);

        Console.WriteLine("✓ Chat A2A VERIFIED: 'hello' → 'hello world' (target executed, all context fields verified)");
    }

    #endregion

    #region Data Message Tests

    [Fact]
    public async Task A2A_DataMessage_SendData_ReturnsProcessedData()
    {
        if (!RunRealServerTests) return;

        Console.WriteLine("=== A2A Data Test: data message processing ===");

        var temporalClient = await _agent!.TemporalService!.GetClientAsync();
        var handle = await TemporalTestUtils.StartOrGetWorkflowAsync(
            temporalClient, _agentName, SENDER_WORKFLOW, systemScoped: true);

        Console.WriteLine($"✓ Sender workflow: {handle.Id}");

        var testId = $"data-test-{Guid.NewGuid():N}";
        var message = TemporalTestUtils.CreateDataMessage(
            _agentName, 
            new { value = "test-value-123" }, 
            text: "data:test-value-123",
            threadId: testId);

        Console.WriteLine($"✓ Sending data signal (testId: {testId})");
        await TemporalTestUtils.SendSignalAsync(handle, message);

        var result = await TemporalTestUtils.WaitForResultAsync(
            () => _testResults.TryGetValue(testId, out var r) ? r : null);

        Assert.NotNull(result);
        Assert.True(result.Success, $"A2A failed: {result.Error}");
        Assert.True(_targetWorkflowExecuted.ContainsKey(testId), 
            "Data target workflow did not execute");
        Assert.Equal("Data processed", result.ResponseText);
        Assert.NotNull(result.ResponseData);

        // Verify context fields were properly passed to target
        var contextResult = await TemporalTestUtils.WaitForResultAsync(
            () => _testResults.TryGetValue($"{testId}-context", out var r) ? r : null,
            timeout: TimeSpan.FromSeconds(5));
        
        Assert.NotNull(contextResult);
        Assert.Equal("A2A", contextResult.ReceivedScope);
        Assert.NotNull(contextResult.ReceivedHint); // Hint is for message processing, not agent name
        Assert.NotNull(contextResult.ReceivedTenantId);
        Assert.Equal(testId, contextResult.ReceivedThreadId);
        Assert.NotNull(contextResult.ReceivedParticipantId); // Preserved from original context
        Assert.NotNull(contextResult.ReceivedRequestId); // Preserved from original context
        
        // Verify Authorization and Metadata
        Assert.Equal("Bearer data-token-456", contextResult.ReceivedAuthorization);
        Assert.NotNull(contextResult.ReceivedMetadata);
        Assert.Equal("data-value", contextResult.ReceivedMetadata["data-key"]);
        Assert.Equal("sender-workflow-data", contextResult.ReceivedMetadata["source"]);

        Console.WriteLine($"✓ Data A2A VERIFIED: received processed response with data (target executed, all context fields verified)");
    }

    #endregion

    #region Custom Workflow Signal/Query/Update Tests

    [Fact]
    public async Task A2A_CustomWorkflow_SignalQueryUpdate_WorksCorrectly()
    {
        if (!RunRealServerTests) return;

        Console.WriteLine("=== A2A Custom Workflow Test: Signal → Update → Query ===");

        var temporalClient = await _agent!.TemporalService!.GetClientAsync();
        
        // Start the custom target workflow
        var targetWorkflowId = $"a2a-custom-target-{Guid.NewGuid():N}";
        var targetWorkflowType = $"{_agentName}:CustomTargetWorkflow";
        Console.WriteLine($"✓ Starting custom target workflow: {targetWorkflowId}");
        Console.WriteLine($"   Workflow type: {targetWorkflowType}");
        
        var targetHandle = await temporalClient.StartWorkflowAsync(
            (CustomTargetWorkflow wf) => wf.RunAsync(),
            new Temporalio.Client.WorkflowOptions
            {
                Id = targetWorkflowId,
                TaskQueue = Xians.Lib.Common.MultiTenancy.TenantContext.GetTaskQueueName(
                    targetWorkflowType,
                    systemScoped: true,
                    _agent!.Options!.CertificateTenantId),
                ExecutionTimeout = TemporalTestUtils.DefaultWorkflowExecutionTimeout
            });
        
        _customWorkflowIds.Add(targetWorkflowId); // Track for cleanup

        // Give workflow time to start
        await Task.Delay(500);
        Console.WriteLine($"✓ Custom target workflow started");

        // Start the custom sender workflow
        var senderWorkflowId = $"a2a-custom-sender-{Guid.NewGuid():N}";
        var senderWorkflowType = $"{_agentName}:CustomSenderWorkflow";
        Console.WriteLine($"✓ Starting custom sender workflow: {senderWorkflowId}");
        
        var testId = $"custom-test-{Guid.NewGuid():N}";
        
        var senderHandle = await temporalClient.StartWorkflowAsync(
            (CustomSenderWorkflow wf) => wf.RunAsync(targetWorkflowId, testId),
            new Temporalio.Client.WorkflowOptions
            {
                Id = senderWorkflowId,
                TaskQueue = Xians.Lib.Common.MultiTenancy.TenantContext.GetTaskQueueName(
                    senderWorkflowType,
                    systemScoped: true,
                    _agent!.Options!.CertificateTenantId),
                ExecutionTimeout = TemporalTestUtils.DefaultWorkflowExecutionTimeout
            });
        
        _customWorkflowIds.Add(senderWorkflowId); // Track for cleanup

        Console.WriteLine($"✓ Custom sender workflow started, executing A2A operations...");

        // Wait for all three operations to complete
        var result = await TemporalTestUtils.WaitForResultAsync(
            () => _testResults.TryGetValue(testId, out var r) ? r : null,
            timeout: TimeSpan.FromSeconds(15));

        Assert.NotNull(result);
        Assert.True(result.Success, $"A2A custom workflow test failed: {result.Error}");
        
        // Verify all three operations worked
        Assert.NotNull(result.ResponseText);
        Assert.Contains("signal_received", result.ResponseText);
        Assert.Contains("update_processed", result.ResponseText);
        Assert.Contains("query_returned", result.ResponseText);
        
        // Verify the data from update
        Assert.NotNull(result.ResponseData);

        Console.WriteLine($"✓ Custom Workflow A2A VERIFIED: Signal → Update → Query all succeeded");
        Console.WriteLine($"   Response: {result.ResponseText}");

        // Wait for workflows to complete naturally (they should complete after all operations)
        Console.WriteLine("⏳ Waiting for workflows to complete...");
        await Task.WhenAll(
            senderHandle.GetResultAsync(),
            targetHandle.GetResultAsync()
        ).WaitAsync(TimeSpan.FromSeconds(10));
        
        Console.WriteLine("✓ Workflows completed naturally");
    }

    [Fact]
    public async Task A2A_BuiltInToCustomWorkflow_SignalQueryUpdate_WorksCorrectly()
    {
        if (!RunRealServerTests) return;

        Console.WriteLine("=== A2A Built-In to Custom Workflow Test: Signal → Update → Query ===");

        var temporalClient = await _agent!.TemporalService!.GetClientAsync();
        
        // Start the custom target workflow
        var targetWorkflowId = $"a2a-custom-target-builtin-{Guid.NewGuid():N}";
        var targetWorkflowType = $"{_agentName}:CustomTargetWorkflow";
        Console.WriteLine($"✓ Starting custom target workflow: {targetWorkflowId}");
        Console.WriteLine($"   Workflow type: {targetWorkflowType}");
        
        var targetHandle = await temporalClient.StartWorkflowAsync(
            (CustomTargetWorkflow wf) => wf.RunAsync(),
            new Temporalio.Client.WorkflowOptions
            {
                Id = targetWorkflowId,
                TaskQueue = Xians.Lib.Common.MultiTenancy.TenantContext.GetTaskQueueName(
                    targetWorkflowType,
                    systemScoped: true,
                    _agent!.Options!.CertificateTenantId),
                ExecutionTimeout = TemporalTestUtils.DefaultWorkflowExecutionTimeout
            });
        
        _customWorkflowIds.Add(targetWorkflowId); // Track for cleanup

        // Give workflow time to start
        await Task.Delay(500);
        Console.WriteLine($"✓ Custom target workflow started");

        // Get or start the built-in workflow that will send A2A to custom
        var builtInHandle = await TemporalTestUtils.StartOrGetWorkflowAsync(
            temporalClient, _agentName, BUILTIN_TO_CUSTOM_WORKFLOW, systemScoped: true);

        Console.WriteLine($"✓ Built-in workflow: {builtInHandle.Id}");

        var testId = $"builtin-to-custom-test-{Guid.NewGuid():N}";
        var message = TemporalTestUtils.CreateChatMessage(
            _agentName, 
            $"target:{targetWorkflowId}", 
            testId);

        Console.WriteLine($"✓ Sending signal to built-in workflow (testId: {testId})");
        await TemporalTestUtils.SendSignalAsync(builtInHandle, message);

        // Wait for all operations to complete
        var result = await TemporalTestUtils.WaitForResultAsync(
            () => _testResults.TryGetValue(testId, out var r) ? r : null,
            timeout: TimeSpan.FromSeconds(15));

        Assert.NotNull(result);
        Assert.True(result.Success, $"Built-in to custom workflow test failed: {result.Error}");
        
        // Verify all operations worked
        Assert.NotNull(result.ResponseText);
        Assert.Contains("builtin_to_custom", result.ResponseText);
        Assert.Contains("signal_sent", result.ResponseText);
        Assert.Contains("update:", result.ResponseText);
        Assert.Contains("query:", result.ResponseText);
        
        // Verify the data from update
        Assert.NotNull(result.ResponseData);

        Console.WriteLine($"✓ Built-In to Custom Workflow A2A VERIFIED: Signal → Update → Query all succeeded");
        Console.WriteLine($"   Response: {result.ResponseText}");

        // Wait for custom target workflow to complete naturally
        Console.WriteLine("⏳ Waiting for custom target workflow to complete...");
        await targetHandle.GetResultAsync().WaitAsync(TimeSpan.FromSeconds(10));
        
        Console.WriteLine("✓ Custom target workflow completed naturally");
    }

    #endregion

    public class A2ATestResult
    {
        public string? ResponseText { get; set; }
        public object? ResponseData { get; set; }
        public string? Error { get; set; }
        public bool Success { get; set; }
        
        // Context fields received by target
        public string? ReceivedScope { get; set; }
        public string? ReceivedHint { get; set; }
        public string? ReceivedTenantId { get; set; }
        public string? ReceivedThreadId { get; set; }
        public string? ReceivedParticipantId { get; set; }
        public string? ReceivedRequestId { get; set; }
        public string? ReceivedAuthorization { get; set; }
        public Dictionary<string, string>? ReceivedMetadata { get; set; }
    }

    public async Task DisposeAsync()
    {
        await TerminateWorkflowsAsync();

        if (_workerCts != null)
        {
            _workerCts.Cancel();
            try
            {
                if (_workerTask != null)
                {
                    await _workerTask;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        try
        {
            XiansContext.Clear();
        }
        catch
        {
            // Ignore
        }
    }

    private async Task TerminateWorkflowsAsync()
    {
        if (_agent?.TemporalService == null) return;

        try
        {
            Console.WriteLine("Cleaning up test workflows...");
            var temporalClient = await _agent.TemporalService.GetClientAsync();
            
            // Terminate built-in workflows
            await TemporalTestUtils.TerminateBuiltInWorkflowsAsync(
                temporalClient, 
                _agentName, 
                new[] { SENDER_WORKFLOW, CHAT_TARGET_WORKFLOW, DATA_TARGET_WORKFLOW, BUILTIN_TO_CUSTOM_WORKFLOW });
            
            // Terminate custom workflows created during tests
            if (_customWorkflowIds.Count > 0)
            {
                Console.WriteLine($"Terminating {_customWorkflowIds.Count} custom workflows...");
                await TemporalTestUtils.TerminateCustomWorkflowsAsync(
                    temporalClient, 
                    _customWorkflowIds,
                    "Test cleanup");
            }
            
            Console.WriteLine("✓ All workflows terminated");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to terminate workflows: {ex.Message}");
        }
    }
}

/// <summary>
/// Custom workflow for testing A2A signal/query/update operations.
/// </summary>
[Temporalio.Workflows.Workflow("A2ATestAgent:CustomTargetWorkflow")]
public class CustomTargetWorkflow
{
    private readonly Queue<object> _requestQueue = new();
    private readonly Dictionary<string, ProcessResult> _results = new();
    private int _processedCount = 0;
    private bool _shouldComplete = false;

    [Temporalio.Workflows.WorkflowRun]
    public async Task RunAsync()
    {
        // Process requests until completion signal is received
        while (!_shouldComplete)
        {
            await Temporalio.Workflows.Workflow.WaitConditionAsync(
                () => _requestQueue.Count > 0 || _shouldComplete,
                TimeSpan.FromSeconds(30)); // Timeout after 30 seconds
            
            if (_shouldComplete)
            {
                break;
            }
            
            if (_requestQueue.Count > 0)
            {
                var request = _requestQueue.Dequeue();
                // Process the request
                await Temporalio.Workflows.Workflow.DelayAsync(TimeSpan.FromMilliseconds(100));
            }
        }
        
        Console.WriteLine("[CustomTarget] Workflow completing naturally");
    }

    /// <summary>
    /// Signal handler - receives async requests.
    /// </summary>
    [Temporalio.Workflows.WorkflowSignal("ProcessRequest")]
    public Task ProcessRequest(object request)
    {
        _requestQueue.Enqueue(request);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Signal handler - completes the workflow.
    /// </summary>
    [Temporalio.Workflows.WorkflowSignal("Complete")]
    public Task Complete()
    {
        _shouldComplete = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Query handler - returns current state.
    /// </summary>
    [Temporalio.Workflows.WorkflowQuery("GetState")]
    public WorkflowState GetState(string requestId)
    {
        return new WorkflowState
        {
            RequestId = requestId,
            ProcessedCount = _processedCount,
            QueueSize = _requestQueue.Count,
            ResultsCount = _results.Count
        };
    }

    /// <summary>
    /// Update handler - synchronous request-response.
    /// </summary>
    [Temporalio.Workflows.WorkflowUpdate("ProcessUpdate")]
    public Task<ProcessResult> ProcessUpdate(object request)
    {
        // Extract requestId from the request (it's an anonymous object)
        string requestId = "unknown";
        try
        {
            var jsonElement = System.Text.Json.JsonSerializer.SerializeToElement(request);
            if (jsonElement.TryGetProperty("requestId", out var idElement))
            {
                requestId = idElement.GetString() ?? "unknown";
            }
        }
        catch { }
        
        _processedCount++;
        
        var result = new ProcessResult
        {
            RequestId = requestId,
            Status = "Completed",
            ProcessedAt = DateTime.UtcNow
        };
        
        _results[requestId] = result;
        
        return Task.FromResult(result);
    }

    /// <summary>
    /// Validator for ProcessUpdate.
    /// </summary>
    [Temporalio.Workflows.WorkflowUpdateValidator(nameof(ProcessUpdate))]
    public void ValidateProcessUpdate(object request)
    {
        // Simple validation - ensure request is not null
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request), "Request cannot be null");
        }
    }
}

/// <summary>
/// Custom sender workflow that demonstrates using A2A signal/query/update methods.
/// </summary>
[Temporalio.Workflows.Workflow("A2ATestAgent:CustomSenderWorkflow")]
public class CustomSenderWorkflow
{
    // Get shared test results dictionary
    private static ConcurrentDictionary<string, RealServerA2ATests.A2ATestResult> GetTestResults()
    {
        var field = typeof(RealServerA2ATests).GetField("_testResults", 
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        return (ConcurrentDictionary<string, RealServerA2ATests.A2ATestResult>)field!.GetValue(null)!;
    }

    [Temporalio.Workflows.WorkflowRun]
    public async Task RunAsync(string targetWorkflowId, string testId)
    {
        var testResults = GetTestResults();
        
        Console.WriteLine($"[CustomSender] Starting A2A flow to workflow {targetWorkflowId} (testId: {testId})");

        try
        {
            // Step 1: Send a signal (fire-and-forget)
            Console.WriteLine("[CustomSender] Sending signal...");
            await Xians.Lib.Agents.Core.XiansContext.A2A.SendSignalAsync(
                targetWorkflowId,
                "ProcessRequest",
                new { requestId = testId, action = "initialize" });

            // Give signal time to process
            await Temporalio.Workflows.Workflow.DelayAsync(TimeSpan.FromSeconds(1));

            // Step 2: Send an update (synchronous request-response)
            Console.WriteLine("[CustomSender] Sending update...");
            var updateResult = await Xians.Lib.Agents.Core.XiansContext.A2A.UpdateAsync<ProcessResult>(
                targetWorkflowId,
                "ProcessUpdate",
                new { requestId = testId, value = "test-data" });

            Console.WriteLine($"[CustomSender] Update result: {updateResult.Status}");

            // Step 3: Query the state
            Console.WriteLine("[CustomSender] Querying state...");
            var queryResult = await Xians.Lib.Agents.Core.XiansContext.A2A.QueryAsync<WorkflowState>(
                targetWorkflowId,
                "GetState",
                testId);

            Console.WriteLine($"[CustomSender] Query result: ProcessedCount={queryResult.ProcessedCount}");

            // Step 4: Signal target to complete
            Console.WriteLine("[CustomSender] Signaling target to complete...");
            await Xians.Lib.Agents.Core.XiansContext.A2A.SendSignalAsync(
                targetWorkflowId,
                "Complete");

            testResults[testId] = new RealServerA2ATests.A2ATestResult
            {
                ResponseText = $"signal_received|update_processed:{updateResult.Status}|query_returned:{queryResult.ProcessedCount}",
                ResponseData = updateResult,
                Success = true
            };

            Console.WriteLine("[CustomSender] All A2A operations completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CustomSender] Error: {ex.Message}");
            testResults[testId] = new RealServerA2ATests.A2ATestResult 
            { 
                Error = ex.Message, 
                Success = false 
            };
        }
    }
}

/// <summary>
/// Result model for update operations.
/// </summary>
public class ProcessResult
{
    public string RequestId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}

/// <summary>
/// State model for query operations.
/// </summary>
public class WorkflowState
{
    public string RequestId { get; set; } = string.Empty;
    public int ProcessedCount { get; set; }
    public int QueueSize { get; set; }
    public int ResultsCount { get; set; }
}

/// <summary>
/// Collection definition to disable parallelization for A2A tests.
/// </summary>
[CollectionDefinition("RealServerA2A", DisableParallelization = true)]
public class RealServerA2ACollection
{
}
