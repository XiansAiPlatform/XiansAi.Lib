using Xians.Lib.Agents.Workflows.Models;

namespace Xians.Lib.Tests.UnitTests.Agents;

/// <summary>
/// Defaults and clone behaviour for <see cref="WorkflowOptions"/>, focused on
/// <see cref="WorkflowOptions.MaxCachedWorkflows"/> — the sticky cache bound that decides how much memory a
/// worker retains after a burst of executions.
/// </summary>
public class WorkflowOptionsTests
{
    [Fact]
    public void MaxCachedWorkflows_DefaultsWellBelowTemporalsOwnDefault()
    {
        var options = new WorkflowOptions();

        // Temporal applies 10,000 per worker when the option is left unset. An agent process runs one worker
        // per defined workflow, so that default lets a few definitions retain tens of thousands of executions.
        Assert.Equal(500, options.MaxCachedWorkflows);
        Assert.True(options.MaxCachedWorkflows < 10_000);
    }

    [Fact]
    public void MaxCachedWorkflows_DefaultLeavesHeadroomAboveConcurrentTasks()
    {
        var options = new WorkflowOptions();

        // A cache at or below the concurrent task count thrashes: an execution still being worked on gets
        // evicted to make room for the next one.
        Assert.True(
            options.MaxCachedWorkflows > options.MaxConcurrent,
            $"cache {options.MaxCachedWorkflows} must exceed concurrency {options.MaxConcurrent}");
    }

    [Fact]
    public void MaxCachedWorkflows_IsSettable()
    {
        var options = new WorkflowOptions { MaxCachedWorkflows = 50 };

        Assert.Equal(50, options.MaxCachedWorkflows);
    }

    [Fact]
    public void Clone_CarriesMaxCachedWorkflows()
    {
        var options = new WorkflowOptions
        {
            MaxCachedWorkflows = 1234,
            MaxConcurrent = 7,
            MaxHistoryLength = 99,
            Activable = false,
            InactivityTimeout = TimeSpan.FromMinutes(5)
        };

        var clone = InvokeClone(options);

        // A clone that dropped this would silently restore Temporal's 10,000 for every workflow defined
        // through the collection, since the clone is what the worker is built from.
        Assert.Equal(1234, clone.MaxCachedWorkflows);
        Assert.Equal(7, clone.MaxConcurrent);
        Assert.Equal(99, clone.MaxHistoryLength);
        Assert.False(clone.Activable);
        Assert.Equal(TimeSpan.FromMinutes(5), clone.InactivityTimeout);
    }

    [Fact]
    public void Clone_OfDefaults_KeepsTheDefaultCacheBound()
    {
        var clone = InvokeClone(new WorkflowOptions());

        Assert.Equal(new WorkflowOptions().MaxCachedWorkflows, clone.MaxCachedWorkflows);
    }

    /// <summary>
    /// Clone is internal to the library and there is no public seam that exposes it, so the test reaches it
    /// directly rather than asserting on a proxy for it.
    /// </summary>
    private static WorkflowOptions InvokeClone(WorkflowOptions options)
    {
        var method = typeof(WorkflowOptions).GetMethod(
            "Clone",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(method);
        return (WorkflowOptions)method!.Invoke(options, null)!;
    }
}
