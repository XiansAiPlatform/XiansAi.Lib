namespace Xians.Lib.Common.Exceptions;

/// <summary>
/// Exception thrown when workflow operations fail.
/// </summary>
public class WorkflowException : XiansException
{
    public string? WorkflowType { get; }
    public string? WorkflowId { get; }

    public WorkflowException() : base() { }

    public WorkflowException(string message) : base(message) { }

    public WorkflowException(string message, Exception innerException) 
        : base(message, innerException) { }

    public WorkflowException(string message, string? workflowType, string? workflowId = null)
        : base(message)
    {
        WorkflowType = workflowType;
        WorkflowId = workflowId;
    }

    public WorkflowException(string message, string? workflowType, string? workflowId, Exception innerException)
        : base(message, innerException)
    {
        WorkflowType = workflowType;
        WorkflowId = workflowId;
    }
}

