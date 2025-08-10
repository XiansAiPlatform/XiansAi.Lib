
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Common;

namespace Temporal;

public class NewWorkflowOptions : WorkflowOptions
{
    public NewWorkflowOptions(string workflowType, string? idPostfix = null)
    {
        Id = AgentContext.TenantId + ":" + workflowType + (idPostfix != null ? idPostfix : "");
        TaskQueue = AgentContext.TenantId + ":" + workflowType;
        Memo = GetMemo();
        TypedSearchAttributes = GetSearchAttributes();
        IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting;
    }

    public SearchAttributeCollection GetSearchAttributes()
    {
        var searchAttributesBuilder = new SearchAttributeCollection.Builder()
                    .Set(SearchAttributeKey.CreateKeyword(Constants.TenantIdKey), AgentContext.TenantId)
                    .Set(SearchAttributeKey.CreateKeyword(Constants.AgentKey), AgentContext.AgentName)
                    .Set(SearchAttributeKey.CreateKeyword(Constants.UserIdKey), AgentContext.UserId);

        return searchAttributesBuilder.ToSearchAttributeCollection();
    }

    public Dictionary<string, object> GetMemo()
    {
        var memo = new Dictionary<string, object> {
            { Constants.TenantIdKey, AgentContext.TenantId },
            { Constants.AgentKey, AgentContext.AgentName },
            { Constants.UserIdKey, AgentContext.UserId },
        };

        return memo;
    }

}
