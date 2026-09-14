using System.Reflection;
using Moq;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Workflows;
using Xians.Lib.Common;
using Xians.Lib.Http;
using Xians.Lib.Temporal;
using Xians.Lib.Temporal.Workflows;
using Xians.Lib.Tests.TestUtilities;
using Xians.Lib.Tests.UnitTests.Common;

namespace Xians.Lib.Tests.UnitTests.Agents;

/// <summary>
/// End-to-end Unicode coverage for agent registration, registry lookup, workflow identifiers,
/// and dynamic workflow type names (Norwegian æ/ø/å, NFC).
/// </summary>
[Collection("Sequential")]
public class AgentNameUnicodeTests : IDisposable
{
    public AgentNameUnicodeTests()
    {
        XiansContext.CleanupForTests();
    }

    public void Dispose()
    {
        XiansContext.CleanupForTests();
        DynamicWorkflowTypeBuilder.ClearCacheForTests();
    }

    [Fact]
    public void XiansAgent_AcceptsNorwegianName_AndRegistersUnderNfc()
    {
        var agent = CreateAgent(IdentifierSanitizerTests.NorwegianAgentName);

        Assert.Equal(IdentifierSanitizerTests.NorwegianAgentName, agent.Name);
        Assert.True(XiansContext.TryGetAgent(IdentifierSanitizerTests.NorwegianAgentName, out var found));
        Assert.Same(agent, found);
    }

    [Fact]
    public void XiansAgent_LookupWithDecomposedARing_FindsComposedRegistration()
    {
        var composed = "Kåre";
        var decomposed = "Ka\u030Are";
        var agent = CreateAgent(composed);

        Assert.True(XiansContext.TryGetAgent(decomposed, out var found));
        Assert.Same(agent, found);
        Assert.Equal(composed, found!.Name);
    }

    [Fact]
    public void XiansAgent_RejectsMarkupInName()
    {
        Assert.Throws<ArgumentException>(() => CreateAgent("bad<script>"));
    }

    [Fact]
    public void BuildBuiltInWorkflowType_PreservesNorwegianAgentName()
    {
        var workflowType = XiansContext.BuildBuiltInWorkflowType(
            IdentifierSanitizerTests.NorwegianAgentName,
            "Supervisor Workflow");

        Assert.Equal($"{IdentifierSanitizerTests.NorwegianAgentName}:Supervisor Workflow", workflowType);
    }

    [Fact]
    public void GetTaskWorkflowType_PreservesNorwegianAgentName()
    {
        var workflowType = WorkflowConstants.WorkflowTypes.GetTaskWorkflowType(
            IdentifierSanitizerTests.NorwegianAgentName);

        Assert.Equal($"{IdentifierSanitizerTests.NorwegianAgentName}:Task Workflow", workflowType);
    }

    [Fact]
    public void GetTaskWorkflowType_RejectsInvalidAgentName()
    {
        Assert.Throws<ArgumentException>(() =>
            WorkflowConstants.WorkflowTypes.GetTaskWorkflowType("bad<script>"));
    }

    [Fact]
    public void DynamicWorkflowTypeBuilder_PreservesUnicodeInWorkflowAttribute()
    {
        var workflowType = $"{IdentifierSanitizerTests.NorwegianAgentName}:Supervisor Workflow";

        var type = DynamicWorkflowTypeBuilder.GetOrCreateType(workflowType);
        var attr = type.GetCustomAttribute<WorkflowAttribute>();

        Assert.NotNull(attr);
        Assert.Equal(workflowType, attr!.Name);
        Assert.Contains("Kjøpsassistent", type.Name);
    }

    [Fact]
    public void DynamicWorkflowTypeBuilder_NfcKey_DoesNotCreateDuplicateType()
    {
        var composed = "Kåre:Chat";
        var decomposed = "Ka\u030Are:Chat";

        var first = DynamicWorkflowTypeBuilder.GetOrCreateType(decomposed);
        var second = DynamicWorkflowTypeBuilder.GetOrCreateType(composed);

        Assert.Same(first, second);
        Assert.Equal(composed, first.GetCustomAttribute<WorkflowAttribute>()!.Name);
    }

    [Fact]
    public void DynamicWorkflowTypeBuilder_TaskWorkflowNfcKey_DoesNotCreateDuplicateType()
    {
        var composed = "Kåre:Task Workflow";
        var decomposed = "Ka\u030Are:Task Workflow";

        var first = DynamicWorkflowTypeBuilder.GetOrCreateTaskWorkflowType(decomposed);
        var second = DynamicWorkflowTypeBuilder.GetOrCreateTaskWorkflowType(composed);

        Assert.Same(first, second);
        Assert.True(typeof(TaskWorkflow).IsAssignableFrom(first));
        Assert.Equal(composed, first.GetCustomAttribute<WorkflowAttribute>()!.Name);
    }

    [Fact]
    public void DynamicWorkflowTypeBuilder_BuiltinAndTaskWorkflow_DoNotShareCacheEntry()
    {
        var workflowType = "SharedAgent:Task Workflow";

        var builtin = DynamicWorkflowTypeBuilder.GetOrCreateType(workflowType);
        var task = DynamicWorkflowTypeBuilder.GetOrCreateTaskWorkflowType(workflowType);

        Assert.NotSame(builtin, task);
        Assert.True(typeof(BuiltinWorkflow).IsAssignableFrom(builtin));
        Assert.True(typeof(TaskWorkflow).IsAssignableFrom(task));
        Assert.Equal(workflowType, builtin.GetCustomAttribute<WorkflowAttribute>()!.Name);
        Assert.Equal(workflowType, task.GetCustomAttribute<WorkflowAttribute>()!.Name);
    }

    private static XiansAgent CreateAgent(string name)
    {
        var mockHttpService = new Mock<IHttpClientService>();
        var mockTemporalService = new Mock<ITemporalClientService>();
        mockTemporalService.Setup(x => x.IsConnectionHealthy()).Returns(true);

        var options = new XiansOptions
        {
            ApiKey = TestCertificateGenerator.GenerateTestCertificateBase64("test-tenant", "test-user"),
            ServerUrl = "http://localhost"
        };

        return new XiansAgent(
            name,
            false,
            null, null, null, null, null, null, null,
            mockTemporalService.Object,
            mockHttpService.Object,
            options,
            null);
    }
}
