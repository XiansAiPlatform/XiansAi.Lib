using System.Text.Json;
using Temporalio.Client;
using Temporalio.Workflows;

namespace Temporal;


public class UpdateService 
{
    public static async Task<TResult?> SendUpdateWithStart<TResult>(Type workflowType, string update, params object?[] args) {
        var workflow = AgentContext.GetWorkflowTypeFor(workflowType);
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
            return JsonSerializer.Deserialize<TResult>(jsonElement.GetRawText());
        }

        // If it's a string representation, try to deserialize it
        if (result is string jsonString && typeof(TResult) != typeof(string))
        {
            try
            {
                return JsonSerializer.Deserialize<TResult>(jsonString);
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