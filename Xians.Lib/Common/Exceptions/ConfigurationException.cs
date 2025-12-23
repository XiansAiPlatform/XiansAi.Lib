namespace Xians.Lib.Common.Exceptions;

/// <summary>
/// Exception thrown when there are configuration errors (invalid, missing, or malformed configuration).
/// </summary>
public class ConfigurationException : XiansException
{
    public string? ConfigurationKey { get; }

    public ConfigurationException() : base() { }

    public ConfigurationException(string message) : base(message) { }

    public ConfigurationException(string message, Exception innerException) 
        : base(message, innerException) { }

    public ConfigurationException(string message, string configurationKey)
        : base(message)
    {
        ConfigurationKey = configurationKey;
    }

    public ConfigurationException(string message, string configurationKey, Exception innerException)
        : base(message, innerException)
    {
        ConfigurationKey = configurationKey;
    }
}

