using Temporalio.Common;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common;

namespace Xians.Lib.Tests.UnitTests.Agents;

/// <summary>
/// Matrix for <see cref="WorkflowMetadataResolver.SanitizeForStart"/>: server-reserved Temporal*
/// attributes (stamped by the server under Worker Deployment Versioning and rejected when a client
/// sets them in a start request) must be stripped, while library-owned and custom attributes survive
/// with values and types intact. Null stays null, empty stays empty (never converted to null), and
/// the no-op path returns the original instance.
/// </summary>
public class WorkflowMetadataResolverSanitizeTests
{
    private static SearchAttributeCollection BuildKeywords(params (string Name, string Value)[] attrs)
    {
        var builder = new SearchAttributeCollection.Builder();
        foreach (var (name, value) in attrs)
        {
            builder.Set(SearchAttributeKey.CreateKeyword(name), value);
        }
        return builder.ToSearchAttributeCollection();
    }

    // ---- Null / empty semantics ----

    [Fact]
    public void SanitizeForStart_NullInput_ReturnsNull()
    {
        Assert.Null(WorkflowMetadataResolver.SanitizeForStart(null));
    }

    [Fact]
    public void SanitizeForStart_EmptyInput_ReturnsEmptyNotNull()
    {
        var result = WorkflowMetadataResolver.SanitizeForStart(SearchAttributeCollection.Empty);

        Assert.NotNull(result);
        Assert.Empty(result!);
    }

    // ---- Reserved attributes are stripped ----

    [Theory]
    [InlineData("TemporalWorkerDeployment")]
    [InlineData("TemporalWorkerDeploymentVersion")]
    [InlineData("TemporalWorkflowVersioningBehavior")]
    public void SanitizeForStart_StripsVersioningAttribute(string reservedName)
    {
        var input = BuildKeywords(
            (reservedName, "data-quality-agent.abc123"),
            (WorkflowConstants.Keys.TenantId, "tenant-1"));

        var result = WorkflowMetadataResolver.SanitizeForStart(input);

        Assert.NotNull(result);
        Assert.Null(WorkflowMetadataResolver.GetValueFromSearchAttributes(result, reservedName));
        Assert.Equal("tenant-1",
            WorkflowMetadataResolver.GetValueFromSearchAttributes(result, WorkflowConstants.Keys.TenantId));
        Assert.Single(result!);
    }

    [Theory]
    [InlineData("TemporalChangeVersion")]
    [InlineData("TemporalScheduledById")]
    [InlineData("TemporalScheduledStartTime")]
    [InlineData("TemporalNamespaceDivision")]
    [InlineData("TemporalPauseInfo")]
    public void SanitizeForStart_StripsOtherReservedTemporalAttributes(string reservedName)
    {
        var input = BuildKeywords(
            (reservedName, "some-value"),
            (WorkflowConstants.Keys.Agent, "Data Quality Agent"));

        var result = WorkflowMetadataResolver.SanitizeForStart(input);

        Assert.NotNull(result);
        Assert.Null(WorkflowMetadataResolver.GetValueFromSearchAttributes(result, reservedName));
        Assert.Equal("Data Quality Agent",
            WorkflowMetadataResolver.GetValueFromSearchAttributes(result, WorkflowConstants.Keys.Agent));
    }

    [Fact]
    public void SanitizeForStart_AllReservedInput_ReturnsEmptyCollectionNotNull()
    {
        var input = BuildKeywords(
            ("TemporalWorkerDeployment", "dq-agent"),
            ("TemporalWorkerDeploymentVersion", "dq-agent.abc123"),
            ("TemporalWorkflowVersioningBehavior", "Pinned"));

        var result = WorkflowMetadataResolver.SanitizeForStart(input);

        Assert.NotNull(result);
        Assert.Empty(result!);
    }

    // ---- Library-owned and custom attributes survive ----

    [Fact]
    public void SanitizeForStart_PreservesStandardKeysAndCustomAttributes()
    {
        var longKey = SearchAttributeKey.CreateLong("customCount");
        var input = new SearchAttributeCollection.Builder()
            .Set(SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.TenantId), "tenant-1")
            .Set(SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.Agent), "Data Quality Agent")
            .Set(SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.UserId), "user-42")
            .Set(SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.idPostfix), "activation-a")
            .Set(SearchAttributeKey.CreateKeyword("customerRegion"), "emea")
            .Set(longKey, 7L)
            .Set(SearchAttributeKey.CreateKeyword("TemporalWorkerDeployment"), "dq-agent")
            .ToSearchAttributeCollection();

        var result = WorkflowMetadataResolver.SanitizeForStart(input);

        Assert.NotNull(result);
        Assert.Equal(6, result!.Count);
        foreach (var key in WorkflowMetadataResolver.StandardMetadataKeys)
        {
            Assert.NotNull(WorkflowMetadataResolver.GetValueFromSearchAttributes(result, key));
        }
        Assert.Equal("emea", WorkflowMetadataResolver.GetValueFromSearchAttributes(result, "customerRegion"));
        Assert.Equal(7L, result.Get(longKey));
        Assert.Null(WorkflowMetadataResolver.GetValueFromSearchAttributes(result, "TemporalWorkerDeployment"));
    }

    // ---- Fast path and prefix matching rules ----

    [Fact]
    public void SanitizeForStart_NothingToStrip_ReturnsSameInstance()
    {
        var input = BuildKeywords(
            (WorkflowConstants.Keys.TenantId, "tenant-1"),
            (WorkflowConstants.Keys.Agent, "Data Quality Agent"));

        var result = WorkflowMetadataResolver.SanitizeForStart(input);

        Assert.Same(input, result);
    }

    [Fact]
    public void SanitizeForStart_IsCaseSensitiveOrdinal()
    {
        // The server reserves the exact "Temporal" prefix; a lowercase name is a legal custom attribute.
        var input = BuildKeywords(("temporalCustom", "keep-me"));

        var result = WorkflowMetadataResolver.SanitizeForStart(input);

        Assert.Same(input, result);
        Assert.Equal("keep-me", WorkflowMetadataResolver.GetValueFromSearchAttributes(result, "temporalCustom"));
    }
}
