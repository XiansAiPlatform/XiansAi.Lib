namespace Xians.Lib.Common.Exceptions;

/// <summary>
/// Exception thrown when a tenant isolation violation is detected.
/// </summary>
public class TenantIsolationException : XiansException
{
    public string? ExpectedTenantId { get; }
    public string? ActualTenantId { get; }

    public TenantIsolationException() : base() { }

    public TenantIsolationException(string message) : base(message) { }

    public TenantIsolationException(string message, Exception innerException) 
        : base(message, innerException) { }

    public TenantIsolationException(string message, string? expectedTenantId, string? actualTenantId)
        : base(message)
    {
        ExpectedTenantId = expectedTenantId;
        ActualTenantId = actualTenantId;
    }
}

