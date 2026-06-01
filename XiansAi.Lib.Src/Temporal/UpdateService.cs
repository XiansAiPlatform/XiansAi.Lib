using System.Text.Json;
using Temporalio.Client;
using Temporalio.Workflows;

namespace Temporal;


public class UpdateService
{
    // Perf: hoisted to static readonly so STJ's per-options metadata cache survives across calls.
    // Previously a fresh JsonSerializerOptions was allocated per ConvertResult invocation.
    private static readonly JsonSerializerOptions _convertResultJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = 32
    };

    public static async Task<TResult?> SendUpdateWithStart<TResult>(Type workflowType, string update, params object?[] args) {
        var workflow = WorkflowIdentifier.GetWorkflowTypeFor(workflowType);
        return await SendUpdateWithStart<TResult>(workflow, update, args);
    }

    public static async Task<TResult?> SendUpdateWithStart<TResult>(string workflow, string update, params object?[] args) {

        object? result = null;
        if (Workflow.InWorkflow) {
            result = await Workflow.ExecuteActivityAsync(
                (SystemActivities a) => a.SendUpdateWithStart(workflow, update, args), 
                new SystemActivityOptions());
        } else {
            result = await UpdateServiceImpl.SendUpdateWithStart(workflow, update, args);
        }

        return ConvertResult<TResult>(result);
    }

    private static TResult? ConvertResult<TResult>(object? result)
    {
        if (result == null)
            return default(TResult);

        // If the result is already the correct type, return it
        if (result is TResult directResult)
            return directResult;

        // Handle JsonElement conversion (common with Temporal activities)
        if (result is JsonElement jsonElement)
        {
            // For primitive types, get the value directly
            if (typeof(TResult) == typeof(string))
                return (TResult)(object)jsonElement.GetString()!;
            
            if (typeof(TResult) == typeof(int) || typeof(TResult) == typeof(int?))
                return (TResult)(object)jsonElement.GetInt32();
                
            if (typeof(TResult) == typeof(bool) || typeof(TResult) == typeof(bool?))
                return (TResult)(object)jsonElement.GetBoolean();
                
            if (typeof(TResult) == typeof(double) || typeof(TResult) == typeof(double?))
                return (TResult)(object)jsonElement.GetDouble();
                
            if (typeof(TResult) == typeof(Guid) || typeof(TResult) == typeof(Guid?))
                return (TResult)(object)jsonElement.GetGuid();

            // For complex types, deserialize from JSON
            return JsonSerializer.Deserialize<TResult>(jsonElement.GetRawText(), _convertResultJsonOptions);
        }

        // If it's a string representation, try to deserialize it
        if (result is string jsonString && typeof(TResult) != typeof(string))
        {
            try
            {
                // Security: Validate string size
                const int MaxJsonSize = 5 * 1024 * 1024; // 5 MB
                if (jsonString.Length > MaxJsonSize)
                {
                    throw new InvalidOperationException($"Result JSON size {jsonString.Length} exceeds maximum allowed size");
                }

                return JsonSerializer.Deserialize<TResult>(jsonString, _convertResultJsonOptions);
            }
            catch (JsonException)
            {
                // If JSON deserialization fails, fall back to direct conversion
            }
        }

        // Try direct conversion as last resort
        try
        {
            return (TResult)Convert.ChangeType(result, typeof(TResult))!;
        }
        catch (InvalidCastException)
        {
            throw new InvalidCastException(
                $"Unable to convert result of type '{result.GetType().Name}' to '{typeof(TResult).Name}'. " +
                $"Result value: {result}");
        }
    }
}

internal class UpdateServiceImpl
{
    public static async Task<object?> SendUpdateWithStart(string workflow, string update, params object?[] args) {
        ITemporalClient client = await TemporalClientService.Instance.GetClientAsync();
        
        var options = new NewWorkflowOptions(workflow);
        // Create the start operation
        var startOperation = WithStartWorkflowOperation.Create(
            workflow,
            [],
            options);
        
        var updateOptions = new WorkflowUpdateWithStartOptions {
            StartWorkflowOperation = startOperation,
        };

        return await client.ExecuteUpdateWithStartWorkflowAsync<object>(update, args, updateOptions);
    }
}