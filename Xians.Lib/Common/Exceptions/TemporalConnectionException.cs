namespace Xians.Lib.Common.Exceptions;

/// <summary>
/// Exception thrown when Temporal connection operations fail.
/// </summary>
public class TemporalConnectionException : XiansException
{
    public string? ServerUrl { get; }
    public string? Namespace { get; }

    public TemporalConnectionException() : base() { }

    public TemporalConnectionException(string message) : base(message) { }

    public TemporalConnectionException(string message, Exception innerException) 
        : base(message, innerException) { }

    public TemporalConnectionException(string message, string? serverUrl, string? @namespace)
        : base(message)
    {
        ServerUrl = serverUrl;
        Namespace = @namespace;
    }

    public TemporalConnectionException(string message, string? serverUrl, string? @namespace, Exception innerException)
        : base(message, innerException)
    {
        ServerUrl = serverUrl;
        Namespace = @namespace;
    }
}

