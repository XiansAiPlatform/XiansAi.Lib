using Temporalio.Workflows;
using XiansAi.Logging;

namespace XiansAi.Knowledge;

public interface IKnowledgeHub
{
    Task<Models.Knowledge?> GetKnowledgeAsync(string knowledgeName);
}

public class KnowledgeHub : IKnowledgeHub
{
    private readonly Logger<KnowledgeHub> _logger = Logger<KnowledgeHub>.For();

    public async Task<Models.Knowledge?> GetKnowledgeAsync(string knowledgeName)
    {
        if (Workflow.InWorkflow) {
            // Go through a Temporal activity to perform IO operations
            var response = await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.GetKnowledgeAsync(knowledgeName),
                new SystemActivityOptions());

            return response;
        } else {
            return await new SystemActivities().GetKnowledgeAsync(knowledgeName);
        }

    }

    public async Task<bool> UpdateKnowledgeAsync(string knowledgeName, string knowledgeType,  string knowledgeContent)
    {
        try
        {
            if (Workflow.InWorkflow) {
            // Go through a Temporal activity to perform IO operations
            var response = await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.UpdateKnowledgeAsync(knowledgeName, knowledgeType, knowledgeContent),
                new SystemActivityOptions());

            return response;
        } else {
            return await new SystemActivities().UpdateKnowledgeAsync(knowledgeName, knowledgeType, knowledgeContent);
        }
        }
        catch (Exception)
        {
            _logger.LogError($"Failed to update knowledge: {knowledgeName}");
            return false;
        }
    }

}
