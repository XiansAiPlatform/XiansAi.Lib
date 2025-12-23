namespace Xians.Lib.Common;

/// <summary>
/// Utility for reading and validating environment variables.
/// </summary>
public static class EnvironmentVariableReader
{
    /// <summary>
    /// Gets a required environment variable or throws an exception.
    /// </summary>
    public static string GetRequired(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{variableName} environment variable is required");

        return value;
    }

    /// <summary>
    /// Gets an optional environment variable.
    /// </summary>
    public static string? GetOptional(string variableName)
    {
        return Environment.GetEnvironmentVariable(variableName);
    }

    /// <summary>
    /// Gets multiple required environment variables.
    /// </summary>
    public static Dictionary<string, string> GetRequired(params string[] variableNames)
    {
        var result = new Dictionary<string, string>();
        
        foreach (var name in variableNames)
        {
            result[name] = GetRequired(name);
        }
        
        return result;
    }
}

