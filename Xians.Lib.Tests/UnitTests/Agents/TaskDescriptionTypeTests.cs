using Temporalio.Converters;
using Xians.Lib.Agents.Tasks.Models;
using Xians.Lib.Common;
using Xians.Lib.Temporal.Workflows;
using Xians.Lib.Temporal.Workflows.Tasks;

namespace Xians.Lib.Tests.UnitTests.Agents;

/// <summary>
/// Verifies TaskDescriptionType survives Temporal's default payload conversion by name,
/// which is how the server reads it from the task workflow input and GetTaskInfo query.
///
/// dotnet test --filter "FullyQualifiedName~TaskDescriptionTypeTests"
/// </summary>
public class TaskDescriptionTypeTests
{
    private static readonly IPayloadConverter Converter = DataConverter.Default.PayloadConverter;

    [Fact]
    public void TaskWorkflowRequest_DefaultsToString()
    {
        var request = new TaskWorkflowRequest { Title = "t", Description = "d", TaskName = "task" };

        Assert.Equal(TaskDescriptionType.String, request.DescriptionType);
    }

    [Fact]
    public void TaskWorkflowRequest_SerializesDescriptionTypeByName()
    {
        var request = new TaskWorkflowRequest
        {
            Title = "Review order",
            Description = "{\"amount\":1200}",
            DescriptionType = TaskDescriptionType.Json,
            TaskName = "task"
        };

        var payload = Converter.ToPayload(request);

        Assert.Contains("\"DescriptionType\":\"Json\"", payload.Data.ToStringUtf8());
        Assert.Equal(TaskDescriptionType.Json, Converter.ToValue<TaskWorkflowRequest>(payload).DescriptionType);
    }

    [Fact]
    public void TaskWorkflowRequest_WithoutDescriptionType_DeserializesAsString()
    {
        // Workflow input recorded by an SDK version that predates DescriptionType.
        var legacy = Converter.ToPayload(new { Title = "t", Description = "d", TaskName = "task" });

        Assert.Equal(TaskDescriptionType.String, Converter.ToValue<TaskWorkflowRequest>(legacy).DescriptionType);
    }

    [Fact]
    public void TaskMemo_StoresDescriptionTypeByName()
    {
        // The server reads this memo entry to build the task list.
        var memo = TaskWorkflowOptions.BuildInheritedMemo(
            "tenant", "agent", false, "", "Review order", "## Order", TaskDescriptionType.Markdown, "user", ["OK"]);

        Assert.Equal("Markdown", memo[WorkflowConstants.Keys.TaskDescriptionType]);
    }

    [Fact]
    public void GetTaskInfo_ReturnsRequestDescriptionType()
    {
        var workflow = new TaskWorkflow();

        // RunAsync stores the request before waiting; outside a workflow the wait faults, which is ignored here.
        _ = workflow.RunAsync(new TaskWorkflowRequest
        {
            Title = "Review order",
            Description = "{\"amount\":1200}",
            DescriptionType = TaskDescriptionType.Json,
            TaskName = "task"
        });

        var payload = Converter.ToPayload(workflow.GetTaskInfo());

        Assert.Contains("\"DescriptionType\":\"Json\"", payload.Data.ToStringUtf8());
        Assert.Equal(TaskDescriptionType.Json, Converter.ToValue<TaskInfo>(payload).DescriptionType);
    }
}
