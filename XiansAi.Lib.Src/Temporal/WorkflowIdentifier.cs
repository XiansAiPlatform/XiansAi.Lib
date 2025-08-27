using System.Reflection;
using Temporalio.Workflows;

namespace Temporal;

public class WorkflowIdentifier
{
    public string WorkflowId { get; set; }
    public string WorkflowType { get; set; }
    public string AgentName { get; set; }

    /// <summary>
    /// WorkflowIdentifier is used to identify a workflow by its id or type.
    /// 
    /// WORKFLOW ID FORMAT: Tenant Id:Agent Name:Flow Name:Id Postfix
    /// EXAMPLE WORKFLOW ID: default:My Agent v1.3.1:Router Bot:ebbb57bd-8428-458f-9618-d8fe3bef103c
    /// 
    /// WORKFLOW TYPE FORMAT: Agent Name:Flow Name
    /// EXAMPLE WORKFLOW TYPE: My Agent v1.3.1:Router Bot
    /// 
    /// </summary>
    /// <param name="identifier">Either workflowId or workflowType</param>
    /// <exception cref="Exception"></exception>
    public WorkflowIdentifier(string identifier, string? tenantId = null)
    {
        if (tenantId == null) {
            tenantId = AgentContext.TenantId;
        }
        // if identifier has 2 ":" then we got workflowId
        if (identifier.Count(c => c == ':') >= 2)
        {
            if (!identifier.StartsWith(tenantId + ":"))
            {
                throw new Exception($"Invalid workflow identifier `{identifier}`. Expected to start with tenant id `{tenantId}`");
            }
            // we got workflowId
            WorkflowId = identifier;
            WorkflowType = GetWorkflowType(WorkflowId);
            AgentName = GetAgentName(WorkflowType);
        }
        else
        {
            // we got workflowType
            WorkflowType = identifier;
            AgentName = GetAgentName(WorkflowType);
            WorkflowId = GetWorkflowId(WorkflowType, tenantId);
        }
    }

    public static string GetWorkflowId(string workflowType, string? tenantId = null)
    {
        if (tenantId == null) {
            tenantId = AgentContext.TenantId;
        }
        return tenantId + ":" + workflowType;
    }

    public static string GetWorkflowType(string workflow)
    {
        // if workflow has 1 ":" then we got workflowType
        if (workflow.Count(c => c == ':') == 1) {
            return workflow;
        } 
        else if (workflow.Count(c => c == ':') >= 2) 
        {
            var parts = workflow.Split(":");
            return parts[1] + ":" + parts[2];
        }
        else {
            throw new Exception($"Invalid workflow identifier `{workflow}`. Expected to have at least 1 `:`");
        }
    }

    public static string GetAgentName(string workflow)
    {
        if (workflow.Count(c => c == ':') == 1) {
            return workflow.Split(":")[0];
        }
        else if (workflow.Count(c => c == ':') >= 2) 
        {
            return workflow.Split(":")[0];
        }
        else {
            throw new Exception($"Invalid workflow identifier `{workflow}`. Expected to have at least 1 `:`");
        }
    }


    public static string GetSingletonWorkflowIdFor(Type flowClassType)
    {
        var workflowId = $"{AgentContext.TenantId}:{GetWorkflowTypeFor(flowClassType)}";
        return workflowId;
    }

    public static string GetWorkflowIdFor(Type flowClassType, string? idPostfix = null)
    {
        var workflowId = $"{AgentContext.TenantId}:{GetWorkflowTypeFor(flowClassType)}:{idPostfix}";
        return workflowId;
    }

    public static string GetWorkflowTypeFor(Type flowClassType)
    {
        var workflowAttr = flowClassType.GetCustomAttribute<WorkflowAttribute>();
        return workflowAttr?.Name ?? throw new InvalidOperationException("WorkflowAttribute.Name is not set");
    }
}