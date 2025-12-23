namespace Xians.Lib.Common.Exceptions;

/// <summary>
/// Base exception for all Xians library exceptions.
/// Provides a common foundation for domain-specific exceptions.
/// </summary>
public class XiansException : Exception
{
    public XiansException() : base() { }

    public XiansException(string message) : base(message) { }

    public XiansException(string message, Exception innerException) 
        : base(message, innerException) { }
}

