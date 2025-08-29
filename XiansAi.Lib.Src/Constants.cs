static class Constants {
    public const string TenantIdKey = "tenantId";
    public const string UserIdKey = "userId";
    public const string AgentKey = "agent";
    public const string QueueNameKey = "queueName";

    public const string UPDATE_INBOUND_CHAT_OR_DATA = "HandleInboundChatOrDataSync";
    
    // ActivityHistory size limits (in bytes)
    public const int MaxActivityInputSize = 10 * 1024; // 10KB
    public const int MaxActivityResultSize = 10 * 1024; // 10KB
    
    // Environment variable names for logging configuration
    public const string ConsoleLogLevelEnvVar = "CONSOLE_LOG_LEVEL";
    public const string ApiLogLevelEnvVar = "API_LOG_LEVEL";
}