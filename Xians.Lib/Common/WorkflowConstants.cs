namespace Xians.Lib.Common;

/// <summary>
/// Constants for workflow memo keys and search attributes.
/// Used across workflows, schedules, and child workflow options.
/// </summary>
public static class WorkflowConstants
{
    /// <summary>
    /// Memo and search attribute keys
    /// </summary>
    public static class Keys
    {
        // Standard workflow keys
        public const string TenantId = "tenantId";
        public const string Agent = "agent";
        public const string UserId = "userId";
        public const string SystemScoped = "systemScoped";
        
        // Task workflow specific keys
        public const string TaskTitle = "taskTitle";
        public const string TaskDescription = "taskDescription";

        // Builtin workflow specific keys
        public const string BuiltinWorkflowType = "Platform:Builtin Workflow";
        public const string TaskWorkflowType = "Platform:Task Workflow";
    }

    /// <summary>
    /// HTTP header names
    /// </summary>
    public static class Headers
    {
        public const string TenantId = "X-Tenant-Id";
        public const string Authorization = "Authorization";
    }

    /// <summary>
    /// API endpoint paths
    /// </summary>
    public static class ApiEndpoints
    {
        public const string AgentDefinitions = "/api/agent/definitions";
        public const string FlowServerSettings = "/api/agent/settings/flowserver";
        public const string Documents = "api/agent/documents";
        public const string Knowledge = "api/agent/knowledge";
        public const string KnowledgeLatest = "api/agent/knowledge/latest";
        public const string KnowledgeList = "api/agent/knowledge/list";
        public const string ConversationHistory = "api/agent/conversation/history";
        public const string ConversationOutbound = "api/agent/conversation/outbound";
        public const string ConversationLastHint = "api/agent/conversation/last-hint";
    }

    /// <summary>
    /// Common error messages
    /// </summary>
    public static class ErrorMessages
    {
        public const string WorkflowTypeNullOrEmpty = "Workflow type cannot be null or empty.";
        public const string TenantIdNullOrEmpty = "Tenant ID cannot be null or empty.";
        public const string WorkflowIdNullOrEmpty = "WorkflowId cannot be null or empty.";
    }
}

