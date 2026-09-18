using System.ComponentModel;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;

namespace PromptDefinedAgent.Scheduling;

[Description("Executes a prompt on a Temporal schedule and sends the result to the requesting participant")]
[Workflow("Prompt Defined Agent:Scheduled Prompt Workflow")]
public sealed class ScheduledPromptWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(ScheduledPromptRequest request)
    {
        var response = await Workflow.ExecuteActivityAsync(
            (ScheduledPromptActivities activity) => activity.ExecuteAsync(request),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(10) });
        await XiansContext.Messaging.SendChatAsSupervisorAsync(
            response, scope: request.Scope, participantId: request.ParticipantId);
    }
}
