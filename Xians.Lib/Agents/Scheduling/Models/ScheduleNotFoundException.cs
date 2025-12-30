namespace Xians.Lib.Agents.Scheduling.Models;

/// <summary>
/// Exception thrown when a schedule is not found.
/// </summary>
public class ScheduleNotFoundException : Exception
{
    public string ScheduleId { get; }

    public ScheduleNotFoundException(string scheduleId)
        : base($"Schedule '{scheduleId}' not found.")
    {
        ScheduleId = scheduleId;
    }

    public ScheduleNotFoundException(string scheduleId, Exception innerException)
        : base($"Schedule '{scheduleId}' not found.", innerException)
    {
        ScheduleId = scheduleId;
    }
}

