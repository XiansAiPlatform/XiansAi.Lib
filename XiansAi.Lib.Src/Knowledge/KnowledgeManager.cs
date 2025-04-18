using Temporalio.Workflows;

namespace XiansAi.Knowledge;

public interface IKnowledgeManager
{
    Task<Models.Knowledge> GetKnowledgeAsync(string knowledgeName);
}

public class KnowledgeManager : IKnowledgeManager
{

    public async Task<Models.Knowledge> GetKnowledgeAsync(string knowledgeName)
    {
        // Go through a Temporal activity to perform IO operations
        var response = await Workflow.ExecuteActivityAsync(
            (SystemActivities a) => a.GetKnowledgeAsync(knowledgeName),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(60) });

        return response;
    }

}

class KnowledgeManagerImpl: IKnowledgeManager
{

    public async Task<Models.Knowledge> GetKnowledgeAsync(string knowledgeName)
    {
        var knowledgeLoader = new KnowledgeLoaderImpl();
        var knowledge = await knowledgeLoader.Load(knowledgeName);
        return knowledge ?? throw new InvalidOperationException($"Knowledge {knowledgeName} not found");
    }

}