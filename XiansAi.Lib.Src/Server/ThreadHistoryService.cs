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

    public async Task<List<HistoricalMessage>> GetMessageHistory(string agent, string participantId, int page = 1, int pageSize = 10)
    {
        if (Workflow.InWorkflow) {
            return await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.GetMessageHistory(agent, participantId, page, pageSize),
                new SystemActivityOptions());
        } else {
            return await SystemActivities.GetMessageHistoryStatic(agent, participantId, page, pageSize);
        }
    }

}