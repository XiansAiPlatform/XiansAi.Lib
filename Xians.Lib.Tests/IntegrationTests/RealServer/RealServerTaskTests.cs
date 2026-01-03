using System.Collections.Concurrent;
using Temporalio.Client;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Agents.Tasks.Models;
using Xians.Lib.Tests.TestUtilities;
using Xians.Lib.Workflows.Messaging.Models;

namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// Real server integration tests for TaskCollection and task lifecycle management.
/// 
/// These tests verify TaskCollection functionality:
/// - ✅ Task creation via client API
/// - ✅ UpdateDraftAsync (from client)
/// - ✅ CompleteTaskAsync (from client)
/// - ✅ RejectTaskAsync (from client)
/// - ✅ QueryTaskInfoAsync (from client)
/// - ✅ Full task lifecycle validation
/// 
/// The TaskCollection API automatically handles Temporal workflows via the Platform agent.
/// These tests just need to initialize the platform and use the client-side API.
/// 
/// dotnet test --filter "FullyQualifiedName~RealServerTaskTests"
/// 
/// Set SERVER_URL and API_KEY environment variables to run these tests.
/// </summary>
[Trait("Category", "RealServer")]
[Collection("RealServerTask")] // Force sequential execution
public class RealServerTaskTests : RealServerTestBase, IAsyncLifetime
{
    private XiansPlatform? _platform;
    private XiansAgent? _platformAgent;
    private CancellationTokenSource? _workerCts;
    private Task? _workerTask;
    
    // Track task IDs for cleanup
    private readonly List<string> _taskIds = new();

    public RealServerTaskTests()
    {
    }

    public async Task InitializeAsync()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        // Clear any previous task IDs
        _taskIds.Clear();

        // Initialize platform
        var options = new XiansOptions
        {
            ServerUrl = ServerUrl!,
            ApiKey = ApiKey!
        };

        _platform = await XiansPlatform.InitializeAsync(options);
        
        // Register Platform agent for task workflows
        _platformAgent = _platform.Agents.Register(new XiansAgentRegistration 
        { 
            Name = "Platform",
            SystemScoped = true
        });

        // Upload workflow definitions (Platform agent has the Task Workflow)
        await _platformAgent.UploadWorkflowDefinitionsAsync();

        // Start Platform agent worker to handle task workflows
        _workerCts = new CancellationTokenSource();
        _workerTask = _platformAgent.RunAllAsync(_workerCts.Token);

        // Wait for worker to be ready
        await Task.Delay(2000);

        Console.WriteLine("✓ Test setup complete - Platform agent worker running");
    }

    public async Task DisposeAsync()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        try
        {
            Console.WriteLine("\n🧹 Cleaning up test resources...");

            // Stop workers
            if (_workerCts != null)
            {
                _workerCts.Cancel();
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
            }

            if (_platformAgent?.TemporalService != null)
            {
                var client = await _platformAgent.TemporalService.GetClientAsync();

                // Terminate task workflows
                foreach (var taskId in _taskIds)
                {
                    var workflowId = $"test:Platform:Task Workflow:{taskId}";
                    await TemporalTestUtils.TerminateWorkflowIfRunningAsync(client, workflowId, "Test cleanup");
                }
            }

            // Clear context
            try
            {
                XiansContext.Clear();
            }
            catch
            {
                // Ignore cleanup errors
            }

            Console.WriteLine("✓ Cleanup complete\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Cleanup error: {ex.Message}");
        }
    }

    [Fact]
    public async Task QueryTaskInfoAsync_ShouldReturnTaskInformation()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var taskId = $"task-query-{Guid.NewGuid():N}";
        _taskIds.Add(taskId);
        
        Console.WriteLine($"\n▶ Test: QueryTaskInfoAsync (taskId: {taskId})");

        try
        {
            var client = await _platformAgent!.TemporalService!.GetClientAsync();
            
            // Start a task workflow directly from the client
            var request = new TaskWorkflowRequest
            {
                TaskId = taskId,
                Title = "Test Query Task",
                Description = "Testing task query functionality",
                ParticipantId = "test-reviewer",
                DraftWork = "Initial draft for query test"
            };

            // Start the task workflow
            await client.StartWorkflowAsync(
                "Platform:Task Workflow",
                new[] { request },
                new WorkflowOptions
                {
                    Id = $"test:Platform:Task Workflow:{taskId}",
                    TaskQueue = "Platform:Task Workflow", // System-scoped, no tenant prefix
                    IdConflictPolicy = Temporalio.Api.Enums.V1.WorkflowIdConflictPolicy.UseExisting
                });

            // Wait for task to be ready
            await Task.Delay(2000);

            // Query the task using TaskCollection API
            var taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(
                client,
                TemporalTestUtils.DefaultTestTenantId,
                taskId);

            // Verify task info
            Assert.NotNull(taskInfo);
            Assert.Equal(taskId, taskInfo.TaskId);
            Assert.Equal("Test Query Task", taskInfo.Title);
            Assert.Equal("Testing task query functionality", taskInfo.Description);
            Assert.Equal("Initial draft for query test", taskInfo.CurrentDraft);
            Assert.Equal("test-reviewer", taskInfo.ParticipantId);
            Assert.False(taskInfo.IsCompleted);
            Assert.False(taskInfo.Success); // Should be false when not completed
            
            Console.WriteLine($"✓ Task queried successfully");
            Console.WriteLine($"  Task ID: {taskInfo.TaskId}");
            Console.WriteLine($"  Title: {taskInfo.Title}");
            Console.WriteLine($"  Draft: {taskInfo.CurrentDraft}");
            Console.WriteLine($"  Completed: {taskInfo.IsCompleted}");

            // Cleanup - complete the task
            await _platformAgent.Tasks.SignalCompleteTaskAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);
            await Task.Delay(500);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Test failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task UpdateDraftAsync_ShouldUpdateTaskDraft()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var taskId = $"task-update-{Guid.NewGuid():N}";
        _taskIds.Add(taskId);
        
        Console.WriteLine($"\n▶ Test: UpdateDraftAsync (taskId: {taskId})");

        try
        {
            var client = await _platformAgent!.TemporalService!.GetClientAsync();
            
            // Start a task workflow
            var request = new TaskWorkflowRequest
            {
                TaskId = taskId,
                Title = "Test Update Draft",
                Description = "Testing draft update functionality",
                ParticipantId = "test-reviewer",
                DraftWork = "Original draft content"
            };

            await client.StartWorkflowAsync(
                "Platform:Task Workflow",
                new[] { request },
                new WorkflowOptions
                {
                    Id = $"test:Platform:Task Workflow:{taskId}",
                    TaskQueue = "Platform:Task Workflow", // System-scoped, no tenant prefix
                    IdConflictPolicy = Temporalio.Api.Enums.V1.WorkflowIdConflictPolicy.UseExisting
                });

            await Task.Delay(2000);

            // Verify initial draft
            var taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            Assert.Equal("Original draft content", taskInfo.CurrentDraft);
            Console.WriteLine($"✓ Initial draft verified: {taskInfo.CurrentDraft}");

            // Update draft multiple times
            await _platformAgent.Tasks.SignalUpdateDraftAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId,
                "Draft version 1");

            await Task.Delay(500);

            taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            Assert.Equal("Draft version 1", taskInfo.CurrentDraft);
            Console.WriteLine($"✓ Draft updated to: {taskInfo.CurrentDraft}");

            // Update again
            await _platformAgent.Tasks.SignalUpdateDraftAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId,
                "Draft version 2 - final");

            await Task.Delay(500);

            taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            Assert.Equal("Draft version 2 - final", taskInfo.CurrentDraft);
            Console.WriteLine($"✓ Draft updated to final version: {taskInfo.CurrentDraft}");

            // Cleanup
            await _platformAgent.Tasks.SignalCompleteTaskAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);
            await Task.Delay(500);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Test failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task CompleteTaskAsync_ShouldCompleteTask()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var taskId = $"task-complete-{Guid.NewGuid():N}";
        _taskIds.Add(taskId);
        
        Console.WriteLine($"\n▶ Test: CompleteTaskAsync (taskId: {taskId})");

        try
        {
            var client = await _platformAgent!.TemporalService!.GetClientAsync();
            
            // Start a task workflow
            var request = new TaskWorkflowRequest
            {
                TaskId = taskId,
                Title = "Test Task Completion",
                Description = "Testing task completion functionality",
                ParticipantId = "test-reviewer",
                DraftWork = "Draft for completion test"
            };

            await client.StartWorkflowAsync(
                "Platform:Task Workflow",
                new[] { request },
                new WorkflowOptions
                {
                    Id = $"test:Platform:Task Workflow:{taskId}",
                    TaskQueue = "Platform:Task Workflow", // System-scoped, no tenant prefix
                    IdConflictPolicy = Temporalio.Api.Enums.V1.WorkflowIdConflictPolicy.UseExisting
                });

            await Task.Delay(2000);

            // Verify task is not completed
            var taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            Assert.False(taskInfo.IsCompleted);
            Assert.False(taskInfo.Success); // Should be false when not completed
            Console.WriteLine($"✓ Task verified as not completed");

            // Complete the task
            await _platformAgent.Tasks.SignalCompleteTaskAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            await Task.Delay(1000);

            // Verify task is completed
            taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            Assert.True(taskInfo.IsCompleted);
            Assert.True(taskInfo.Success);
            Assert.Null(taskInfo.RejectionReason);
            Console.WriteLine($"✓ Task completed successfully");
            Console.WriteLine($"  Completed: {taskInfo.IsCompleted}");
            Console.WriteLine($"  Success: {taskInfo.Success}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Test failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task RejectTaskAsync_ShouldRejectTaskWithReason()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var taskId = $"task-reject-{Guid.NewGuid():N}";
        _taskIds.Add(taskId);
        
        Console.WriteLine($"\n▶ Test: RejectTaskAsync (taskId: {taskId})");

        try
        {
            var client = await _platformAgent!.TemporalService!.GetClientAsync();
            
            // Start a task workflow
            var request = new TaskWorkflowRequest
            {
                TaskId = taskId,
                Title = "Test Task Rejection",
                Description = "Testing task rejection functionality",
                ParticipantId = "test-reviewer",
                DraftWork = "Draft for rejection test"
            };

            await client.StartWorkflowAsync(
                "Platform:Task Workflow",
                new[] { request },
                new WorkflowOptions
                {
                    Id = $"test:Platform:Task Workflow:{taskId}",
                    TaskQueue = "Platform:Task Workflow", // System-scoped, no tenant prefix
                    IdConflictPolicy = Temporalio.Api.Enums.V1.WorkflowIdConflictPolicy.UseExisting
                });

            await Task.Delay(2000);

            // Verify task is not completed
            var taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            Assert.False(taskInfo.IsCompleted);
            Console.WriteLine($"✓ Task verified as not completed");

            // Reject the task
            var rejectionReason = "Task rejected by automated test - invalid content";
            await _platformAgent.Tasks.SignalRejectTaskAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId, rejectionReason);

            await Task.Delay(1000);

            // Verify task is rejected
            taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            Assert.True(taskInfo.IsCompleted);
            Assert.False(taskInfo.Success);
            Assert.Equal(rejectionReason, taskInfo.RejectionReason);
            Console.WriteLine($"✓ Task rejected successfully");
            Console.WriteLine($"  Completed: {taskInfo.IsCompleted}");
            Console.WriteLine($"  Success: {taskInfo.Success}");
            Console.WriteLine($"  Rejection Reason: {taskInfo.RejectionReason}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Test failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task TaskLifecycle_FullWorkflow_ShouldHandleCompleteLifecycle()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var taskId = $"task-lifecycle-{Guid.NewGuid():N}";
        _taskIds.Add(taskId);
        
        Console.WriteLine($"\n▶ Test: TaskLifecycle_FullWorkflow (taskId: {taskId})");

        try
        {
            var client = await _platformAgent!.TemporalService!.GetClientAsync();

            // Step 1: Create task
            Console.WriteLine($"Step 1: Creating task...");
            var request = new TaskWorkflowRequest
            {
                TaskId = taskId,
                Title = "Full Lifecycle Test",
                Description = "Testing complete task lifecycle",
                ParticipantId = "test-reviewer",
                DraftWork = "Initial draft v0"
            };

            await client.StartWorkflowAsync(
                "Platform:Task Workflow",
                new[] { request },
                new WorkflowOptions
                {
                    Id = $"test:Platform:Task Workflow:{taskId}",
                    TaskQueue = "Platform:Task Workflow", // System-scoped, no tenant prefix
                    IdConflictPolicy = Temporalio.Api.Enums.V1.WorkflowIdConflictPolicy.UseExisting
                });

            await Task.Delay(2000);
            Console.WriteLine($"✓ Step 1: Task created");

            // Step 2: Query initial state
            Console.WriteLine($"Step 2: Querying initial state...");
            var taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            Assert.NotNull(taskInfo);
            Assert.Equal("Initial draft v0", taskInfo.CurrentDraft);
            Assert.False(taskInfo.IsCompleted);
            Console.WriteLine($"✓ Step 2: Initial state verified - Draft: {taskInfo.CurrentDraft}");

            // Step 3: Update draft multiple times
            Console.WriteLine($"Step 3: Updating draft...");
            await _platformAgent.Tasks.SignalUpdateDraftAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId,
                "Draft version 1");

            await Task.Delay(500);

            await _platformAgent.Tasks.SignalUpdateDraftAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId,
                "Draft version 2");

            await Task.Delay(500);

            await _platformAgent.Tasks.SignalUpdateDraftAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId,
                "Draft version 3 - final");

            await Task.Delay(500);

            taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            Assert.Equal("Draft version 3 - final", taskInfo.CurrentDraft);
            Console.WriteLine($"✓ Step 3: Draft updated 3 times - Final draft: {taskInfo.CurrentDraft}");

            // Step 4: Complete the task
            Console.WriteLine($"Step 4: Completing task...");
            await _platformAgent.Tasks.SignalCompleteTaskAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            await Task.Delay(1000);

            taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            Assert.True(taskInfo.IsCompleted);
            Assert.True(taskInfo.Success);
            Assert.Equal("Draft version 3 - final", taskInfo.CurrentDraft);
            
            Console.WriteLine($"✓ Step 4: Task completed successfully");
            Console.WriteLine($"✓ Full lifecycle test passed!");
            Console.WriteLine($"  Final State: Completed={taskInfo.IsCompleted}, Success={taskInfo.Success}");
            Console.WriteLine($"  Final Draft: {taskInfo.CurrentDraft}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Test failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task GetTaskHandleForClient_ShouldReturnValidHandle()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var taskId = $"task-handle-{Guid.NewGuid():N}";
        _taskIds.Add(taskId);
        
        Console.WriteLine($"\n▶ Test: GetTaskHandleForClient (taskId: {taskId})");

        try
        {
            var client = await _platformAgent!.TemporalService!.GetClientAsync();
            
            // Start a task workflow
            var request = new TaskWorkflowRequest
            {
                TaskId = taskId,
                Title = "Test Handle Management",
                Description = "Testing task handle functionality",
                ParticipantId = "test-reviewer",
                DraftWork = "Draft for handle test"
            };

            await client.StartWorkflowAsync(
                "Platform:Task Workflow",
                new[] { request },
                new WorkflowOptions
                {
                    Id = $"test:Platform:Task Workflow:{taskId}",
                    TaskQueue = "Platform:Task Workflow", // System-scoped, no tenant prefix
                    IdConflictPolicy = Temporalio.Api.Enums.V1.WorkflowIdConflictPolicy.UseExisting
                });

            await Task.Delay(2000);

            // Get handle directly - build workflow ID manually since we're outside workflow context
            var workflowId = $"{TemporalTestUtils.DefaultTestTenantId}:Platform:Task Workflow:{taskId}";
            var taskHandle = client.GetWorkflowHandle(workflowId);

            Assert.NotNull(taskHandle);
            Console.WriteLine($"✓ Task handle obtained successfully");
            Console.WriteLine($"  Workflow ID: {workflowId}");
            
            // Use the handle to query task info
            var taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            Assert.NotNull(taskInfo);
            Assert.Equal(taskId, taskInfo.TaskId);
            Assert.Equal("Test Handle Management", taskInfo.Title);
            Console.WriteLine($"✓ Task queried using handle");
            Console.WriteLine($"  Task ID: {taskInfo.TaskId}");
            Console.WriteLine($"  Title: {taskInfo.Title}");

            // Clean up
            await _platformAgent.Tasks.SignalCompleteTaskAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);
            await Task.Delay(500);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Test failed: {ex.Message}");
            throw;
        }
    }
}

