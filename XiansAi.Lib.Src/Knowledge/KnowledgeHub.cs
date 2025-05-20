using Temporalio.Workflows;

namespace XiansAi.Knowledge;

public static class KnowledgeHub
{
    public static async Task<Models.Knowledge?> Fetch(string knowledgeName)
    {
        if (Workflow.InWorkflow) {
            // Go through a Temporal activity to perform IO operations
            var response = await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.GetKnowledgeAsync(knowledgeName),
                new SystemActivityOptions());

            return response;
        } else {
            return await SystemActivities.GetKnowledgeAsyncStatic(knowledgeName);
        }

    }

    public static async Task<bool> Update(string knowledgeName, string knowledgeType,  string knowledgeContent)
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
            return await SystemActivities.UpdateKnowledgeAsyncStatic(knowledgeName, knowledgeType, knowledgeContent);
        }
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to update knowledge: {knowledgeName}", e);
        }
    }

}
