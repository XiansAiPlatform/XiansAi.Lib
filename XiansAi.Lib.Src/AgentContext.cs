using System.Reflection;
using Temporalio.Activities;
using Temporalio.Workflows;
using XiansAi.Models;
using XiansAi.Server;
using Temporal;

public class AgentContext
{

    private static string? _agent { get; set; }
    private static string? _tenantId { get; set; }
    private static CertificateInfo? _certificateInfo { get; set; }

    public static string GetSingletonWorkflowIdFor(Type flowClassType)
    {
        var workflowId = $"{TenantId}:{GetWorkflowTypeFor(flowClassType)}";
        return workflowId;
    }

    public static string GetWorkflowTypeFor(Type flowClassType)
    {
        var workflowAttr = flowClassType.GetCustomAttribute<WorkflowAttribute>();
        return workflowAttr?.Name ?? throw new InvalidOperationException("WorkflowAttribute.Name is not set");
    }

    public static async Task StartWorkflow<TWorkflow>(string namePostfix, object[] args) {
        await SubWorkflowService.Start<TWorkflow>(namePostfix, args);
    }

    public static async Task<TResult> ExecuteWorkflow<TWorkflow, TResult>(string namePostfix, object[] args) {
        return await SubWorkflowService.Execute<TWorkflow, TResult>(namePostfix, args);
    }

    public static CertificateInfo? CertificateInfo { 
        get
        {
            if (_certificateInfo != null)
            {
                return _certificateInfo;
            }
            _certificateInfo = new CertificateReader().ReadCertificate();
            return _certificateInfo;
        }
    }

    public static string TenantId {
        get
        {
            if (_tenantId != null)
            {
                return _tenantId;
            }
            return CertificateInfo?.TenantId ?? throw new InvalidOperationException("Tenant ID is not set, certificate is missing tenant ID info");
        }
    }


    public static string UserId {
        get
        {
            return CertificateInfo?.UserId ?? throw new InvalidOperationException("User ID is not set, certificate is missing user ID info");
        }
    }


    public static string AgentName
    {
        get
        {
            var workflowType = WorkflowType;
            var agentName = workflowType.Split(':')[0];
            return agentName;
        }
    }
    public static string WorkflowId { 
        get 
        {
            if (Workflow.InWorkflow)
            {
                return Workflow.Info.WorkflowId;
            }
            else if (ActivityExecutionContext.HasCurrent)
            {
                return ActivityExecutionContext.Current.Info.WorkflowId;
            }
            else
            {
                throw new InvalidOperationException("Not in workflow or activity");
            }
        }
    }
    public static string WorkflowType { 
        get 
        {
            if (Workflow.InWorkflow)
            {
                return Workflow.Info.WorkflowType;
            }
            else if (ActivityExecutionContext.HasCurrent)
            {
                return ActivityExecutionContext.Current.Info.WorkflowType;
            }
            else
            {
                throw new InvalidOperationException("Not in workflow or activity");
            }
        }
    }

    public static string WorkflowRunId { 
        get 
        {
            if (Workflow.InWorkflow)
            {
                return Workflow.Info.RunId;
            }
            else if (ActivityExecutionContext.HasCurrent)
            {
                return ActivityExecutionContext.Current.Info.WorkflowRunId;
            }
            else
            {
                throw new InvalidOperationException("Not in workflow or activity");
            }
        }
    }

}