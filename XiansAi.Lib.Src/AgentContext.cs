using Temporalio.Activities;
using Temporalio.Workflows;
using XiansAi.Models;
using XiansAi.Server;
using Temporal;
using XiansAi.Flow.Router;
using Server;

public class AgentContext
{
    private static CertificateInfo? _certificateInfo { get; set; }
    private static string? _userId { get; set; }
    private static string? _workflowId { get; set; }
    public static RouterOptions? RouterOptions { get; set; }
    public static ServerSettings? ServerSettings { get; set; }

    public static void SetLocalContext(string userId, string workflowId) {
        _userId = userId;
        _workflowId = workflowId;
    }

    public static async Task StartWorkflow<TWorkflow>(string namePostfix, object[] args) {
        await SubWorkflowService.Start<TWorkflow>(namePostfix, args);
    }

    public static async Task<TResult> ExecuteWorkflow<TWorkflow, TResult>(string namePostfix, object[] args) {
        return await SubWorkflowService.Execute<TWorkflow, TResult>(namePostfix, args);
    }

    private static CertificateInfo? CertificateInfo { 
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
            if (CertificateInfo?.TenantId  != null)
            {
                return CertificateInfo.TenantId ;
            } 
            else if (WorkflowId != null)
            {
                return WorkflowIdentifier.GetTenantId(WorkflowId);
            }
            else
            {
                throw new InvalidOperationException("Tenant ID is not set, certificate is missing tenant ID info");
            }
        }
    }


    public static string UserId {
        get
        {
            return 
                CertificateInfo?.UserId ??
                _userId ??
                throw new InvalidOperationException("User ID is not set, certificate is missing user ID info");
        }
    }

    public static string AgentName
    {
        get
        {
            return WorkflowIdentifier.GetAgentName(WorkflowId ?? WorkflowType);
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
            else if (_workflowId != null)
            {
                // WorkflowId should have 2 ":"
                if(_workflowId.Count(c => c == ':') >= 2)
                {
                    return _workflowId;
                }
                else
                {
                    throw new InvalidOperationException("Custom context workflow id should have the format `tenantId:AgentName:workflowName:optionalId`. But got `" + _workflowId + "`");
                }
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
            else if (WorkflowId != null)
            {
                return WorkflowIdentifier.GetWorkflowType(WorkflowId);
            }
            else
            {
                throw new InvalidOperationException("Not in workflow or activity");
            }
        }
    }

}