using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using XiansAi.Messaging;

public class ThreadHistoryService
{
    private readonly ILogger _logger;

    public ThreadHistoryService()
    {
        _logger = Globals.LogFactory.CreateLogger<ThreadHistoryService>();
    }

    public async Task<List<DbMessage>> GetMessageHistory(string? workflowType, string participantId, string? scope, int page = 1, int pageSize = 10)
    {
        if (Workflow.InWorkflow) {
            return await Workflow.ExecuteLocalActivityAsync(
                (SystemActivities a) => a.GetMessageHistory(workflowType, participantId, scope, page, pageSize),
                new SystemLocalActivityOptions());
        } else {
            return await SystemActivities.GetMessageHistoryStatic(workflowType, participantId, scope, page, pageSize);
        }
    }

}