namespace Xians.Lib.Tests.TestUtilities;

/// <summary>
/// Standard metric types for testing and examples.
/// Helper constants to make tests and examples more readable.
/// </summary>
public static class MetricTypes
{
    // Token metrics
    public const string PromptTokens = "prompt_tokens";
    public const string CompletionTokens = "completion_tokens";
    public const string TotalTokens = "total_tokens";
    
    // Activity metrics
    public const string MessageCount = "message_count";
    public const string WorkflowCompleted = "workflow_completed";
    public const string EmailSent = "email_sent";
    
    // Performance metrics
    public const string ResponseTimeMs = "response_time_ms";
    public const string ProcessingTimeMs = "processing_time_ms";
    
    // LLM usage metrics
    public const string LlmCalls = "llm_calls";
    public const string CacheHits = "cache_hits";
    public const string CacheMisses = "cache_misses";
}
