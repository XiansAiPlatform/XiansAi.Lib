using Temporalio.Converters;
using Xians.Lib.Agents.Tasks.Models;

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
}
