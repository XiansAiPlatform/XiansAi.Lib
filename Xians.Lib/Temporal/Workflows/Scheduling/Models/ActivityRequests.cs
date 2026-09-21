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
    public string? IdPostfix { get; set; }
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
    public string? IdPostfix { get; set; }
    public Dictionary<string, object>? SearchAttributes { get; set; }
}

/// <summary>
/// Common addressing fields for schedule management activities.
/// </summary>
/// <remarks>
/// <see cref="FullScheduleId"/> is resolved in workflow code before dispatch and is authoritative
/// whenever it is set. Search attributes and memo are only readable from workflow context, so an
/// activity that re-derives the id from <see cref="IdPostfix"/> can resolve a different schedule than
/// the caller meant.
/// </remarks>
public abstract class ScheduleRequestBase
{
    public required string ScheduleName { get; set; }
    public string? IdPostfix { get; set; }
    public string? FullScheduleId { get; set; }
}

/// <summary>
/// Request object for checking schedule existence via activity.
/// </summary>
public class ScheduleExistsRequest : ScheduleRequestBase
{
}

/// <summary>
/// Request object for deleting a schedule via activity.
/// </summary>
public class DeleteScheduleRequest : ScheduleRequestBase
{
}

/// <summary>
/// Request object for pausing a schedule via activity.
/// </summary>
public class PauseScheduleRequest : ScheduleRequestBase
{
    public string? Note { get; set; }
}

/// <summary>
/// Request object for resuming a schedule via activity.
/// </summary>
public class ResumeScheduleRequest : ScheduleRequestBase
{
    public string? Note { get; set; }
}

/// <summary>
/// Request object for triggering a schedule via activity.
/// </summary>
public class TriggerScheduleRequest : ScheduleRequestBase
{
}

/// <summary>
/// Request object for loading or describing a schedule via activity (verifies it exists).
/// </summary>
public class GetScheduleRequest : ScheduleRequestBase
{
}

/// <summary>
/// Serializable identity of a schedule, returned from get/exists activities.
/// </summary>
public class ScheduleIdentity
{
    public required string ScheduleName { get; set; }
    public string? IdPostfix { get; set; }
    public required string FullScheduleId { get; set; }
}

/// <summary>
/// Request object for backfilling a schedule via activity.
/// </summary>
public class BackfillScheduleRequest : ScheduleRequestBase
{
    public required IReadOnlyCollection<Temporalio.Client.Schedules.ScheduleBackfill> Backfills { get; set; }
}

/// <summary>
/// Serializable projection of a Temporal <c>ScheduleDescription</c>.
/// </summary>
/// <remarks>
/// <c>ScheduleDescription</c> itself cannot cross an activity boundary: it exposes no public
/// constructor, so Temporal's JSON converter cannot rebuild it, and its <c>TypedSearchAttributes</c>
/// is an <c>IReadOnlyCollection</c> that encodes to an empty array. This carries the fields workflow
/// code actually needs instead.
/// </remarks>
public class ScheduleSnapshot
{
    public required string Id { get; set; }
    public bool Paused { get; set; }
    public string? Note { get; set; }
    public long NumActions { get; set; }
    public long NumActionsMissedCatchupWindow { get; set; }
    public long NumActionsSkippedOverlap { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public List<DateTime> NextActionTimes { get; set; } = new();
}






