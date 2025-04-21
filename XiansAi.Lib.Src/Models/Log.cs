namespace XiansAi.Models
{
    public class Log
    {
        public string? Id { get; set; }
        public required string TenantId { get; set; }
        public DateTime CreatedAt { get; set; }
        public required LogLevel Level { get; set; }
        public required string Message { get; set; }
        public required string WorkflowId { get; set; }
        public required string WorkflowRunId { get; set; }
        public Dictionary<string, object>? Properties { get; set; }
        public string? Exception { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public enum LogLevel
    {
        Information,
        Warning,
        Error,
        Debug,
        Critical
    }
}
