using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using XiansAi.Models;

namespace Server;

public class ActivityUploader
{
    private readonly ILogger _logger;

    public ActivityUploader()
    {
        _logger = Globals.LogFactory.CreateLogger<ActivityUploader>();
    }

    public async Task UploadActivity(ActivityHistory activityHistory)
    {
        if (SecureApi.IsReady)
        {
            var client = SecureApi.Instance.Client;
            var sanitizedActivity = SanitizeActivityHistory(activityHistory);

            var response = await client.PostAsync("api/agent/activity-history", JsonContent.Create(sanitizedActivity));
            response.EnsureSuccessStatusCode();
        }
        else
        {
            _logger.LogWarning("App server secure API is not ready, skipping activity upload to server");
        }
    }

    private ActivityHistory SanitizeActivityHistory(ActivityHistory original)
    {
        var sanitized = new ActivityHistory
        {
            Agent = original.Agent,
            ActivityId = original.ActivityId,
            ActivityName = original.ActivityName,
            StartedTime = original.StartedTime,
            EndedTime = original.EndedTime,
            WorkflowId = original.WorkflowId,
            WorkflowRunId = original.WorkflowRunId,
            WorkflowType = original.WorkflowType,
            TaskQueue = original.TaskQueue,
            AgentToolNames = original.AgentToolNames,
            InstructionIds = original.InstructionIds,
            Attempt = original.Attempt,
            WorkflowNamespace = original.WorkflowNamespace,
            Inputs = SanitizeInputs(original.Inputs),
            Result = SanitizeResult(original.Result)
        };

        return sanitized;
    }

    private Dictionary<string, object?> SanitizeInputs(Dictionary<string, object?> inputs)
    {
        var sanitizedInputs = new Dictionary<string, object?>();
        
        foreach (var kvp in inputs)
        {
            sanitizedInputs[kvp.Key] = TruncateIfTooLarge(kvp.Value, Constants.MaxActivityInputSize);
        }
        
        return sanitizedInputs;
    }

    private object? SanitizeResult(object? result)
    {
        return TruncateIfTooLarge(result, Constants.MaxActivityResultSize);
    }

    private object? TruncateIfTooLarge(object? value, int maxSizeBytes)
    {
        if (value == null) return null;

        try
        {
            var jsonString = JsonSerializer.Serialize(value);
            var sizeInBytes = System.Text.Encoding.UTF8.GetByteCount(jsonString);
            
            if (sizeInBytes <= maxSizeBytes)
            {
                return value;
            }

            _logger.LogWarning("Activity data too large ({SizeInBytes} bytes), truncating to {MaxSize} bytes", 
                sizeInBytes, maxSizeBytes);

            // Truncate the JSON string to fit within the size limit
            var maxStringLength = maxSizeBytes / 3; // Conservative estimate for UTF-8 encoding
            var truncatedString = jsonString.Length > maxStringLength 
                ? jsonString.Substring(0, maxStringLength) + "...[TRUNCATED]"
                : jsonString;

            return $"[LARGE_DATA_TRUNCATED] Original size: {sizeInBytes} bytes. Data: {truncatedString}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking activity data size, keeping original value");
            return value;
        }
    }
}
