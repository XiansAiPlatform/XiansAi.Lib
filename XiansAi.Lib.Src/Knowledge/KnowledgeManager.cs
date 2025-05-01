using Temporalio.Common;
using Temporalio.Workflows;

namespace XiansAi.Knowledge;

public interface IKnowledgeManager
{
    Task<Models.Knowledge?> GetKnowledgeAsync(string knowledgeName);
}

public class KnowledgeManager : IKnowledgeManager
{

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

}
