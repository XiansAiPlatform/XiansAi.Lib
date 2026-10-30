using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Workflows;
using Xians.Lib.Common;

namespace Xians.Lib.Tests.UnitTests.Agents;

/// <summary>
/// Unit tests for the userId a child workflow inherits from its parent's search attributes.
/// A child started while handling a message carries that message's participant; otherwise
/// the parent's userId is kept.
/// </summary>
public class SubWorkflowUserIdInheritanceTests : IDisposable
{
    private const string TENANT_ID = "tenant-a";
    private const string AGENT_NAME = "Fraud Detection Agent";
    private const string PARENT_USER = "heartbeat";

    [Fact]
    public void ChildStartedForMessage_UsesMessageParticipant()
    {
        XiansContext.SetParticipantId("jane@example.com");

        var result = SubWorkflowService.OverrideChildMetadata(ParentAttributes(), TENANT_ID, AGENT_NAME, null);

        Assert.Equal("jane@example.com", UserIdOf(result));
    }

    [Fact]
    public void ChildStartedWithoutMessage_KeepsParentUser()
    {
        XiansContext.ClearParticipantId();

        var result = SubWorkflowService.OverrideChildMetadata(ParentAttributes(), TENANT_ID, AGENT_NAME, null);

        Assert.Equal(PARENT_USER, UserIdOf(result));
    }

    public void Dispose() => XiansContext.ClearParticipantId();

    private static Temporalio.Common.SearchAttributeCollection ParentAttributes() =>
        WorkflowMetadataResolver.BuildSearchAttributes(TENANT_ID, "Parent Agent", PARENT_USER, "parent-activation");

    private static string? UserIdOf(Temporalio.Common.SearchAttributeCollection attributes) =>
        WorkflowMetadataResolver.GetValueFromSearchAttributes(attributes, WorkflowConstants.Keys.UserId);
}
