namespace Xians.Lib.Common.Exceptions;

/// <summary>
/// Exception thrown when workflow registration fails.
/// </summary>
public class WorkflowRegistrationException : XiansException
{
    /// <summary>
    /// Gets the workflow type that failed to register.
    /// </summary>
    public string? WorkflowType { get; }

    /// <summary>
    /// Gets the agent name associated with the workflow.
    /// </summary>
    public string? AgentName { get; }

    public WorkflowRegistrationException(string message) : base(message) { }

    public WorkflowRegistrationException(string message, Exception innerException) 
        : base(message, innerException) { }

    public WorkflowRegistrationException(
        string message,
        string? workflowType = null,
        string? agentName = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        WorkflowType = workflowType;
        AgentName = agentName;
    }
}
