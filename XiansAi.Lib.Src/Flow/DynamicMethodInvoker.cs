using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using XiansAi.Messaging;
using XiansAi.Logging;

namespace XiansAi.Flow;

public class DynamicMethodInvokerLogger{}

/// <summary>
/// Provides robust dynamic method invocation with JSON parameter handling
/// </summary>
public static class DynamicMethodInvoker
{
    private static readonly Logger<DynamicMethodInvokerLogger> _logger = Logger<DynamicMethodInvokerLogger>.For();
    
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        // Security: Limit JSON depth to prevent deeply nested attacks
        MaxDepth = 32
    };

    /// <summary>
    /// Invokes a method dynamically with proper JSON parameter handling
    /// </summary>
    /// <param name="targetType">The type containing the method to invoke</param>
    /// <param name="messageThread">MessageThread to pass to constructor</param>
    /// <param name="methodName">Name of the method to invoke</param>
    /// <param name="parametersJson">JSON string containing method parameters</param>
    /// <returns>Result of the method invocation</returns>
    public static async Task<object?> InvokeMethodAsync(
        Type targetType, 
        object[] constructorArgs, 
        string methodName, 
        string? parametersJson)
    {
        try
        {
            _logger.LogDebug($"Invoking method {methodName} on type {targetType.Name} with parameters: {parametersJson}");

            // Create instance with MessageThread constructor parameter
            var instance = CreateInstance(targetType, constructorArgs);
            
            // Get method and parameters
            var (method, parameters) = PrepareMethodInvocation(targetType, methodName, parametersJson);
            
            // Invoke the method
            var result = method.Invoke(instance, parameters);
            
            // Handle async methods
            if (result is Task task)
            {
                await task;
                
                _logger.LogDebug($"Task completed. Task type: {task.GetType()}");
                
                // Check if this is a Task<T> by looking for Result property
                var resultProperty = task.GetType().GetProperty("Result");
                if (resultProperty != null && resultProperty.PropertyType != typeof(void))
                {
                    try
                    {
                        var actualResult = resultProperty.GetValue(task);
                        _logger.LogDebug($"Async method result type: {actualResult?.GetType()}, value: {actualResult}");
                        return actualResult;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error extracting result from Task: {ex.Message}", ex);
                        throw;
                    }
                }
                
                _logger.LogDebug("Async method completed (void)");
                return null; // Task (void)
            }
            
            _logger.LogDebug($"Sync method result: {result}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to invoke method {methodName} on type {targetType.Name}", ex);
            throw new MethodInvocationException(
                $"Failed to invoke method '{methodName}' on type '{targetType.Name}': {ex.Message}", ex);
        }
    }

    private static object CreateInstance(Type targetType, object[] constructorArgs)
    {
        try
        {
            return TypeActivator.CreateWithOptionalArgs(targetType, constructorArgs);
        }
        catch (Exception ex)
        {
            throw new InstanceCreationException(
                $"Failed to create instance of type '{targetType.Name}': {ex.Message}", ex);
        }
    }

    private static (MethodInfo method, object?[] parameters) PrepareMethodInvocation(
        Type targetType, 
        string methodName, 
        string? parametersJson)
    {
        var methods = targetType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (methods.Length == 0)
        {
            throw new MethodNotFoundException($"Method '{methodName}' not found in type '{targetType.Name}'");
        }

        // Parse JSON parameters
        var jsonParameters = ParseJsonParameters(parametersJson);
        
        // Find best matching method
        var (method, parameters) = FindBestMethodMatch(methods, jsonParameters);
        
        return (method, parameters);
    }

    private static JsonElement[] ParseJsonParameters(string? parametersJson)
    {
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return Array.Empty<JsonElement>();
        }

        // Security: Validate JSON size to prevent DoS
        const int MaxJsonSize = 1 * 1024 * 1024; // 1 MB
        if (parametersJson.Length > MaxJsonSize)
        {
            throw new ParameterParsingException($"Parameters JSON size {parametersJson.Length} exceeds maximum allowed size of {MaxJsonSize} bytes");
        }

        try
        {
            var jsonOptions = new JsonDocumentOptions
            {
                MaxDepth = 32 // Security: Limit depth
            };
            var jsonDocument = JsonDocument.Parse(parametersJson, jsonOptions);
            var root = jsonDocument.RootElement;

            // Handle array of parameters
            if (root.ValueKind == JsonValueKind.Array)
            {
                return root.EnumerateArray().ToArray();
            }

            // Handle single parameter
            if (root.ValueKind == JsonValueKind.Object || 
                root.ValueKind == JsonValueKind.String ||
                root.ValueKind == JsonValueKind.Number ||
                root.ValueKind == JsonValueKind.True ||
                root.ValueKind == JsonValueKind.False)
            {
                return new[] { root };
            }

            return Array.Empty<JsonElement>();
        }
        catch (JsonException ex)
        {
            // If JSON parsing fails, treat the entire string as a single string parameter
            // This handles cases like simple strings that aren't wrapped in quotes
            try
            {
                var wrappedJson = JsonSerializer.Serialize(parametersJson);
                var jsonDocument = JsonDocument.Parse(wrappedJson);
                return new[] { jsonDocument.RootElement };
            }
            catch (Exception)
            {
                throw new ParameterParsingException($"Invalid JSON parameters: {ex.Message}", ex);
            }
        }
    }

    private static (MethodInfo method, object?[] parameters) FindBestMethodMatch(
        MethodInfo[] methods, 
        JsonElement[] jsonParameters)
    {
        foreach (var method in methods.OrderBy(m => m.GetParameters().Length))
        {
            var methodParams = method.GetParameters();
            
            if (TryConvertParameters(jsonParameters, methodParams, out var convertedParams))
            {
                return (method, convertedParams);
            }
        }

        var parameterCount = jsonParameters.Length;
        var availableMethods = string.Join(", ", methods.Select(m => 
            $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})"));
            
        throw new MethodMatchException(
            $"No method overload found that matches {parameterCount} parameter(s). Available methods: {availableMethods}");
    }

    private static bool TryConvertParameters(
        JsonElement[] jsonParameters, 
        ParameterInfo[] methodParameters, 
        out object?[] convertedParameters)
    {
        convertedParameters = new object?[methodParameters.Length];

        try
        {
            for (int i = 0; i < methodParameters.Length; i++)
            {
                var paramType = methodParameters[i].ParameterType;
                
                // Handle optional parameters
                if (i >= jsonParameters.Length)
                {
                    if (methodParameters[i].HasDefaultValue)
                    {
                        convertedParameters[i] = methodParameters[i].DefaultValue;
                        continue;
                    }
                    return false; // Required parameter missing
                }

                // Convert JSON element to target type
                convertedParameters[i] = ConvertJsonElementToType(jsonParameters[i], paramType);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static object? ConvertJsonElementToType(JsonElement jsonElement, Type targetType)
    {
        // Handle null values
        if (jsonElement.ValueKind == JsonValueKind.Null)
        {
            return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null 
                ? throw new DynamicInvokerException($"Cannot assign null to non-nullable type {targetType.Name}")
                : null;
        }

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            // String conversion
            if (underlyingType == typeof(string))
            {
                return jsonElement.GetString();
            }

            // Numeric conversions
            if (underlyingType == typeof(int))
                return jsonElement.GetInt32();
            if (underlyingType == typeof(long))
                return jsonElement.GetInt64();
            if (underlyingType == typeof(double))
                return jsonElement.GetDouble();
            if (underlyingType == typeof(decimal))
                return jsonElement.GetDecimal();
            if (underlyingType == typeof(float))
                return jsonElement.GetSingle();
            if (underlyingType == typeof(bool))
                return jsonElement.GetBoolean();

            // DateTime conversion
            if (underlyingType == typeof(DateTime))
                return jsonElement.GetDateTime();
            if (underlyingType == typeof(DateTimeOffset))
                return jsonElement.GetDateTimeOffset();

            // Guid conversion
            if (underlyingType == typeof(Guid))
                return jsonElement.GetGuid();

            // Enum conversion
            if (underlyingType.IsEnum)
            {
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    return Enum.Parse(underlyingType, jsonElement.GetString()!, true);
                }
                if (jsonElement.ValueKind == JsonValueKind.Number)
                {
                    return Enum.ToObject(underlyingType, jsonElement.GetInt32());
                }
            }

            // Complex object deserialization
            if (jsonElement.ValueKind == JsonValueKind.Object)
            {
                var json = jsonElement.GetRawText();
                return JsonSerializer.Deserialize(json, targetType, _jsonOptions);
            }

            // Array/List conversion
            if (jsonElement.ValueKind == JsonValueKind.Array)
            {
                var json = jsonElement.GetRawText();
                return JsonSerializer.Deserialize(json, targetType, _jsonOptions);
            }

            throw new DynamicInvokerException($"Conversion from JsonElement to {targetType.Name} is not supported");
        }
        catch (Exception ex)
        {
            throw new TypeConversionException(
                $"Failed to convert JSON value to {targetType.Name}: {ex.Message}", ex);
        }
    }
}

public class DynamicInvokerException : Exception
{
    public DynamicInvokerException(string message) : base(message) { }
    public DynamicInvokerException(string message, Exception innerException) : base(message, innerException) { }
}

// Custom exceptions for better error handling
public class MethodInvocationException : DynamicInvokerException
{
    public MethodInvocationException(string message) : base(message) { }
    public MethodInvocationException(string message, Exception innerException) : base(message, innerException) { }
}

public class InstanceCreationException : DynamicInvokerException
{
    public InstanceCreationException(string message) : base(message) { }
    public InstanceCreationException(string message, Exception innerException) : base(message, innerException) { }
}

public class MethodNotFoundException : DynamicInvokerException
{
    public MethodNotFoundException(string message) : base(message) { }
}

public class ParameterParsingException : DynamicInvokerException
{
    public ParameterParsingException(string message) : base(message) { }
    public ParameterParsingException(string message, Exception innerException) : base(message, innerException) { }
}

public class MethodMatchException : DynamicInvokerException
{
    public MethodMatchException(string message) : base(message) { }
}

public class TypeConversionException : DynamicInvokerException
{
    public TypeConversionException(string message) : base(message) { }
    public TypeConversionException(string message, Exception innerException) : base(message, innerException) { }
}