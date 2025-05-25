using Temporalio.Converters;

namespace XiansAi.Temporal;

public class MemoUtil
{
    private readonly IReadOnlyDictionary<string, IRawValue> _memo;

    internal MemoUtil(IReadOnlyDictionary<string, IRawValue> memo)
    {
        _memo = memo;
    }

    public MemoUtil(IReadOnlyDictionary<string, IEncodedRawValue> memo)
    {
        _memo = memo.ToDictionary(kvp => kvp.Key, kvp => (IRawValue)kvp.Value);
    }

    public string GetAgent()
    {
        return ExtractMemoValue(_memo, Constants.AgentKey) ?? throw new Exception("Agent value not found in workflow memo");
    }
    public string? GetQueueName()
    {
        return ExtractMemoValue(_memo, Constants.QueueNameKey);
    }

    public string GetTenantId() {
        return ExtractMemoValue(_memo, Constants.TenantIdKey) ?? throw new Exception("TenantId value not found in workflow memo");
    }

    public string GetUserId() {
        return ExtractMemoValue(_memo, Constants.UserIdKey) ?? throw new Exception("UserId value not found in workflow memo");
    }

    private string? ExtractMemoValue(IReadOnlyDictionary<string, IRawValue> memo, string key)
    {
        if (memo.TryGetValue(key, out var memoValue))
        {
            return memoValue?.Payload?.Data?.ToStringUtf8()?.Replace("\"", "");
        }
        return null;
    }

    private string? ExtractMemoValue(IReadOnlyDictionary<string, IEncodedRawValue> memo, string key)
    {
        if (memo.TryGetValue(key, out var memoValue))
        {
            return memoValue?.Payload?.Data?.ToStringUtf8()?.Replace("\"", "");
        }
        return null;
    }
}