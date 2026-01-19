using Microsoft.Extensions.Logging;
using Temporalio.Exceptions;
using Temporalio.Workflows;
using Xians.Agent.Sample;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Tasks;
using Xians.Lib.Agents.Tasks.Models;

[Workflow(Constants.AgentName + ":Content Processing Workflow")]
public class ContentProcessingWorkflow
{
    private readonly ILogger<ContentProcessingWorkflow> _logger;

    public ContentProcessingWorkflow()
    {
        _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<ContentProcessingWorkflow>();
    }

    [WorkflowRun]
    public async Task<string?> RunAsync(string contentURL, string reportingUserID)
    {
        _logger.LogInformation("Processing content: {ContentURL}, Reporting user ID: {ReportingUserID}", contentURL, reportingUserID);

        if (string.IsNullOrEmpty(reportingUserID))
        {
            throw new ApplicationFailureException("Reporting user ID is required");
        }
        if (string.IsNullOrEmpty(contentURL) || !Uri.TryCreate(contentURL, UriKind.Absolute, out _))
        {
            throw new ApplicationFailureException("Content URL is required and must be a valid URL: " + contentURL);
        }

        await XiansContext.Messaging.SendChatAsWorkflowAsync(Constants.ConversationalWorkflowName, reportingUserID, $"A new article found: {contentURL}", scope: contentURL);

        var taskHandle = await TaskWorkflowService.StartTaskAsync(
            new TaskWorkflowRequest
            {
                TaskId = $"content-approval-{Workflow.NewGuid()}",
                Title = "Approve Content",
                Description = "Approve the content before it is published",
                ParticipantId = reportingUserID,
                DraftWork = contentURL,
                Actions = ["publish", "reject", "revise"]
            }
        );

        await XiansContext.Messaging.SendChatAsWorkflowAsync(Constants.ConversationalWorkflowName, reportingUserID, $"This article is ready to be published: {contentURL}", scope: contentURL, hint: taskHandle.Id);

        await XiansContext.Messaging.SendChatAsWorkflowAsync(Constants.ConversationalWorkflowName, reportingUserID, $"Please review. Should I publish this article?", scope: contentURL);

        var result = await TaskWorkflowService.GetResultAsync(taskHandle);

        _logger.LogInformation("Content processed: {ContentURL}, Action: {Action}", contentURL, result.PerformedAction);

        return result.PerformedAction switch
        {
            "publish" => $"Published: {result.FinalWork}",
            "reject" => $"Rejected: {result.Comment}",
            "revise" => $"Revision requested: {result.Comment}",
            _ => $"Unknown action: {result.PerformedAction}"
        };
    }
}
