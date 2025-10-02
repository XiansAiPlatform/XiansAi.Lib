
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Common;

namespace Temporal;

public class NewWorkflowOptions : WorkflowOptions
{
    private readonly string? _agentName;
    private readonly bool _systemScoped;
    public NewWorkflowOptions(string workflowType, string? idPostfix = null, string? agentName = null)
    {
        _agentName = agentName ?? WorkflowIdentifier.GetAgentName(workflowType);
        _systemScoped = AgentContext.SystemScoped;
        Id = AgentContext.TenantId + ":" + workflowType + (idPostfix != null ? ":" + idPostfix : "");

        if (_systemScoped) {
            TaskQueue = workflowType;
        } else {
            TaskQueue = AgentContext.TenantId + ":" + workflowType;
        }
        Memo = GetMemo();
        TypedSearchAttributes = GetSearchAttributes();
        IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting;

    }

    public SearchAttributeCollection GetSearchAttributes()
    {
        var searchAttributesBuilder = new SearchAttributeCollection.Builder()
                    .Set(SearchAttributeKey.CreateKeyword(Constants.TenantIdKey), AgentContext.TenantId)
                    .Set(SearchAttributeKey.CreateKeyword(Constants.AgentKey), _agentName ?? AgentContext.AgentName)
                    .Set(SearchAttributeKey.CreateKeyword(Constants.UserIdKey), AgentContext.UserId);

        return searchAttributesBuilder.ToSearchAttributeCollection();
    }

    public Dictionary<string, object> GetMemo()
    {

        var memo = new Dictionary<string, object> {
            { Constants.TenantIdKey, AgentContext.TenantId },
            { Constants.AgentKey, _agentName ?? AgentContext.AgentName },
            { Constants.UserIdKey, AgentContext.UserId },
            { Constants.SystemScopedKey, _systemScoped },
        };

        return memo;
    }

}
