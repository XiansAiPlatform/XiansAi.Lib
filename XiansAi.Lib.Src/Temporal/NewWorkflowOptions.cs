
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Common;

public class NewWorkflowOptions : WorkflowOptions
{
    public NewWorkflowOptions(string workflowType, string? workflowId = null)
    {
        Id = workflowId ?? AgentContext.TenantId + ":" + workflowType;
        TaskQueue = AgentContext.TenantId + ":" + workflowType;
        Memo = GetMemo();
        TypedSearchAttributes = GetSearchAttributes();
        IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting;
    }

    private SearchAttributeCollection GetSearchAttributes()
    {
        var searchAttributesBuilder = new SearchAttributeCollection.Builder()
                    .Set(SearchAttributeKey.CreateKeyword(Constants.TenantIdKey), AgentContext.TenantId)
                    .Set(SearchAttributeKey.CreateKeyword(Constants.AgentKey), AgentContext.AgentName)
                    .Set(SearchAttributeKey.CreateKeyword(Constants.UserIdKey), AgentContext.UserId);

        return searchAttributesBuilder.ToSearchAttributeCollection();
    }

    private Dictionary<string, object> GetMemo()
    {
        var memo = new Dictionary<string, object> {
            { Constants.TenantIdKey, AgentContext.TenantId },
            { Constants.AgentKey, AgentContext.AgentName },
            { Constants.UserIdKey, AgentContext.UserId },
        };

        return memo;
    }

}
