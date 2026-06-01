using System.Collections.Concurrent;
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

    public static string GetTenantId(string workflowId)
    {
        if (workflowId.Count(c => c == ':') >= 2) {
            return workflowId.Split(":")[0];
        }
        else {
            throw new Exception($"Invalid workflow identifier `{workflowId}`. Expected to have at least 2 `:`");
        }
    }

    public static string GetAgentName(string workflow)
    {
        if (workflow.Count(c => c == ':') == 1) {
            return workflow.Split(":")[0];
        }
        else if (workflow.Count(c => c == ':') >= 2) 
        {
            return workflow.Split(":")[1];
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
        if(String.IsNullOrEmpty(idPostfix))
        {
            return $"{AgentContext.TenantId}:{GetWorkflowTypeFor(flowClassType)}";
        } else
        {
            return $"{AgentContext.TenantId}:{GetWorkflowTypeFor(flowClassType)}:{idPostfix}";
        }
    }

    public static string GetWorkflowTypeFor(Type flowClassType)
    {
        var workflowAttr = flowClassType.GetCustomAttribute<WorkflowAttribute>();
        return workflowAttr?.Name ?? throw new InvalidOperationException("WorkflowAttribute.Name is not set");
    }

    // Perf (issue #98): GetClassTypeFor is called on every cross-agent message via
    // Agent2Agent.BotToBotMessage. Walking every assembly + every type per call is
    // O(types-in-process) reflection on the hot path. Cache the resolution.
    private static readonly ConcurrentDictionary<string, Type?> _classTypeCache = new();

    public static Type? GetClassTypeFor(string workflowType)
    {
        return _classTypeCache.GetOrAdd(workflowType, static name =>
        {
            // Find all types in the current app domain that have WorkflowAttribute
            // and whose attribute name matches. Reflection cost is paid once per name.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Fall back to the types that did load successfully.
                    types = ex.Types.Where(t => t is not null).ToArray()!;
                }

                foreach (var type in types)
                {
                    var workflowAttr = type.GetCustomAttribute<WorkflowAttribute>();
                    if (workflowAttr != null && workflowAttr.Name == name)
                    {
                        return type;
                    }
                }
            }

            return null;
        });
    }
}