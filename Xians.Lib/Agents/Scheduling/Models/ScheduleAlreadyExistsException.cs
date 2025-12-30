namespace Xians.Lib.Agents.Scheduling.Models;

/// <summary>
/// Exception thrown when attempting to create a schedule that already exists.
/// </summary>
public class ScheduleAlreadyExistsException : Exception
{
    public string ScheduleId { get; }

    public ScheduleAlreadyExistsException(string scheduleId)
        : base($"Schedule '{scheduleId}' already exists.")
    {
        ScheduleId = scheduleId;
    }

    public ScheduleAlreadyExistsException(string scheduleId, Exception innerException)
        : base($"Schedule '{scheduleId}' already exists.", innerException)
    {
        ScheduleId = scheduleId;
    }
}

