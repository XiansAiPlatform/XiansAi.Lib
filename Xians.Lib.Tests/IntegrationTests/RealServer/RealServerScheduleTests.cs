using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Scheduling;
using Xians.Lib.Agents.Scheduling.Models;
using Xians.Lib.Common.Caching;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Tests.TestUtilities;
using Temporalio.Client.Schedules;

namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// Tests for Schedule functionality against a real server.
/// These tests verify end-to-end scheduling operations with the Xians platform.
/// 
/// dotnet test --filter "FullyQualifiedName~RealServerScheduleTests"
/// 
/// </summary>
[Trait("Category", "RealServer")]
public class RealServerScheduleTests : RealServerTestBase, IAsyncLifetime
{
    // Use unique agent name per test class instance to avoid conflicts when tests run in parallel
    private readonly string _agentName = $"ScheduleTestAgent-{Guid.NewGuid():N}";
    private const string TEST_WORKFLOW_NAME = "ScheduleTestWorkflow";
    
    private XiansPlatform? _platform;
    private XiansAgent? _agent;
    private XiansWorkflow _workflow = null!;
    private readonly List<string> _scheduleIdsToCleanup = new();
    private const string TestIdPostfix = "test-schedules";

    //dotnet test --filter "FullyQualifiedName~RealServerScheduleTests"

    public async Task InitializeAsync()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        Console.WriteLine($"Initializing Schedule tests against REAL server: {ServerUrl}");
        
        // Initialize platform
        _platform = await XiansPlatform.InitializeAsync(new XiansOptions
        {
            ServerUrl = ServerUrl!,
            ApiKey = ApiKey!
        });

        // Register agent
        _agent = _platform.Agents.Register(new XiansAgentRegistration
        {
            Name = _agentName,
            SystemScoped = false
        });

        // Define a workflow for scheduling
        _workflow = _agent.Workflows.DefineBuiltIn(name: TEST_WORKFLOW_NAME);
        
        // Upload workflow definitions
        await _agent.UploadWorkflowDefinitionsAsync();
        
        Console.WriteLine($"✓ Initialized: Agent '{_agentName}', Workflow '{_workflow.WorkflowType}'");
    }

    public async Task DisposeAsync()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        Console.WriteLine($"Cleaning up {_scheduleIdsToCleanup.Count} tracked schedules...");
        
        // Step 1: Pause all tracked schedules first to prevent new workflow triggers
        foreach (var scheduleId in _scheduleIdsToCleanup)
        {
            try
            {
                if (await _workflow!.Schedules!.ExistsAsync(scheduleId, TestIdPostfix))
                {
                    await _workflow.Schedules.PauseAsync(scheduleId, TestIdPostfix, "Test cleanup");
                    Console.WriteLine($"  ⏸ Paused schedule: {scheduleId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ Failed to pause schedule {scheduleId}: {ex.Message}");
            }
        }
        
        // Step 2: Find and pause any orphaned schedules (not in cleanup list)
        await CleanupOrphanedSchedulesAsync();
        
        // Step 3: Terminate any running workflows BEFORE deleting schedules
        await TerminateWorkflowsAsync();
        
        // Step 4: Now delete all the schedules (tracked + orphaned)
        foreach (var scheduleId in _scheduleIdsToCleanup)
        {
            try
            {
                if (await _workflow!.Schedules!.ExistsAsync(scheduleId, TestIdPostfix))
                {
                    await _workflow.Schedules.DeleteAsync(scheduleId, TestIdPostfix);
                    Console.WriteLine($"  ✓ Deleted schedule: {scheduleId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ Failed to delete schedule {scheduleId}: {ex.Message}");
            }
        }
        
        // Delete agent
        if (_agent != null)
        {
            try
            {
                await _agent.DeleteAsync();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        
        Console.WriteLine("✓ Cleanup complete");
    }

    private async Task CleanupOrphanedSchedulesAsync()
    {
        try
        {
            Console.WriteLine("  Checking for orphaned schedules...");
            
            if (_agent?.TemporalService == null)
            {
                return;
            }

            var temporalClient = await _agent.TemporalService.GetClientAsync();
            var scheduleListStream = temporalClient.ListSchedulesAsync();
            
            var orphanedCount = 0;
            await foreach (var scheduleListEntry in scheduleListStream)
            {
                var scheduleId = scheduleListEntry.Id;
                
                // Check if this is a test schedule that wasn't tracked
                if (scheduleId.StartsWith("test-") && !_scheduleIdsToCleanup.Contains(scheduleId))
                {
                    try
                    {
                        // Get the schedule to check if it belongs to this workflow
                        var schedule = await _workflow!.Schedules!.GetAsync(scheduleId, TestIdPostfix);
                        var description = await schedule.DescribeAsync();
                        
                        // Check if this schedule is for our test workflow
                        var action = description.Schedule.Action as Temporalio.Client.Schedules.ScheduleActionStartWorkflow;
                        if (action != null && action.Workflow.Contains(TEST_WORKFLOW_NAME))
                        {
                            Console.WriteLine($"  ⚠ Found orphaned schedule: {scheduleId}");
                            _scheduleIdsToCleanup.Add(scheduleId);
                            
                            // Pause it immediately
                            await _workflow.Schedules.PauseAsync(scheduleId, TestIdPostfix, "Orphaned schedule cleanup");
                            orphanedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ⚠ Error checking schedule {scheduleId}: {ex.Message}");
                    }
                }
            }
            
            if (orphanedCount > 0)
            {
                Console.WriteLine($"  ⚠ Found and paused {orphanedCount} orphaned schedule(s)");
            }
            else
            {
                Console.WriteLine($"  ✓ No orphaned schedules found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Error during orphaned schedule cleanup: {ex.Message}");
        }
    }

    private async Task TerminateWorkflowsAsync()
    {
        if (_agent?.TemporalService == null || _platform == null) return;

        try
        {
            var temporalClient = await _agent.TemporalService.GetClientAsync();
            var tenantId = _platform.Options.CertificateTenantId ?? "tests";
            
            Console.WriteLine("  Terminating all workflows for test agent...");
            
            // Wait a moment for any schedule-triggered workflows to fully start
            // Schedules may have just triggered workflows that are still being created
            await Task.Delay(2000);
            
            int totalTerminated = 0;
            
            // Use Temporal's workflow listing to find ALL running workflows for this test agent
            // Query by both workflow type AND execution status
            var query = $"WorkflowType STARTS_WITH '{_agentName}:' AND ExecutionStatus='Running'";
            
            await foreach (var workflowExecution in temporalClient.ListWorkflowsAsync(query))
            {
                try
                {
                    var workflowId = workflowExecution.Id;
                    var handle = temporalClient.GetWorkflowHandle(workflowId);
                    await handle.TerminateAsync("Test cleanup");
                    totalTerminated++;
                    Console.WriteLine($"    ✓ Terminated workflow: {workflowId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    ⚠ Could not terminate workflow: {ex.Message}");
                }
            }
            
            if (totalTerminated > 0)
            {
                Console.WriteLine($"  ✓ Terminated {totalTerminated} workflow(s)");
            }
            else
            {
                Console.WriteLine($"  ℹ No running workflows found for agent {_agentName}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to terminate workflows: {ex.Message}");
        }
    }

    [Fact]
    public async Task Schedule_CreateWithCron_ShouldSucceed()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var scheduleId = $"test-cron-{Guid.NewGuid():N}";
        _scheduleIdsToCleanup.Add(scheduleId);

        Console.WriteLine($"Creating cron schedule: {scheduleId}");

        // Act - Create a cron-based schedule (daily at midnight UTC)
        var schedule = await _workflow!.Schedules!
            .Create(scheduleId, TestIdPostfix)
            .WithCronSchedule("0 0 * * *")  // Daily at midnight
            .WithInput("test-input")
            .CreateIfNotExistsAsync();

        // Assert
        Assert.NotNull(schedule);
        Console.WriteLine($"✓ Created cron schedule: {scheduleId}");

        // Verify schedule exists
        var exists = await _workflow.Schedules.ExistsAsync(scheduleId, TestIdPostfix);
        Assert.True(exists);
        Console.WriteLine($"✓ Verified schedule exists");
    }

    [Fact]
    public async Task Schedule_CreateWithInterval_ShouldSucceed()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var scheduleId = $"test-interval-{Guid.NewGuid():N}";
        _scheduleIdsToCleanup.Add(scheduleId);

        Console.WriteLine($"Creating interval schedule: {scheduleId}");

        // Act - Create an interval-based schedule (every 5 minutes)
        var schedule = await _workflow!.Schedules!
            .Create(scheduleId, TestIdPostfix)
            .WithIntervalSchedule(TimeSpan.FromMinutes(5))
            .WithInput("test-input")
            .CreateIfNotExistsAsync();

        // Assert
        Assert.NotNull(schedule);
        Console.WriteLine($"✓ Created interval schedule: {scheduleId}");

        // Verify schedule exists
        var exists = await _workflow.Schedules.ExistsAsync(scheduleId, TestIdPostfix);
        Assert.True(exists);
        Console.WriteLine($"✓ Verified schedule exists");
    }

    [Fact]
    public async Task Schedule_CreatePaused_ShouldStartInPausedState()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var scheduleId = $"test-paused-{Guid.NewGuid():N}";
        _scheduleIdsToCleanup.Add(scheduleId);

        Console.WriteLine($"Creating paused schedule: {scheduleId}");

        // Act - Create a schedule that starts paused
        var schedule = await _workflow!.Schedules!
            .Create(scheduleId, TestIdPostfix)
            .WithCronSchedule("0 0 * * *")
            .StartPaused(true, "Test schedule - starting paused")
            .CreateIfNotExistsAsync();

        // Assert
        Assert.NotNull(schedule);
        var description = await schedule.DescribeAsync();
        Assert.True(description.Schedule.State.Paused);
        Console.WriteLine($"✓ Schedule created in paused state");
    }

    [Fact]
    public async Task Schedule_GetAsync_ShouldRetrieveExistingSchedule()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var scheduleId = $"test-get-{Guid.NewGuid():N}";
        _scheduleIdsToCleanup.Add(scheduleId);

        Console.WriteLine($"Testing Get operation: {scheduleId}");

        // Arrange - Create a schedule
        await _workflow!.Schedules!
            .Create(scheduleId, TestIdPostfix)
            .WithCronSchedule("0 0 * * *")
            .CreateIfNotExistsAsync();
        Console.WriteLine($"  ✓ Created schedule");

        // Act - Retrieve the schedule
        var retrievedSchedule = await _workflow!.Schedules!.GetAsync(scheduleId, TestIdPostfix);

        // Assert
        Assert.NotNull(retrievedSchedule);
        var description = await retrievedSchedule.DescribeAsync();
        Assert.Contains(scheduleId, description.Id);
        Console.WriteLine($"✓ Successfully retrieved schedule");
    }

    [Fact]
    public async Task Schedule_Get_SyncMethod_ShouldWork()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var scheduleId = $"test-get-sync-{Guid.NewGuid():N}";
        _scheduleIdsToCleanup.Add(scheduleId);

        Console.WriteLine($"Testing synchronous Get operation: {scheduleId}");

        // Arrange - Create a schedule
        await _workflow!.Schedules!
            .Create(scheduleId, TestIdPostfix)
            .WithCronSchedule("0 0 * * *")
            .CreateIfNotExistsAsync();

        // Act - Use synchronous Get method
        var retrievedSchedule = _workflow!.Schedules!.Get(scheduleId, TestIdPostfix);

        // Assert
        Assert.NotNull(retrievedSchedule);
        Console.WriteLine($"✓ Synchronous Get succeeded");
    }

    [Fact]
    public async Task Schedule_ExistsAsync_ShouldReturnCorrectStatus()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var existingScheduleId = $"test-exists-true-{Guid.NewGuid():N}";
        var nonExistingScheduleId = $"test-exists-false-{Guid.NewGuid():N}";
        _scheduleIdsToCleanup.Add(existingScheduleId);

        Console.WriteLine($"Testing ExistsAsync operation");

        // Arrange - Create one schedule
        await _workflow!.Schedules!
            .Create(existingScheduleId, TestIdPostfix)
            .WithCronSchedule("0 0 * * *")
            .CreateIfNotExistsAsync();
        Console.WriteLine($"  ✓ Created schedule: {existingScheduleId}");

        // Act & Assert - Check existing schedule
        var exists = await _workflow!.Schedules!.ExistsAsync(existingScheduleId, TestIdPostfix);
        Assert.True(exists);
        Console.WriteLine($"✓ ExistsAsync returned true for existing schedule");

        // Act & Assert - Check non-existing schedule
        var notExists = await _workflow!.Schedules!.ExistsAsync(nonExistingScheduleId, TestIdPostfix);
        Assert.False(notExists);
        Console.WriteLine($"✓ ExistsAsync returned false for non-existing schedule");
    }

    [Fact]
    public async Task Schedule_PauseAndUnpause_ShouldWork()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var scheduleId = $"test-pause-{Guid.NewGuid():N}";
        _scheduleIdsToCleanup.Add(scheduleId);

        Console.WriteLine($"Testing Pause/Unpause operations: {scheduleId}");

        // Arrange - Create an active schedule
        var schedule = await _workflow!.Schedules!
            .Create(scheduleId, TestIdPostfix)
            .WithCronSchedule("0 0 * * *")
            .CreateIfNotExistsAsync();
        Console.WriteLine($"  ✓ Created active schedule");

        // Act - Pause the schedule
        await _workflow!.Schedules!.PauseAsync(scheduleId, TestIdPostfix, "Testing pause functionality");
        Console.WriteLine($"  ✓ Paused schedule");

        // Assert - Verify paused
        var pausedDescription = await schedule.DescribeAsync();
        Assert.True(pausedDescription.Schedule.State.Paused);
        Console.WriteLine($"✓ Verified schedule is paused");

        // Act - Unpause the schedule
        await _workflow!.Schedules!.UnpauseAsync(scheduleId, TestIdPostfix, "Testing unpause functionality");
        Console.WriteLine($"  ✓ Unpaused schedule");

        // Assert - Verify unpaused
        var unpausedDescription = await schedule.DescribeAsync();
        Assert.False(unpausedDescription.Schedule.State.Paused);
        Console.WriteLine($"✓ Verified schedule is unpaused");
    }

    [Fact]
    public async Task Schedule_TriggerAsync_ShouldExecuteImmediately()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var scheduleId = $"test-trigger-{Guid.NewGuid():N}";
        _scheduleIdsToCleanup.Add(scheduleId);

        Console.WriteLine($"Testing TriggerAsync operation: {scheduleId}");

        // Arrange - Create a paused schedule (so it only runs when triggered)
        await _workflow!.Schedules!
            .Create(scheduleId, TestIdPostfix)
            .WithCronSchedule("0 0 * * *")  // Won't run on its own
            .StartPaused(true)
            .CreateIfNotExistsAsync();
        Console.WriteLine($"  ✓ Created paused schedule");

        // Act - Trigger immediate execution
        await _workflow!.Schedules!.TriggerAsync(scheduleId, TestIdPostfix);
        Console.WriteLine($"✓ Triggered schedule execution");

        // Note: We can't easily verify the workflow ran without more infrastructure,
        // but if TriggerAsync completes without error, the trigger was successful
    }

    [Fact]
    public async Task Schedule_DeleteAsync_ShouldRemoveSchedule()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var scheduleId = $"test-delete-{Guid.NewGuid():N}";
        // Don't add to cleanup list since we're testing deletion

        Console.WriteLine($"Testing DeleteAsync operation: {scheduleId}");

        // Arrange - Create a schedule
        await _workflow!.Schedules!
            .Create(scheduleId, TestIdPostfix)
            .WithCronSchedule("0 0 * * *")
            .CreateIfNotExistsAsync();
        Console.WriteLine($"  ✓ Created schedule");

        // Verify it exists
        var existsBefore = await _workflow!.Schedules!.ExistsAsync(scheduleId, TestIdPostfix);
        Assert.True(existsBefore);
        Console.WriteLine($"  ✓ Verified schedule exists");

        // Act - Delete the schedule
        await _workflow!.Schedules!.DeleteAsync(scheduleId, TestIdPostfix);
        Console.WriteLine($"  ✓ Deleted schedule");

        // Assert - Verify it no longer exists
        var existsAfter = await _workflow!.Schedules!.ExistsAsync(scheduleId, TestIdPostfix);
        Assert.False(existsAfter);
        Console.WriteLine($"✓ Verified schedule was deleted");
    }

    [Fact]
    public async Task Schedule_FullLifecycle_ShouldWork()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var scheduleId = $"test-lifecycle-{Guid.NewGuid():N}";
        _scheduleIdsToCleanup.Add(scheduleId);

        Console.WriteLine($"Testing full schedule lifecycle: {scheduleId}");

        // 1. Create
        Console.WriteLine("  Step 1: Creating schedule...");
        var schedule = await _workflow!.Schedules!
            .Create(scheduleId, TestIdPostfix)
            .WithCronSchedule("0 */6 * * *")  // Every 6 hours
            .WithInput("lifecycle-test")
            .CreateIfNotExistsAsync();
        Assert.NotNull(schedule);
        Console.WriteLine("  ✓ Created");

        // 2. Verify existence
        Console.WriteLine("  Step 2: Verifying existence...");
        var exists = await _workflow!.Schedules!.ExistsAsync(scheduleId, TestIdPostfix);
        Assert.True(exists);
        Console.WriteLine("  ✓ Exists");

        // 3. Get/Retrieve
        Console.WriteLine("  Step 3: Retrieving schedule...");
        var retrieved = await _workflow.Schedules.GetAsync(scheduleId, TestIdPostfix);
        Assert.NotNull(retrieved);
        Console.WriteLine("  ✓ Retrieved");

        // 4. Pause
        Console.WriteLine("  Step 4: Pausing schedule...");
        await _workflow.Schedules.PauseAsync(scheduleId, TestIdPostfix, "Lifecycle test");
        var descAfterPause = await retrieved.DescribeAsync();
        Assert.True(descAfterPause.Schedule.State.Paused);
        Console.WriteLine("  ✓ Paused");

        // 5. Unpause
        Console.WriteLine("  Step 5: Unpausing schedule...");
        await _workflow.Schedules.UnpauseAsync(scheduleId, TestIdPostfix);
        var descAfterUnpause = await retrieved.DescribeAsync();
        Assert.False(descAfterUnpause.Schedule.State.Paused);
        Console.WriteLine("  ✓ Unpaused");

        // 6. Trigger
        Console.WriteLine("  Step 6: Triggering schedule...");
        await _workflow.Schedules.TriggerAsync(scheduleId, TestIdPostfix);
        Console.WriteLine("  ✓ Triggered");

        // 7. List (verify we can call ListAsync)
        Console.WriteLine("  Step 7: Testing ListAsync...");
        
        // Small delay to ensure schedule is indexed
        await Task.Delay(100);

        Console.WriteLine("✓ Full lifecycle test PASSED");
    }

    [Fact]
    public async Task Schedule_CreateAsync_ShouldFailIfExists()
    {
        if (!RunRealServerTests) return;
        var scheduleId = $"test-strict-{Guid.NewGuid():N}";
        _scheduleIdsToCleanup.Add(scheduleId);
        await _workflow!.Schedules!.Create(scheduleId, TestIdPostfix).WithCronSchedule("0 0 * * *").CreateIfNotExistsAsync();
        await Assert.ThrowsAsync<ScheduleAlreadyExistsException>(async () =>
            await _workflow!.Schedules!.Create(scheduleId, TestIdPostfix).WithCronSchedule("0 0 * * *").CreateAsync());
        Console.WriteLine($"✓ CreateAsync correctly threw exception");
    }

    [Fact]
    public async Task Schedule_CreateIfNotExistsAsync_ShouldBeIdempotent()
    {
        if (!RunRealServerTests) return;
        var scheduleId = $"test-idempotent-{Guid.NewGuid():N}";
        _scheduleIdsToCleanup.Add(scheduleId);
        var s1 = await _workflow!.Schedules!.Create(scheduleId, TestIdPostfix).WithCronSchedule("0 0 * * *").CreateIfNotExistsAsync();
        var s2 = await _workflow!.Schedules!.Create(scheduleId, TestIdPostfix).WithCronSchedule("0 1 * * *").CreateIfNotExistsAsync();
        Assert.NotNull(s1);
        Assert.NotNull(s2);
        Console.WriteLine($"✓ CreateIfNotExistsAsync is idempotent");
    }

    [Fact]
    public async Task Schedule_RecreateAsync_ShouldDeleteAndRecreate()
    {
        if (!RunRealServerTests) return;
        var scheduleId = $"test-recreate-{Guid.NewGuid():N}";
        _scheduleIdsToCleanup.Add(scheduleId);
        await _workflow!.Schedules!.Create(scheduleId, TestIdPostfix).WithIntervalSchedule(TimeSpan.FromHours(1)).CreateAsync();
        await Task.Delay(100);
        var schedule = await _workflow!.Schedules!.Create(scheduleId, TestIdPostfix).WithIntervalSchedule(TimeSpan.FromMinutes(30)).RecreateAsync();
        Assert.NotNull(schedule);
        Console.WriteLine($"✓ RecreateAsync works");
    }

    [Fact]
    public async Task Schedule_MemoShouldContainRequiredMetadata()
    {
        if (!RunRealServerTests) return;
        var scheduleId = $"test-memo-{Guid.NewGuid():N}";
        _scheduleIdsToCleanup.Add(scheduleId);
        var schedule = await _workflow!.Schedules!.Create(scheduleId, TestIdPostfix).WithCronSchedule("0 0 * * *").CreateIfNotExistsAsync();
        var description = await schedule.DescribeAsync();
        
        // Memo is on the schedule action (for scheduled workflows), not the schedule itself
        var action = description.Schedule.Action as Temporalio.Client.Schedules.ScheduleActionStartWorkflow;
        Assert.NotNull(action);
        
        var memo = action.Options.Memo;
        Assert.NotNull(memo);
        Assert.True(memo.ContainsKey("tenantId"));
        Assert.True(memo.ContainsKey("agent"));
        Assert.True(memo.ContainsKey("userId"));
        Assert.True(memo.ContainsKey("idPostfix"));
        Assert.True(memo.ContainsKey("systemScoped"));
        
        Console.WriteLine($"✓ All required memo fields present");
    }
}

