namespace Xians.Lib.Temporal.Workflows.Scheduling.Models;

/// <summary>
/// Request object for creating a cron schedule via activity.
/// </summary>
public class CreateCronScheduleRequest
{
    public required string ScheduleName { get; set; }
    public required string CronExpression { get; set; }
    public required object[] WorkflowInput { get; set; }
    public string? Timezone { get; set; }
    public required string IdPostfix { get; set; }
    public required string WorkflowType { get; set; }
    public Dictionary<string, object>? SearchAttributes { get; set; }
}

/// <summary>
/// Request object for creating an interval schedule via activity.
/// </summary>
public class CreateIntervalScheduleRequest
{
    public required string ScheduleName { get; set; }
    public required string WorkflowType { get; set; }
    public required TimeSpan Interval { get; set; }
    public required object[] WorkflowInput { get; set; }
    public required string IdPostfix { get; set; }
    public Dictionary<string, object>? SearchAttributes { get; set; }
}

/// <summary>
/// Request object for checking schedule existence via activity.
/// </summary>
public class ScheduleExistsRequest
{
    public required string ScheduleName { get; set; }
    public required string IdPostfix { get; set; }
}

/// <summary>
/// Request object for deleting a schedule via activity.
/// </summary>
public class DeleteScheduleRequest
{
    public required string ScheduleName { get; set; }
    public required string IdPostfix { get; set; }
}

/// <summary>
/// Request object for pausing a schedule via activity.
/// </summary>
public class PauseScheduleRequest
{
    public required string ScheduleName { get; set; }
    public required string IdPostfix { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// Request object for resuming a schedule via activity.
/// </summary>
public class ResumeScheduleRequest
{
    public required string ScheduleName { get; set; }
    public required string IdPostfix { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// Request object for triggering a schedule via activity.
/// </summary>
public class TriggerScheduleRequest
{
    public required string ScheduleName { get; set; }
    public required string IdPostfix { get; set; }
}






