using System.Collections.Concurrent;
using Temporalio.Client;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Agents.Tasks.Models;
using Xians.Lib.Tests.TestUtilities;
using Xians.Lib.Temporal.Workflows.Messaging.Models;

namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// Real server integration tests for TaskCollection and task lifecycle management.
/// 
/// These tests verify TaskCollection functionality:
/// - ✅ Task creation via client API
/// - ✅ UpdateDraftAsync (from client)
/// - ✅ PerformActionAsync (approve/reject with comment)
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
        var options = CreateTestOptions();

        _platform = await XiansPlatform.InitializeAsync(options);
        
        // Register Platform agent for task workflows
        _platformAgent = _platform.Agents.Register(new XiansAgentRegistration 
        { 
            Name = "Platform",
            IsTemplate = true
        });

        // Enable task workflow support
        await _platformAgent.Workflows.WithTasks();

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

            // Delete agent
            if (_platformAgent != null)
            {
                try
                {
                    await _platformAgent.DeleteAsync();
                }
                catch
                {
                    // Ignore cleanup errors
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
                Title = "Test Query Task",
                Description = "Testing task query functionality",
                ParticipantId = "test-reviewer",
                DraftWork = "Initial draft for query test",
                Actions = ["approve", "reject", "hold"]
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
            Assert.Equal("Test Query Task", taskInfo.Title);
            Assert.Equal("Testing task query functionality", taskInfo.Description);
            Assert.Equal("Initial draft for query test", taskInfo.FinalWork);
            Assert.Equal("test-reviewer", taskInfo.ParticipantId);
            Assert.False(taskInfo.IsCompleted);
            Assert.Null(taskInfo.PerformedAction);
            Assert.NotNull(taskInfo.AvailableActions);
            Assert.Contains("approve", taskInfo.AvailableActions);
            Assert.Contains("reject", taskInfo.AvailableActions);
            Assert.Contains("hold", taskInfo.AvailableActions);
            
            Console.WriteLine($"✓ Task queried successfully"); 
            Console.WriteLine($"  Title: {taskInfo.Title}");
            Console.WriteLine($"  Draft: {taskInfo.FinalWork}");
            Console.WriteLine($"  Available Actions: {string.Join(", ", taskInfo.AvailableActions)}");
            Console.WriteLine($"  Completed: {taskInfo.IsCompleted}");

            // Cleanup - complete the task
            await _platformAgent.Tasks.SignalPerformActionAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId, "approve");
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

            Assert.Equal("Original draft content", taskInfo.FinalWork);
            Console.WriteLine($"✓ Initial draft verified: {taskInfo.FinalWork}");

            // Update draft multiple times
            await _platformAgent.Tasks.SignalUpdateDraftAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId,
                "Draft version 1");

            await Task.Delay(500);

            taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            Assert.Equal("Draft version 1", taskInfo.FinalWork);
            Console.WriteLine($"✓ Draft updated to: {taskInfo.FinalWork}");

            // Update again
            await _platformAgent.Tasks.SignalUpdateDraftAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId,
                "Draft version 2 - final");

            await Task.Delay(500);

            taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            Assert.Equal("Draft version 2 - final", taskInfo.FinalWork);
            Console.WriteLine($"✓ Draft updated to final version: {taskInfo.FinalWork}");

            // Cleanup
            await _platformAgent.Tasks.SignalPerformActionAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId, "approve");
            await Task.Delay(500);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Test failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task PerformActionAsync_Approve_ShouldCompleteTask()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var taskId = $"task-approve-{Guid.NewGuid():N}";
        _taskIds.Add(taskId);
        
        Console.WriteLine($"\n▶ Test: PerformActionAsync - Approve (taskId: {taskId})");

        try
        {
            var client = await _platformAgent!.TemporalService!.GetClientAsync();
            
            // Start a task workflow
            var request = new TaskWorkflowRequest
            {
                Title = "Test Task Approval",
                Description = "Testing task approval functionality",
                ParticipantId = "test-reviewer",
                DraftWork = "Draft for approval test"
            };

            await client.StartWorkflowAsync(
                "Platform:Task Workflow",
                new[] { request },
                new WorkflowOptions
                {
                    Id = $"test:Platform:Task Workflow:{taskId}",
                    TaskQueue = "Platform:Task Workflow",
                    IdConflictPolicy = Temporalio.Api.Enums.V1.WorkflowIdConflictPolicy.UseExisting
                });

            await Task.Delay(2000);

            // Verify task is not completed
            var taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            Assert.False(taskInfo.IsCompleted);
            Assert.Null(taskInfo.PerformedAction);
            Console.WriteLine($"✓ Task verified as not completed");

            // Approve the task with a comment
            await _platformAgent.Tasks.SignalPerformActionAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId, 
                "approve", "Looks good to me!");

            await Task.Delay(1000);

            // Verify task is completed
            taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            Assert.True(taskInfo.IsCompleted);
            Assert.Equal("approve", taskInfo.PerformedAction);
            Assert.Equal("Looks good to me!", taskInfo.Comment);
            Console.WriteLine($"✓ Task approved successfully");
            Console.WriteLine($"  Completed: {taskInfo.IsCompleted}");
            Console.WriteLine($"  Action: {taskInfo.PerformedAction}");
            Console.WriteLine($"  Comment: {taskInfo.Comment}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Test failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task PerformActionAsync_Reject_ShouldCompleteTaskWithReason()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var taskId = $"task-reject-{Guid.NewGuid():N}";
        _taskIds.Add(taskId);
        
        Console.WriteLine($"\n▶ Test: PerformActionAsync - Reject (taskId: {taskId})");

        try
        {
            var client = await _platformAgent!.TemporalService!.GetClientAsync();
            
            // Start a task workflow
            var request = new TaskWorkflowRequest
            {
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
                    TaskQueue = "Platform:Task Workflow",
                    IdConflictPolicy = Temporalio.Api.Enums.V1.WorkflowIdConflictPolicy.UseExisting
                });

            await Task.Delay(2000);

            // Verify task is not completed
            var taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            Assert.False(taskInfo.IsCompleted);
            Console.WriteLine($"✓ Task verified as not completed");

            // Reject the task
            var rejectionReason = "Task rejected by automated test - invalid content";
            await _platformAgent.Tasks.SignalPerformActionAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId, 
                "reject", rejectionReason);

            await Task.Delay(1000);

            // Verify task is rejected
            taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            Assert.True(taskInfo.IsCompleted);
            Assert.Equal("reject", taskInfo.PerformedAction);
            Assert.Equal(rejectionReason, taskInfo.Comment);
            Console.WriteLine($"✓ Task rejected successfully");
            Console.WriteLine($"  Completed: {taskInfo.IsCompleted}");
            Console.WriteLine($"  Action: {taskInfo.PerformedAction}");
            Console.WriteLine($"  Comment: {taskInfo.Comment}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Test failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task PerformActionAsync_CustomAction_ShouldCompleteWithCustomAction()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var taskId = $"task-custom-{Guid.NewGuid():N}";
        _taskIds.Add(taskId);
        
        Console.WriteLine($"\n▶ Test: PerformActionAsync - Custom Action (taskId: {taskId})");

        try
        {
            var client = await _platformAgent!.TemporalService!.GetClientAsync();
            
            // Start a task workflow with custom actions
            var request = new TaskWorkflowRequest
            {
                Title = "Test Custom Actions",
                Description = "Testing custom action functionality",
                ParticipantId = "test-reviewer",
                DraftWork = "Draft for custom action test",
                Actions = ["ship", "hold", "cancel", "refund"]
            };

            await client.StartWorkflowAsync(
                "Platform:Task Workflow",
                new[] { request },
                new WorkflowOptions
                {
                    Id = $"test:Platform:Task Workflow:{taskId}",
                    TaskQueue = "Platform:Task Workflow",
                    IdConflictPolicy = Temporalio.Api.Enums.V1.WorkflowIdConflictPolicy.UseExisting
                });

            await Task.Delay(2000);

            // Verify available actions
            var taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            Assert.NotNull(taskInfo.AvailableActions);
            Assert.Contains("ship", taskInfo.AvailableActions);
            Assert.Contains("hold", taskInfo.AvailableActions);
            Console.WriteLine($"✓ Custom actions verified: {string.Join(", ", taskInfo.AvailableActions)}");

            // Perform custom action
            await _platformAgent.Tasks.SignalPerformActionAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId, 
                "hold", "Waiting for inventory restock");

            await Task.Delay(1000);

            // Verify task completed with custom action
            taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            Assert.True(taskInfo.IsCompleted);
            Assert.Equal("hold", taskInfo.PerformedAction);
            Assert.Equal("Waiting for inventory restock", taskInfo.Comment);
            Console.WriteLine($"✓ Custom action performed successfully");
            Console.WriteLine($"  Action: {taskInfo.PerformedAction}");
            Console.WriteLine($"  Comment: {taskInfo.Comment}");
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
                Title = "Full Lifecycle Test",
                Description = "Testing complete task lifecycle",
                ParticipantId = "test-reviewer",
                DraftWork = "Initial draft v0",
                Actions = ["approve", "reject", "request-changes"]
            };

            await client.StartWorkflowAsync(
                "Platform:Task Workflow",
                new[] { request },
                new WorkflowOptions
                {
                    Id = $"test:Platform:Task Workflow:{taskId}",
                    TaskQueue = "Platform:Task Workflow",
                    IdConflictPolicy = Temporalio.Api.Enums.V1.WorkflowIdConflictPolicy.UseExisting
                });

            await Task.Delay(2000);
            Console.WriteLine($"✓ Step 1: Task created");

            // Step 2: Query initial state
            Console.WriteLine($"Step 2: Querying initial state...");
            var taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            Assert.NotNull(taskInfo);
            Assert.Equal("Initial draft v0", taskInfo.FinalWork);
            Assert.False(taskInfo.IsCompleted);
            Assert.NotNull(taskInfo.AvailableActions);
            Console.WriteLine($"✓ Step 2: Initial state verified - Draft: {taskInfo.FinalWork}");
            Console.WriteLine($"  Available Actions: {string.Join(", ", taskInfo.AvailableActions)}");

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

            Assert.Equal("Draft version 3 - final", taskInfo.FinalWork);
            Console.WriteLine($"✓ Step 3: Draft updated 3 times - Final draft: {taskInfo.FinalWork}");

            // Step 4: Complete the task with approve action
            Console.WriteLine($"Step 4: Approving task...");
            await _platformAgent.Tasks.SignalPerformActionAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId, 
                "approve", "All revisions look great!");

            await Task.Delay(1000);

            taskInfo = await _platformAgent.Tasks.QueryTaskInfoAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId);

            Assert.True(taskInfo.IsCompleted);
            Assert.Equal("approve", taskInfo.PerformedAction);
            Assert.Equal("All revisions look great!", taskInfo.Comment);
            Assert.Equal("Draft version 3 - final", taskInfo.FinalWork);
            
            Console.WriteLine($"✓ Step 4: Task approved successfully");
            Console.WriteLine($"✓ Full lifecycle test passed!");
            Console.WriteLine($"  Final State: Completed={taskInfo.IsCompleted}, Action={taskInfo.PerformedAction}");
            Console.WriteLine($"  Final Draft: {taskInfo.FinalWork}");
            Console.WriteLine($"  Comment: {taskInfo.Comment}");
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
                    TaskQueue = "Platform:Task Workflow",
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
            Assert.Equal("Test Handle Management", taskInfo.Title);
            Console.WriteLine($"✓ Task queried using handle");
            Console.WriteLine($"  Title: {taskInfo.Title}");

            // Clean up
            await _platformAgent.Tasks.SignalPerformActionAsync(client, TemporalTestUtils.DefaultTestTenantId, taskId, "approve");
            await Task.Delay(500);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Test failed: {ex.Message}");
            throw;
        }
    }
}

/// <summary>
/// Collection definition to disable parallelization for Task tests.
/// </summary>
[CollectionDefinition("RealServerTask", DisableParallelization = true)]
public class RealServerTaskCollection
{
}
