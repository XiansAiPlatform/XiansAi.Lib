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

    public async Task<List<DbMessage>> GetMessageHistory(string? workflowType, string participantId, int page = 1, int pageSize = 10)
    {
        if (Workflow.InWorkflow) {
            return await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.GetMessageHistory(workflowType, participantId, page, pageSize),
                new SystemActivityOptions());
        } else {
            return await SystemActivities.GetMessageHistoryStatic(workflowType, participantId, page, pageSize);
        }
    }

}