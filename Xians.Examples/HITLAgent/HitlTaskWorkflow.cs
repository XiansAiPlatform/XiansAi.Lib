using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Tasks.Models;

namespace Xians.Examples.HITLAgent;

/// <summary>
/// Opens a human-in-the-loop task and tells the chatting user how it turned out.
/// Task creation has to run inside a workflow. The chat handler is an activity, so it starts this workflow.
/// </summary>
[Description("Opens a human-in-the-loop task from a chat request and reports the decision")]
[Workflow("HITL Agent:HITL Task Workflow")]
public class HitlTaskWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(HitlTaskRequest request)
    {
        Workflow.Logger.LogInformation("Opening HITL task '{Title}' for {ParticipantId}", request.Title, request.ParticipantId);

        var taskHandle = await XiansContext.CurrentAgent.Tasks.StartTaskAsync(new TaskWorkflowRequest
        {
            Title = request.Title,
            Description = request.Description,
            DraftWork = request.DraftWork,
            ParticipantId = request.ParticipantId,
            Actions = request.Actions,
            Timeout = request.TimeoutSeconds is int seconds ? TimeSpan.FromSeconds(seconds) : null
        });

        await XiansContext.Messaging.SendChatAsSupervisorAsync(
            $"A task is waiting for a decision: {request.Title}\n{request.Description}",
            scope: request.Scope,
            taskId: taskHandle.Id,
            participantId: request.ParticipantId);

        var result = await XiansContext.CurrentAgent.Tasks.GetResultAsync(taskHandle);
        await XiansContext.Messaging.SendChatAsSupervisorAsync(
            DescribeOutcome(request.Title, result),
            scope: request.Scope,
            participantId: request.ParticipantId);

        Workflow.Logger.LogInformation(
            "HITL task '{Title}' finished. Action={Action}, TimedOut={TimedOut}",
            request.Title,
            result.PerformedAction,
            result.TimedOut);
    }

    private static string DescribeOutcome(string title, TaskWorkflowResult result)
    {
        if (result.TimedOut)
        {
            return $"The task \"{title}\" timed out before anyone completed it.";
        }

        var action = string.IsNullOrWhiteSpace(result.PerformedAction) ? "completed" : result.PerformedAction;
        if (string.IsNullOrWhiteSpace(result.Comment))
        {
            return $"The task \"{title}\" was completed with \"{action}\".";
        }

        return $"The task \"{title}\" was completed with \"{action}\". Comment: {result.Comment}";
    }
}
