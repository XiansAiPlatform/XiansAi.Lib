using Temporalio.Client.Schedules;
using Temporalio.Api.Enums.V1;

namespace Xians.Lib.Agents.Scheduling;

/// <summary>
/// Fluent extension methods for creating and configuring schedules.
/// Provides convenient shorthand methods for common scheduling patterns
/// and overlap policy configurations.
/// </summary>
public static class ScheduleExtensions
{
    // ========================================
    // TIME-BASED SCHEDULE PATTERNS
    // ========================================
    /// <summary>
    /// Creates a daily schedule at a specific time.
    /// </summary>
    /// <param name="builder">The schedule builder.</param>
    /// <param name="hour">Hour of day (0-23).</param>
    /// <param name="minute">Minute of hour (0-59).</param>
    /// <param name="timezone">Optional timezone (defaults to UTC).</param>
    public static ScheduleBuilder Daily(this ScheduleBuilder builder, int hour, int minute = 0, string? timezone = null)
    {
        if (hour < 0 || hour > 23)
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23");
        
        if (minute < 0 || minute > 59)
            throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be between 0 and 59");

        return builder.WithCronSchedule($"{minute} {hour} * * *", timezone);
    }

    /// <summary>
    /// Creates a weekly schedule on specific days at a specific time.
    /// </summary>
    /// <param name="builder">The schedule builder.</param>
    /// <param name="dayOfWeek">Day of week (0 = Sunday, 6 = Saturday).</param>
    /// <param name="hour">Hour of day (0-23).</param>
    /// <param name="minute">Minute of hour (0-59).</param>
    /// <param name="timezone">Optional timezone (defaults to UTC).</param>
    public static ScheduleBuilder Weekly(this ScheduleBuilder builder, DayOfWeek dayOfWeek, int hour, int minute = 0, string? timezone = null)
    {
        if (hour < 0 || hour > 23)
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23");
        
        if (minute < 0 || minute > 59)
            throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be between 0 and 59");

        var day = (int)dayOfWeek;
        return builder.WithCronSchedule($"{minute} {hour} * * {day}", timezone);
    }

    /// <summary>
    /// Creates a monthly schedule on a specific day at a specific time.
    /// </summary>
    /// <param name="builder">The schedule builder.</param>
    /// <param name="dayOfMonth">Day of month (1-31).</param>
    /// <param name="hour">Hour of day (0-23).</param>
    /// <param name="minute">Minute of hour (0-59).</param>
    /// <param name="timezone">Optional timezone (defaults to UTC).</param>
    public static ScheduleBuilder Monthly(this ScheduleBuilder builder, int dayOfMonth, int hour, int minute = 0, string? timezone = null)
    {
        if (dayOfMonth < 1 || dayOfMonth > 31)
            throw new ArgumentOutOfRangeException(nameof(dayOfMonth), "Day of month must be between 1 and 31");
        
        if (hour < 0 || hour > 23)
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23");
        
        if (minute < 0 || minute > 59)
            throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be between 0 and 59");

        return builder.WithCronSchedule($"{minute} {hour} {dayOfMonth} * *", timezone);
    }

    /// <summary>
    /// Creates an hourly schedule at a specific minute.
    /// </summary>
    /// <param name="builder">The schedule builder.</param>
    /// <param name="minute">Minute of hour (0-59).</param>
    public static ScheduleBuilder Hourly(this ScheduleBuilder builder, int minute = 0)
    {
        if (minute < 0 || minute > 59)
            throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be between 0 and 59");

        return builder.WithCronSchedule($"{minute} * * * *");
    }

    /// <summary>
    /// Creates a schedule for weekdays (Monday-Friday) at a specific time.
    /// </summary>
    /// <param name="builder">The schedule builder.</param>
    /// <param name="hour">Hour of day (0-23).</param>
    /// <param name="minute">Minute of hour (0-59).</param>
    /// <param name="timezone">Optional timezone (defaults to UTC).</param>
    public static ScheduleBuilder Weekdays(this ScheduleBuilder builder, int hour, int minute = 0, string? timezone = null)
    {
        if (hour < 0 || hour > 23)
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23");
        
        if (minute < 0 || minute > 59)
            throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be between 0 and 59");

        return builder.WithCronSchedule($"{minute} {hour} * * 1-5", timezone);
    }

    // ========================================
    // INTERVAL-BASED SCHEDULE PATTERNS
    // ========================================

    /// <summary>
    /// Creates a schedule for every N minutes.
    /// </summary>
    /// <param name="builder">The schedule builder.</param>
    /// <param name="minutes">Number of minutes between executions.</param>
    public static ScheduleBuilder EveryMinutes(this ScheduleBuilder builder, int minutes)
    {
        if (minutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(minutes), "Minutes must be greater than 0");

        return builder.WithIntervalSchedule(TimeSpan.FromMinutes(minutes));
    }

    /// <summary>
    /// Creates a schedule for every N hours.
    /// </summary>
    /// <param name="builder">The schedule builder.</param>
    /// <param name="hours">Number of hours between executions.</param>
    public static ScheduleBuilder EveryHours(this ScheduleBuilder builder, int hours)
    {
        if (hours <= 0)
            throw new ArgumentOutOfRangeException(nameof(hours), "Hours must be greater than 0");

        return builder.WithIntervalSchedule(TimeSpan.FromHours(hours));
    }

    /// <summary>
    /// Creates a schedule for every N seconds.
    /// </summary>
    /// <param name="builder">The schedule builder.</param>
    /// <param name="seconds">Number of seconds between executions.</param>
    public static ScheduleBuilder EverySeconds(this ScheduleBuilder builder, int seconds)
    {
        if (seconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(seconds), "Seconds must be greater than 0");

        return builder.WithIntervalSchedule(TimeSpan.FromSeconds(seconds));
    }

    /// <summary>
    /// Creates a schedule for every N days at a specific time.
    /// </summary>
    /// <param name="builder">The schedule builder.</param>
    /// <param name="days">Number of days between executions.</param>
    /// <param name="hour">Hour of day (0-23).</param>
    /// <param name="minute">Minute of hour (0-59).</param>
    /// <param name="timezone">Optional timezone (defaults to UTC).</param>
    public static ScheduleBuilder EveryDays(this ScheduleBuilder builder, int days, int hour = 0, int minute = 0, string? timezone = null)
    {
        if (days <= 0)
            throw new ArgumentOutOfRangeException(nameof(days), "Days must be greater than 0");
        
        if (hour < 0 || hour > 23)
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23");
        
        if (minute < 0 || minute > 59)
            throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be between 0 and 59");

        // For multi-day intervals with specific time, use interval schedule
        if (days == 1)
        {
            return builder.Daily(hour, minute, timezone);
        }

        // For intervals > 1 day, use interval schedule (time-of-day not supported)
        return builder.WithIntervalSchedule(TimeSpan.FromDays(days));
    }

    // ========================================
    // OVERLAP POLICY CONFIGURATION
    // ========================================

    /// <summary>
    /// Sets the overlap policy to allow all concurrent executions.
    /// Use when you want multiple instances to run simultaneously without restriction.
    /// </summary>
    public static ScheduleBuilder AllowOverlap(this ScheduleBuilder builder)
    {
        return builder.WithSchedulePolicy(new SchedulePolicy
        {
            Overlap = ScheduleOverlapPolicy.AllowAll
        });
    }

    /// <summary>
    /// Sets the overlap policy to skip new executions if a previous one is still running.
    /// RECOMMENDED for most use cases to prevent execution pile-up.
    /// </summary>
    /// <remarks>
    /// If a scheduled execution is triggered while the previous one is still running,
    /// the new execution will be skipped entirely. Next execution will occur at the next scheduled time.
    /// </remarks>
    public static ScheduleBuilder SkipIfRunning(this ScheduleBuilder builder)
    {
        return builder.WithSchedulePolicy(new SchedulePolicy
        {
            Overlap = ScheduleOverlapPolicy.Skip
        });
    }

    /// <summary>
    /// Sets the overlap policy to buffer one pending execution.
    /// Use when you want to ensure at most one execution is queued after the current one completes.
    /// </summary>
    /// <remarks>
    /// If an execution is running and a new one is triggered, it will be queued to run after completion.
    /// If another execution is triggered while one is queued, the queued one is replaced.
    /// </remarks>
    public static ScheduleBuilder BufferOne(this ScheduleBuilder builder)
    {
        return builder.WithSchedulePolicy(new SchedulePolicy
        {
            Overlap = ScheduleOverlapPolicy.BufferOne
        });
    }

    /// <summary>
    /// Sets the overlap policy to cancel the currently running execution and start a new one.
    /// Use when newer data/state is more important than completing the current execution.
    /// </summary>
    /// <remarks>
    /// The currently running workflow will be cancelled (can be caught and handled),
    /// and a new execution will start immediately.
    /// </remarks>
    public static ScheduleBuilder CancelOther(this ScheduleBuilder builder)
    {
        return builder.WithSchedulePolicy(new SchedulePolicy
        {
            Overlap = ScheduleOverlapPolicy.CancelOther
        });
    }

    /// <summary>
    /// Sets the overlap policy to terminate the currently running execution and start a new one.
    /// Use with caution - forcefully stops the running workflow without cleanup.
    /// </summary>
    /// <remarks>
    /// The currently running workflow will be terminated immediately without any cleanup.
    /// This is more aggressive than CancelOther and should be used sparingly.
    /// </remarks>
    public static ScheduleBuilder TerminateOther(this ScheduleBuilder builder)
    {
        return builder.WithSchedulePolicy(new SchedulePolicy
        {
            Overlap = ScheduleOverlapPolicy.TerminateOther
        });
    }
}

