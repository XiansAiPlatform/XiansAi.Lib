using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Workflows;

namespace Xians.Lib.Tests.UnitTests.Agents;

/// <summary>
/// Unit tests for the child workflow idPostfix (activation name) resolution used when
/// starting/executing sub-workflows. Verifies that:
/// - An explicit activation name always wins.
/// - The caller's idPostfix is inherited only for same-agent children.
/// - Cross-agent children without an explicit activation get no activation context.
/// - Outside workflow/activity context (caller agent unknown), legacy inheritance is preserved.
/// </summary>
public class SubWorkflowIdPostfixResolutionTests
{
    private const string CALLER_AGENT = "Invoice Orchestrator Agent";
    private const string TARGET_AGENT = "Fraud Detection Agent";
    private const string CALLER_ID_POSTFIX = "orchestrator-activation";
    private const string EXPLICIT_ACTIVATION = "fraud-activation";

    #region Pure resolution logic

    [Fact]
    public void ExplicitActivationName_AlwaysWins_EvenForSameAgent()
    {
        var result = SubWorkflowService.ResolveChildIdPostfix(
            EXPLICIT_ACTIVATION, CALLER_AGENT, CALLER_AGENT, CALLER_ID_POSTFIX);

        Assert.Equal(EXPLICIT_ACTIVATION, result);
    }

    [Fact]
    public void ExplicitActivationName_AlwaysWins_ForCrossAgent()
    {
        var result = SubWorkflowService.ResolveChildIdPostfix(
            EXPLICIT_ACTIVATION, TARGET_AGENT, CALLER_AGENT, CALLER_ID_POSTFIX);

        Assert.Equal(EXPLICIT_ACTIVATION, result);
    }

    [Fact]
    public void SameAgentChild_InheritsCallerIdPostfix()
    {
        var result = SubWorkflowService.ResolveChildIdPostfix(
            activationName: null,
            targetAgentName: CALLER_AGENT,
            callerAgentName: CALLER_AGENT,
            callerIdPostfix: CALLER_ID_POSTFIX);

        Assert.Equal(CALLER_ID_POSTFIX, result);
    }

    [Fact]
    public void CrossAgentChild_WithoutActivation_GetsNoIdPostfix()
    {
        var result = SubWorkflowService.ResolveChildIdPostfix(
            activationName: null,
            targetAgentName: TARGET_AGENT,
            callerAgentName: CALLER_AGENT,
            callerIdPostfix: CALLER_ID_POSTFIX);

        Assert.Null(result);
    }

    [Fact]
    public void UnknownCallerAgent_PreservesLegacyInheritance()
    {
        // Outside workflow/activity context, the caller agent cannot be determined;
        // the context idPostfix is inherited for backward compatibility.
        var result = SubWorkflowService.ResolveChildIdPostfix(
            activationName: null,
            targetAgentName: TARGET_AGENT,
            callerAgentName: null,
            callerIdPostfix: CALLER_ID_POSTFIX);

        Assert.Equal(CALLER_ID_POSTFIX, result);
    }

    [Fact]
    public void NoActivation_NoCallerIdPostfix_ReturnsNull()
    {
        var result = SubWorkflowService.ResolveChildIdPostfix(
            activationName: null,
            targetAgentName: TARGET_AGENT,
            callerAgentName: CALLER_AGENT,
            callerIdPostfix: null);

        Assert.Null(result);
    }

    [Fact]
    public void WhitespaceActivationName_IsTreatedAsNotProvided()
    {
        var result = SubWorkflowService.ResolveChildIdPostfix(
            activationName: "   ",
            targetAgentName: CALLER_AGENT,
            callerAgentName: CALLER_AGENT,
            callerIdPostfix: CALLER_ID_POSTFIX);

        Assert.Equal(CALLER_ID_POSTFIX, result);
    }

    [Fact]
    public void AgentNameComparison_IsCaseSensitive()
    {
        // Agent names are exact identifiers; a case difference means a different agent
        var result = SubWorkflowService.ResolveChildIdPostfix(
            activationName: null,
            targetAgentName: "my agent",
            callerAgentName: "My Agent",
            callerIdPostfix: CALLER_ID_POSTFIX);

        Assert.Null(result);
    }

    #endregion

    #region Context-based resolution (outside workflow context)

    [Fact]
    public void OutsideWorkflowContext_NoAsyncLocal_ReturnsNull()
    {
        XiansContext.ClearIdPostfix();
        try
        {
            var result = SubWorkflowService.ResolveChildIdPostfix(
                $"{TARGET_AGENT}:Fraud Detection Workflow", activationName: null);

            Assert.Null(result);
        }
        finally
        {
            XiansContext.ClearIdPostfix();
        }
    }

    [Fact]
    public void OutsideWorkflowContext_AsyncLocalIdPostfix_IsInherited()
    {
        // Outside a workflow, the caller agent is unknown; an idPostfix set on the
        // async-local context (e.g. activation-driven handlers) is inherited as before.
        XiansContext.SetIdPostfix(CALLER_ID_POSTFIX);
        try
        {
            var result = SubWorkflowService.ResolveChildIdPostfix(
                $"{TARGET_AGENT}:Fraud Detection Workflow", activationName: null);

            Assert.Equal(CALLER_ID_POSTFIX, result);
        }
        finally
        {
            XiansContext.ClearIdPostfix();
        }
    }

    [Fact]
    public void OutsideWorkflowContext_ExplicitActivation_OverridesAsyncLocal()
    {
        XiansContext.SetIdPostfix(CALLER_ID_POSTFIX);
        try
        {
            var result = SubWorkflowService.ResolveChildIdPostfix(
                $"{TARGET_AGENT}:Fraud Detection Workflow", EXPLICIT_ACTIVATION);

            Assert.Equal(EXPLICIT_ACTIVATION, result);
        }
        finally
        {
            XiansContext.ClearIdPostfix();
        }
    }

    #endregion
}
