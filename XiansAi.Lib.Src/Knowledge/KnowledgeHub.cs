using Temporalio.Workflows;

namespace XiansAi.Knowledge;


public static class KnowledgeHub
{
    public static async Task<Models.Knowledge?> Fetch(string knowledgeName)
    {
        // Fetch from source
        Models.Knowledge? knowledge;
        if (Workflow.InWorkflow) {
            // Go through a Temporal activity to perform IO operations
            knowledge = await Workflow.ExecuteLocalActivityAsync(
                (SystemActivities a) => a.GetKnowledgeAsync(knowledgeName),
                new SystemLocalActivityOptions());
        } else {
            knowledge = await SystemActivities.GetKnowledgeAsyncStatic(knowledgeName);
        }

        return knowledge;
    }

    public static async Task<bool> Update(string knowledgeName, string knowledgeType, string knowledgeContent)
    {
        try
        {
            bool result;
            if (Workflow.InWorkflow) {
                // Go through a Temporal activity to perform IO operations
                result = await Workflow.ExecuteLocalActivityAsync(
                    (SystemActivities a) => a.UpdateKnowledgeAsync(knowledgeName, knowledgeType, knowledgeContent),
                    new SystemLocalActivityOptions());
            } else {
                result = await SystemActivities.UpdateKnowledgeAsyncStatic(knowledgeName, knowledgeType, knowledgeContent);
            }

            return result;
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to update knowledge: {knowledgeName}", e);
        }
    }

}
