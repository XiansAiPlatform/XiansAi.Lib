using Temporalio.Exceptions;

namespace Xians.Lib.Agents.Scheduling.Models;

/// <summary>
/// Exception thrown when a schedule is not found.
/// </summary>
/// <remarks>
/// Derives from <see cref="ApplicationFailureException"/> so it behaves correctly on both sides of an
/// activity boundary: thrown from an activity it is non-retryable (a missing schedule never becomes
/// present by retrying), and rethrown into workflow code it is a <see cref="FailureException"/>, which
/// fails the workflow run instead of suspending it behind endless workflow task retries.
/// </remarks>
public class ScheduleNotFoundException : ApplicationFailureException
{
    public string ScheduleId { get; }

    public ScheduleNotFoundException(string scheduleId)
        : base(
            $"Schedule '{scheduleId}' not found.",
            errorType: nameof(ScheduleNotFoundException),
            nonRetryable: true,
            details: new object[] { scheduleId })
    {
        ScheduleId = scheduleId;
    }

    public ScheduleNotFoundException(string scheduleId, Exception innerException)
        : base(
            $"Schedule '{scheduleId}' not found.",
            innerException,
            errorType: nameof(ScheduleNotFoundException),
            nonRetryable: true,
            details: new object[] { scheduleId })
    {
        ScheduleId = scheduleId;
    }

    /// <summary>
    /// Rebuilds the exception from the failure Temporal delivered for an activity that threw it,
    /// recovering the schedule id from the failure details rather than re-parsing the message.
    /// </summary>
    internal static ScheduleNotFoundException? FromFailure(ApplicationFailureException failure)
    {
        if (failure.ErrorType != nameof(ScheduleNotFoundException))
            return null;

        try
        {
            if (failure.Details.Count > 0)
                return new ScheduleNotFoundException(failure.Details.ElementAt<string>(0), failure);
        }
        catch
        {
            // Details were encoded by an older or non-SDK producer; fall through to the message.
        }

        return new ScheduleNotFoundException("unknown", failure);
    }
}
