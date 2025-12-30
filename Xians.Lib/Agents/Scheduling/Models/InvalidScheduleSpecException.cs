namespace Xians.Lib.Agents.Scheduling.Models;

/// <summary>
/// Exception thrown when a schedule specification is invalid.
/// </summary>
public class InvalidScheduleSpecException : Exception
{
    public InvalidScheduleSpecException(string message)
        : base(message)
    {
    }

    public InvalidScheduleSpecException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

