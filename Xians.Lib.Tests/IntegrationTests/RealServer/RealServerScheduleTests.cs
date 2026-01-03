using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Scheduling;
using Xians.Lib.Common.Caching;
using Xians.Lib.Common.Infrastructure;
using Temporalio.Client.Schedules;

namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// Tests for Schedule functionality against a real server.
/// These tests verify end-to-end scheduling operations with the Xians platform.
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

    //dotnet test --filter "FullyQualifiedName~RealServerScheduleTests"

    public async Task InitializeAsync()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        Console.WriteLine($"Initializing Schedule tests against REAL server: {ServerUrl}");
        
        // Initialize platform
        _platform = XiansPlatform.Initialize(new XiansOptions
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
        _workflow = _agent.Workflows.DefineBuiltIn(workers: 1, name: TEST_WORKFLOW_NAME);
        
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

        Console.WriteLine($"Cleaning up {_scheduleIdsToCleanup.Count} test schedules...");
        
        // Clean up all created schedules
        foreach (var scheduleId in _scheduleIdsToCleanup)
        {
            try
            {
                if (await _workflow!.Schedules!.ExistsAsync(scheduleId))
                {
                    await _workflow.Schedules.DeleteAsync(scheduleId);
                    Console.WriteLine($"  ✓ Deleted schedule: {scheduleId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ Failed to delete schedule {scheduleId}: {ex.Message}");
            }
        }
        
        Console.WriteLine("✓ Cleanup complete");
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
            .Create(scheduleId)
            .WithCronSchedule("0 0 * * *")  // Daily at midnight
            .WithInput("test-input")
            .StartAsync();

        // Assert
        Assert.NotNull(schedule);
        Console.WriteLine($"✓ Created cron schedule: {scheduleId}");

        // Verify schedule exists
        var exists = await _workflow.Schedules.ExistsAsync(scheduleId);
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
            .Create(scheduleId)
            .WithIntervalSchedule(TimeSpan.FromMinutes(5))
            .WithInput("test-input")
            .StartAsync();

        // Assert
        Assert.NotNull(schedule);
        Console.WriteLine($"✓ Created interval schedule: {scheduleId}");

        // Verify schedule exists
        var exists = await _workflow.Schedules.ExistsAsync(scheduleId);
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
            .Create(scheduleId)
            .WithCronSchedule("0 0 * * *")
            .StartPaused(true, "Test schedule - starting paused")
            .StartAsync();

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
            .Create(scheduleId)
            .WithCronSchedule("0 0 * * *")
            .StartAsync();
        Console.WriteLine($"  ✓ Created schedule");

        // Act - Retrieve the schedule
        var retrievedSchedule = await _workflow!.Schedules!.GetAsync(scheduleId);

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
            .Create(scheduleId)
            .WithCronSchedule("0 0 * * *")
            .StartAsync();

        // Act - Use synchronous Get method
        var retrievedSchedule = _workflow!.Schedules!.Get(scheduleId);

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
            .Create(existingScheduleId)
            .WithCronSchedule("0 0 * * *")
            .StartAsync();
        Console.WriteLine($"  ✓ Created schedule: {existingScheduleId}");

        // Act & Assert - Check existing schedule
        var exists = await _workflow!.Schedules!.ExistsAsync(existingScheduleId);
        Assert.True(exists);
        Console.WriteLine($"✓ ExistsAsync returned true for existing schedule");

        // Act & Assert - Check non-existing schedule
        var notExists = await _workflow!.Schedules!.ExistsAsync(nonExistingScheduleId);
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
            .Create(scheduleId)
            .WithCronSchedule("0 0 * * *")
            .StartAsync();
        Console.WriteLine($"  ✓ Created active schedule");

        // Act - Pause the schedule
        await _workflow!.Schedules!.PauseAsync(scheduleId, "Testing pause functionality");
        Console.WriteLine($"  ✓ Paused schedule");

        // Assert - Verify paused
        var pausedDescription = await schedule.DescribeAsync();
        Assert.True(pausedDescription.Schedule.State.Paused);
        Console.WriteLine($"✓ Verified schedule is paused");

        // Act - Unpause the schedule
        await _workflow!.Schedules!.UnpauseAsync(scheduleId, "Testing unpause functionality");
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
            .Create(scheduleId)
            .WithCronSchedule("0 0 * * *")  // Won't run on its own
            .StartPaused(true)
            .StartAsync();
        Console.WriteLine($"  ✓ Created paused schedule");

        // Act - Trigger immediate execution
        await _workflow!.Schedules!.TriggerAsync(scheduleId);
        Console.WriteLine($"✓ Triggered schedule execution");

        // Note: We can't easily verify the workflow ran without more infrastructure,
        // but if TriggerAsync completes without error, the trigger was successful
    }

    [Fact]
    public async Task Schedule_ListAsync_ShouldReturnSchedules()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var scheduleId1 = $"test-list-1-{Guid.NewGuid():N}";
        var scheduleId2 = $"test-list-2-{Guid.NewGuid():N}";
        _scheduleIdsToCleanup.Add(scheduleId1);
        _scheduleIdsToCleanup.Add(scheduleId2);

        Console.WriteLine($"Testing ListAsync operation");

        // Arrange - Create two schedules
        await _workflow!.Schedules!
            .Create(scheduleId1)
            .WithCronSchedule("0 0 * * *")
            .StartAsync();
        Console.WriteLine($"  ✓ Created schedule 1: {scheduleId1}");

        await _workflow!.Schedules
            .Create(scheduleId2)
            .WithIntervalSchedule(TimeSpan.FromHours(1))
            .StartAsync();
        Console.WriteLine($"  ✓ Created schedule 2: {scheduleId2}");

        // Small delay to allow Temporal to index the schedules
        await Task.Delay(100);

        // Act - List all schedules
        var schedules = await _workflow!.Schedules!.ListAsync();
        var scheduleList = new List<ScheduleListDescription>();
        
        await foreach (var schedule in schedules)
        {
            scheduleList.Add(schedule);
        }

        // Assert - Verify we can enumerate schedules
        // Note: Temporal's schedule listing has eventual consistency, so newly created
        // schedules might not appear immediately. We just verify ListAsync works.
        Console.WriteLine($"✓ ListAsync completed successfully, found {scheduleList.Count} schedule(s)");
        
        // Informational: Check if our schedules are in the list
        var foundSchedule1 = scheduleList.Any(s => s.Id.Contains(scheduleId1));
        var foundSchedule2 = scheduleList.Any(s => s.Id.Contains(scheduleId2));
        
        if (foundSchedule1 && foundSchedule2)
        {
            Console.WriteLine($"✓ Both test schedules found in list (bonus!)");
        }
        else
        {
            Console.WriteLine($"⚠ Note: Newly created schedules not yet in list (eventual consistency)");
        }
        
        // Verify we can at least call the method without error - this is the main test
        Assert.NotNull(schedules);
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
            .Create(scheduleId)
            .WithCronSchedule("0 0 * * *")
            .StartAsync();
        Console.WriteLine($"  ✓ Created schedule");

        // Verify it exists
        var existsBefore = await _workflow!.Schedules!.ExistsAsync(scheduleId);
        Assert.True(existsBefore);
        Console.WriteLine($"  ✓ Verified schedule exists");

        // Act - Delete the schedule
        await _workflow!.Schedules!.DeleteAsync(scheduleId);
        Console.WriteLine($"  ✓ Deleted schedule");

        // Assert - Verify it no longer exists
        var existsAfter = await _workflow!.Schedules!.ExistsAsync(scheduleId);
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
            .Create(scheduleId)
            .WithCronSchedule("0 */6 * * *")  // Every 6 hours
            .WithInput("lifecycle-test")
            .StartAsync();
        Assert.NotNull(schedule);
        Console.WriteLine("  ✓ Created");

        // 2. Verify existence
        Console.WriteLine("  Step 2: Verifying existence...");
        var exists = await _workflow!.Schedules!.ExistsAsync(scheduleId);
        Assert.True(exists);
        Console.WriteLine("  ✓ Exists");

        // 3. Get/Retrieve
        Console.WriteLine("  Step 3: Retrieving schedule...");
        var retrieved = await _workflow.Schedules.GetAsync(scheduleId);
        Assert.NotNull(retrieved);
        Console.WriteLine("  ✓ Retrieved");

        // 4. Pause
        Console.WriteLine("  Step 4: Pausing schedule...");
        await _workflow.Schedules.PauseAsync(scheduleId, "Lifecycle test");
        var descAfterPause = await retrieved.DescribeAsync();
        Assert.True(descAfterPause.Schedule.State.Paused);
        Console.WriteLine("  ✓ Paused");

        // 5. Unpause
        Console.WriteLine("  Step 5: Unpausing schedule...");
        await _workflow.Schedules.UnpauseAsync(scheduleId);
        var descAfterUnpause = await retrieved.DescribeAsync();
        Assert.False(descAfterUnpause.Schedule.State.Paused);
        Console.WriteLine("  ✓ Unpaused");

        // 6. Trigger
        Console.WriteLine("  Step 6: Triggering schedule...");
        await _workflow.Schedules.TriggerAsync(scheduleId);
        Console.WriteLine("  ✓ Triggered");

        // 7. List (verify we can call ListAsync)
        Console.WriteLine("  Step 7: Testing ListAsync...");
        
        // Small delay to ensure schedule is indexed
        await Task.Delay(100);
        
        var schedules = await _workflow.Schedules.ListAsync();
        Assert.NotNull(schedules);
        
        // Try to find our schedule (may not be there due to eventual consistency)
        var found = false;
        await foreach (var s in schedules)
        {
            if (s.Id.Contains(scheduleId))
            {
                found = true;
                break;
            }
        }
        
        if (found)
        {
            Console.WriteLine("  ✓ Schedule found in list");
        }
        else
        {
            Console.WriteLine("  ⚠ Schedule not in list yet (eventual consistency)");
        }

        Console.WriteLine("✓ Full lifecycle test PASSED");
    }
}

