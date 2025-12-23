namespace Xians.Lib.Common.Exceptions;

/// <summary>
/// Exception thrown when certificate operations fail (parsing, validation, or authentication).
/// </summary>
public class CertificateException : XiansException
{
    public CertificateException() : base() { }

    public CertificateException(string message) : base(message) { }

    public CertificateException(string message, Exception innerException) 
        : base(message, innerException) { }
}

