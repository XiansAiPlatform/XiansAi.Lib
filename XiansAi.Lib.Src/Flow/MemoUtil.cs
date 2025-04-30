using Temporalio.Converters;

public class MemoUtil
{
    private readonly IReadOnlyDictionary<string, IRawValue> _memo;

    public MemoUtil(IReadOnlyDictionary<string, IRawValue> memo)
    {
        _memo = memo;
    }

    public string GetAgent()
    {
        return ExtractMemoValue(_memo, Constants.AgentKey) ?? throw new Exception("Agent value not found in workflow memo");
    }

    public string? GetAssignment()
    {
        return ExtractMemoValue(_memo, Constants.AssignmentKey);
    }

    public string? GetQueueName()
    {
        return ExtractMemoValue(_memo, Constants.QueueNameKey);
    }

    public string GetTenantId() {
        return ExtractMemoValue(_memo, Constants.TenantIdKey) ?? throw new Exception("TenantId value not found in workflow memo");
    }

    public string? GetUserId() {
        return ExtractMemoValue(_memo, Constants.UserIdKey);
    }

    private string? ExtractMemoValue(IReadOnlyDictionary<string, IRawValue> memo, string key)
    {
        if (memo.TryGetValue(key, out var memoValue))
        {
            return memoValue?.Payload?.Data?.ToStringUtf8()?.Replace("\"", "");
        }
        return null;
    }
}