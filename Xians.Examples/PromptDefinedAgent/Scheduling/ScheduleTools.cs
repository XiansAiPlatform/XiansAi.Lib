using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Temporalio.Client.Schedules;
using Temporalio.Converters;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Agents.Scheduling;

namespace PromptDefinedAgent.Scheduling;

internal sealed class ScheduleTools(UserMessageContext context)
{
    [Description("Creates a recurring schedule that runs a prompt and sends the result to the current user. Ask for a timezone when the user has not provided one.")]
    public async Task<string> CreateSchedule(
        [Description("Stable, unique schedule name using letters, numbers, dashes, or underscores")] string scheduleName,
        [Description("Complete task instructions to execute at each scheduled run")] string prompt,
        [Description("Standard five-field cron expression")] string cron,
        [Description("IANA timezone such as Asia/Colombo")] string timezone,
        [Description("Optional task-specific parameter values")] Dictionary<string, string>? parameters = null,
        [Description("Short human-readable description shown in Agent Studio")] string? description = null)
    {
        if (!Regex.IsMatch(scheduleName, "^[A-Za-z0-9_-]+$"))
            throw new ArgumentException("Schedule name may only contain letters, numbers, dashes, and underscores.", nameof(scheduleName));

        var request = new ScheduledPromptRequest(
            prompt, parameters ?? [], context.Message.ParticipantId, context.Message.Scope);

        await XiansContext.CurrentAgent.Schedules
            .Create<ScheduledPromptWorkflow>(scheduleName)
            .WithCronSchedule(cron, timezone)
            .WithInput(request)
            .WithMemo(new() { ["description"] = description ?? scheduleName })
            .CreateIfNotExistsAsync();

        return $"Schedule '{scheduleName}' is active for '{cron}' in timezone '{timezone}'.";
    }

    [Description("Lists existing schedules for the current agent activation. Call this before updating or deleting a schedule when its exact ID is unknown.")]
    public async Task<string> ListSchedules()
    {
        var schedules = await XiansContext.CurrentAgent.Schedules.ListAsync();
        var summaries = await Task.WhenAll(schedules.Select(ToSummaryAsync));
        return JsonSerializer.Serialize(summaries);
    }

    [Description("Changes the timing of an existing schedule. List schedules first when its exact ID is unknown.")]
    public async Task<string> UpdateScheduleTiming(
        [Description("Exact schedule ID or its displayed description")] string schedule,
        [Description("New standard five-field cron expression")] string cron,
        [Description("New IANA timezone such as Asia/Colombo")] string timezone)
    {
        var target = await ResolveScheduleAsync(schedule);
        await target.UpdateAsync(input => new ScheduleUpdate(new Schedule(
            input.Description.Schedule.Action,
            new ScheduleSpec { CronExpressions = [cron], TimeZoneName = timezone })
        {
            Policy = input.Description.Schedule.Policy,
            State = input.Description.Schedule.State
        }));
        return $"Schedule '{target.Id}' now runs on '{cron}' in timezone '{timezone}'.";
    }

    [Description("Permanently deletes an existing schedule. List schedules first when its exact ID is unknown.")]
    public async Task<string> DeleteSchedule(
        [Description("Exact schedule ID or its displayed description")] string schedule)
    {
        var target = await ResolveScheduleAsync(schedule);
        await target.DeleteAsync();
        return $"Schedule '{target.Id}' deleted.";
    }

    private static async Task<object> ToSummaryAsync(XiansSchedule schedule)
    {
        var details = await schedule.DescribeAsync();
        return new
        {
            schedule.Id,
            Description = GetDescription(details),
            Cron = details.Schedule.Spec.CronExpressions,
            Timezone = details.Schedule.Spec.TimeZoneName,
            Paused = details.Schedule.State.Paused
        };
    }

    private static async Task<XiansSchedule> ResolveScheduleAsync(string identifier)
    {
        var schedules = await XiansContext.CurrentAgent.Schedules.ListAsync();
        var matches = new List<XiansSchedule>();
        foreach (var schedule in schedules)
        {
            var description = GetDescription(await schedule.DescribeAsync());
            if (schedule.Id.Equals(identifier, StringComparison.OrdinalIgnoreCase) ||
                description?.Equals(identifier, StringComparison.OrdinalIgnoreCase) == true ||
                schedule.Id.EndsWith($":{identifier}", StringComparison.OrdinalIgnoreCase))
                matches.Add(schedule);
        }

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException($"No schedule matching '{identifier}' was found. List schedules and use an exact ID."),
            _ => throw new InvalidOperationException($"Multiple schedules match '{identifier}'. List schedules and use an exact ID.")
        };
    }

    private static string? GetDescription(ScheduleDescription details) =>
        details.Schedule.Action is ScheduleActionStartWorkflow action &&
        action.Options?.Memo?.TryGetValue("description", out var value) == true && value is IEncodedRawValue encoded
            ? encoded.Payload.Data.ToStringUtf8().Trim('"')
            : null;
}
