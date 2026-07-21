using Temporalio.Common;
using Xians.Lib.Agents.Core;

namespace Xians.Lib.Tests.UnitTests.Agents;

/// <summary>
/// Resolver matrix for <see cref="WorkerVersioningResolver"/>: every mode × Build ID supplied/absent ×
/// deployment-name fallback, plus the backward-compatibility guard (null options → unversioned).
/// The library never reads environment variables, so these tests only exercise supplied options.
/// </summary>
public class WorkerVersioningResolverTests
{
    private const string AgentName = "TestAgent";

    // ---- Backward compatibility: nothing enabled -> unversioned ----

    [Fact]
    public void Resolve_WithNullOptions_ReturnsNull()
    {
        Assert.Null(WorkerVersioningResolver.Resolve(null, AgentName));
    }

    [Fact]
    public void Resolve_WithDisabledMode_ReturnsNull()
    {
        var options = new WorkerVersioningOptions { Mode = WorkerVersioningMode.Disabled, BuildId = "1.0" };

        Assert.Null(WorkerVersioningResolver.Resolve(options, AgentName));
    }

    // ---- Enabled mode ----

    [Fact]
    public void Resolve_EnabledMode_NoBuildId_Throws()
    {
        var options = new WorkerVersioningOptions { Mode = WorkerVersioningMode.Enabled };

        Assert.Throws<InvalidOperationException>(() => WorkerVersioningResolver.Resolve(options, AgentName));
    }

    [Fact]
    public void Resolve_EnabledMode_WithBuildId_ResolvesVersioned()
    {
        var options = new WorkerVersioningOptions { Mode = WorkerVersioningMode.Enabled, BuildId = "1.2.3" };

        var result = WorkerVersioningResolver.Resolve(options, AgentName);

        Assert.NotNull(result);
        Assert.Equal("1.2.3", result!.BuildId);
        Assert.Equal(AgentName, result.DeploymentName); // falls back to agent name
    }

    // ---- Deployment name fallback chain: explicit > agent name ----

    [Fact]
    public void Resolve_DeploymentName_ExplicitWinsOverAgentName()
    {
        var options = new WorkerVersioningOptions
        {
            Mode = WorkerVersioningMode.Enabled,
            BuildId = "1.0",
            DeploymentName = "explicit-deployment"
        };

        var result = WorkerVersioningResolver.Resolve(options, AgentName);

        Assert.Equal("explicit-deployment", result!.DeploymentName);
    }

    [Fact]
    public void Resolve_DeploymentName_FallsBackToAgentName()
    {
        var options = new WorkerVersioningOptions { Mode = WorkerVersioningMode.Enabled, BuildId = "1.0" };

        var result = WorkerVersioningResolver.Resolve(options, AgentName);

        Assert.Equal(AgentName, result!.DeploymentName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Resolve_NoDeploymentNameAndBlankAgentName_Throws(string? agentName)
    {
        var options = new WorkerVersioningOptions { Mode = WorkerVersioningMode.Enabled, BuildId = "1.0" };

        Assert.Throws<InvalidOperationException>(
            () => WorkerVersioningResolver.Resolve(options, agentName!));
    }

    // ---- Trimming ----

    [Fact]
    public void Resolve_TrimsSuppliedValues()
    {
        var options = new WorkerVersioningOptions
        {
            Mode = WorkerVersioningMode.Enabled,
            BuildId = "  1.0  ",
            DeploymentName = "  my-deployment  "
        };

        var result = WorkerVersioningResolver.Resolve(options, AgentName);

        Assert.Equal("1.0", result!.BuildId);
        Assert.Equal("my-deployment", result.DeploymentName);
    }

    // ---- Default behavior passthrough ----

    [Fact]
    public void Resolve_DefaultsToPinnedBehavior()
    {
        var options = new WorkerVersioningOptions { Mode = WorkerVersioningMode.Enabled, BuildId = "1.0" };

        var result = WorkerVersioningResolver.Resolve(options, AgentName);

        Assert.Equal(VersioningBehavior.Pinned, result!.DefaultBehavior);
    }

    [Fact]
    public void Resolve_PassesThroughConfiguredDefaultBehavior()
    {
        var options = new WorkerVersioningOptions
        {
            Mode = WorkerVersioningMode.Enabled,
            BuildId = "1.0",
            DefaultBehavior = VersioningBehavior.AutoUpgrade
        };

        var result = WorkerVersioningResolver.Resolve(options, AgentName);

        Assert.Equal(VersioningBehavior.AutoUpgrade, result!.DefaultBehavior);
    }
}
