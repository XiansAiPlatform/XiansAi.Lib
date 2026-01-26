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
        public const string idPostfix = "idPostfix";
        public const string SystemScoped = "systemScoped";
        
        // Task workflow specific keys
        public const string TaskTitle = "taskTitle";
        public const string TaskDescription = "taskDescription";
        public const string TaskActions = "taskActions";

    }

    /// <summary>
    /// Helper methods for generating workflow type names
    /// </summary>
    public static class WorkflowTypes
    {
        /// <summary>
        /// Supervisor workflow name
        /// </summary>
        public const string Supervisor = "Supervisor Workflow";
        
        /// <summary>
        /// Integrator workflow name
        /// </summary>
        public const string Integrator = "Integrator Workflow";
        
        /// <summary>
        /// Generates the task workflow type name for a specific agent.
        /// Format: {AgentName}:Task Workflow
        /// </summary>
        /// <param name="agentName">The agent name</param>
        /// <returns>The task workflow type name</returns>
        public static string GetTaskWorkflowType(string agentName)
        {
            if (string.IsNullOrWhiteSpace(agentName))
            {
                throw new ArgumentException("Agent name cannot be null or empty.", nameof(agentName));
            }
            return $"{agentName}:Task Workflow";
        }
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
        public const string KnowledgeLatestSystem = "api/agent/knowledge/latest/system";
        public const string KnowledgeList = "api/agent/knowledge/list";
        public const string ConversationHistory = "api/agent/conversation/history";
        public const string ConversationOutbound = "api/agent/conversation/outbound";
        public const string ConversationLastHint = "api/agent/conversation/last-hint";
        public const string Logs = "api/agent/logs";
    }

    /// <summary>
    /// Environment variable names
    /// </summary>
    public static class EnvironmentVariables
    {
        /// <summary>
        /// Controls the minimum log level for console output.
        /// Valid values: TRACE, DEBUG, INFORMATION, INFO, WARNING, WARN, ERROR, CRITICAL
        /// Default: DEBUG
        /// </summary>
        public const string ConsoleLogLevel = "CONSOLE_LOG_LEVEL";
        
        /// <summary>
        /// Controls the minimum log level for server logging (logs sent to server).
        /// Valid values: TRACE, DEBUG, INFORMATION, INFO, WARNING, WARN, ERROR, CRITICAL
        /// Default: ERROR
        /// </summary>
        public const string ServerLogLevel = "SERVER_LOG_LEVEL";
        
        /// <summary>
        /// Legacy environment variable name for server logging (deprecated, use SERVER_LOG_LEVEL).
        /// Maintained for backward compatibility.
        /// </summary>
        public const string ApiLogLevel = "API_LOG_LEVEL";
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

