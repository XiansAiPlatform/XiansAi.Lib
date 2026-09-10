using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Common;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Documents.Models;
using Xians.Lib.Agents.Scheduling;
using Xians.Lib.Agents.Scheduling.Models;
using Xians.Lib.Common;
using Xians.Lib.Temporal.Workflows.Messaging;
using Xians.Lib.Temporal.Workflows.Messaging.Models;

[Workflow("Briefing Agent:Check-In Workflow")]
public sealed class CheckInWorkflow
{
    private static readonly ActivityOptions ActivityOptions = new()
    {
        StartToCloseTimeout = TimeSpan.FromMinutes(2),
        RetryPolicy = new RetryPolicy { MaximumAttempts = 3, BackoffCoefficient = 2 }
    };

    [WorkflowRun]
    public async Task RunAsync(bool sendMessage = true)
    {
        var intervalMinutes = await Workflow.ExecuteActivityAsync(
            (CheckInActivities activities) => activities.GetIntervalMinutes(),
            ActivityOptions);

        await Workflow.ExecuteActivityAsync(
            (CheckInActivities activities) => activities.DeleteLegacySchedulesAsync(),
            ActivityOptions);

        await XiansContext.CurrentAgent.Schedules
            .Create<CheckInWorkflow>(CheckInActivities.ScheduleName)
            .EveryMinutes(intervalMinutes)
            .WithInput(true)
            .SkipIfRunning()
            .CreateIfNotExistsAsync();

        if (!sendMessage)
        {
            Workflow.Logger.LogInformation(
                "Check-in schedule is set for every {IntervalMinutes} minute(s). No message sent on this start.",
                intervalMinutes);
            return;
        }

        var targets = await Workflow.ExecuteActivityAsync(
            (CheckInActivities activities) => activities.GetCheckInTargetsAsync(),
            ActivityOptions);

        if (targets.Count == 0)
        {
            Workflow.Logger.LogInformation(
                "Skipping proactive check-in; nobody is due (no subscribers or they were just active).");
            return;
        }

        var text = await Workflow.ExecuteActivityAsync(
            (CheckInActivities activities) => activities.BuildCheckInText(),
            ActivityOptions);

        var agentName = XiansContext.CurrentAgent.Name;
        var workflowType = XiansContext.BuildBuiltInWorkflowType(agentName, WorkflowConstants.WorkflowTypes.Supervisor);
        var workflowId = XiansContext.BuildBuiltInWorkflowId(agentName, WorkflowConstants.WorkflowTypes.Supervisor);
        var tenantId = XiansContext.TenantId;

        foreach (var target in targets)
        {
            Workflow.Logger.LogInformation(
                "Sending proactive check-in to {ParticipantId} scope={Scope} origin={Origin} thread={ThreadId}",
                target.ParticipantId,
                target.Scope ?? "(none)",
                target.Origin ?? "(none)",
                target.ThreadId ?? "(none)");

            var request = new SendMessageRequest
            {
                ParticipantId = target.ParticipantId,
                WorkflowId = workflowId,
                WorkflowType = workflowType,
                Text = text,
                RequestId = Workflow.NewGuid().ToString(),
                Scope = target.Scope,
                ThreadId = target.ThreadId,
                Authorization = target.Authorization,
                // Leave Origin null unless we stored an app: platform origin.
                // Replies work that way: the server copies last inbound origin
                // (app:slack:{id} / app:msteams:{id}) so AppMessageRouter can deliver.
                Origin = target.Origin,
                Hint = "proactive-check-in",
                Type = "chat",
                TenantId = tenantId
            };

            await Workflow.ExecuteActivityAsync(
                (MessageActivities act) => act.SendMessageAsync(request),
                ActivityOptions);
        }
    }
}

internal sealed class CheckInActivities
{
    internal const string SubscriberDocumentType = "check-in-subscriber";
    internal const string WorkflowUniqueKey = "proactive-check-in";
    internal const string ScheduleName = "channel-check-in-10";
    internal const int DefaultIntervalMinutes = 10;
    private static readonly string[] LegacyScheduleNames =
        ["friendly-check-in", "proactive-check-in", "channel-check-in"];
    private static readonly TimeSpan RecentlyActiveWindow = TimeSpan.FromMinutes(1);

    [Activity]
    public int GetIntervalMinutes()
    {
        var raw = Environment.GetEnvironmentVariable("PROACTIVE_CHECKIN_EVERY_MINUTES");
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) && minutes > 0
            ? minutes
            : DefaultIntervalMinutes;
    }

    [Activity]
    public async Task DeleteLegacySchedulesAsync()
    {
        var logger = ActivityExecutionContext.Current.Logger;
        foreach (var name in LegacyScheduleNames)
        {
            try
            {
                await XiansContext.CurrentAgent.Schedules.DeleteAsync(name).ConfigureAwait(false);
                logger.LogInformation("Deleted legacy check-in schedule {ScheduleName}", name);
            }
            catch (ScheduleNotFoundException)
            {
                logger.LogDebug("Legacy check-in schedule {ScheduleName} was already absent", name);
            }
        }
    }

    [Activity]
    public async Task<List<CheckInTarget>> GetCheckInTargetsAsync()
    {
        var subscribers = await XiansContext.CurrentAgent.Documents.QueryAsync(new DocumentQuery
        {
            Type = SubscriberDocumentType,
            Limit = 100
        }).ConfigureAwait(false);

        var targets = subscribers
            .Select(ToSubscriber)
            .OfType<CheckInTarget>()
            .Where(subscriber => !WasRecentlyActive(subscriber))
            .OrderByDescending(subscriber => subscriber.ThreadId is not null)
            .ThenByDescending(subscriber => subscriber.LastSeenAt ?? DateTime.MinValue)
            .DistinctBy(subscriber => (subscriber.ParticipantId, subscriber.Scope))
            .ToList();

        var scopedParticipants = targets
            .Where(target => target.Scope is not null)
            .Select(target => target.ParticipantId)
            .ToHashSet(StringComparer.Ordinal);

        return targets
            .Where(target => target.Scope is not null || !scopedParticipants.Contains(target.ParticipantId))
            .ToList();
    }

    [Activity]
    public string BuildCheckInText()
    {
        var hour = DateTime.Now.Hour;
        var greeting = hour switch
        {
            < 12 => "Good morning",
            < 17 => "Good afternoon",
            _ => "Good evening"
        };

        return $"{greeting} — just checking in. I'm here if you need anything. What's on your mind?";
    }

    internal static async Task RememberParticipantAsync(
        string? participantId,
        string? scope,
        string? threadId,
        string? authorization,
        IReadOnlyDictionary<string, string>? metadata)
    {
        if (string.IsNullOrWhiteSpace(participantId))
        {
            return;
        }

        await XiansContext.CurrentAgent.Documents.SaveAsync(new Document
        {
            Type = SubscriberDocumentType,
            Key = SubscriberKey(participantId, scope),
            ParticipantId = participantId,
            Content = JsonSerializer.SerializeToElement(new
            {
                ParticipantId = participantId,
                Scope = scope,
                ThreadId = threadId,
                Authorization = authorization,
                Metadata = metadata,
                LastSeenAt = DateTime.UtcNow.ToString("O")
            })
        }).ConfigureAwait(false);
    }

    private static string SubscriberKey(string participantId, string? scope) =>
        $"{participantId}::{scope ?? ""}";

    private static CheckInTarget? ToSubscriber(Document document)
    {
        string? participantId = document.ParticipantId;
        string? scope = null;
        string? threadId = null;
        string? authorization = null;
        DateTime? lastSeenAt = null;
        Dictionary<string, string>? metadata = null;

        if (document.Content is { } content)
        {
            participantId = ReadString(content, "ParticipantId") ?? participantId;
            scope = ReadString(content, "Scope");
            threadId = ReadString(content, "ThreadId");
            authorization = ReadString(content, "Authorization");
            lastSeenAt = ReadTimestamp(content, "LastSeenAt");
            metadata = ReadStringMap(content, "Metadata");
        }

        if (string.IsNullOrWhiteSpace(participantId) && !string.IsNullOrWhiteSpace(document.Key))
        {
            var separator = document.Key.LastIndexOf("::", StringComparison.Ordinal);
            participantId = separator >= 0 ? document.Key[..separator] : document.Key;
        }

        if (string.IsNullOrWhiteSpace(participantId))
        {
            return null;
        }

        scope = string.IsNullOrWhiteSpace(scope) ? null : scope;
        return new CheckInTarget
        {
            ParticipantId = participantId,
            Scope = scope,
            ThreadId = string.IsNullOrWhiteSpace(threadId) ? null : threadId,
            Authorization = string.IsNullOrWhiteSpace(authorization) ? null : authorization,
            Origin = ResolveOrigin(metadata),
            LastSeenAt = lastSeenAt
        };
    }

    private static bool WasRecentlyActive(CheckInTarget subscriber)
    {
        if (subscriber.LastSeenAt is not { } lastSeenAt)
        {
            return false;
        }

        return DateTime.UtcNow - lastSeenAt.ToUniversalTime() < RecentlyActiveWindow;
    }

    internal static string? ResolveOrigin(IReadOnlyDictionary<string, string>? metadata)
    {
        return metadata?
            .Where(pair => string.Equals(pair.Key, "origin", StringComparison.OrdinalIgnoreCase)
                           && pair.Value.StartsWith("app:", StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value)
            .FirstOrDefault();
    }

    private static string? ReadString(JsonElement content, string name)
    {
        return content.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
    }

    private static DateTime? ReadTimestamp(JsonElement content, string name)
    {
        if (!content.TryGetProperty(name, out var element))
        {
            return null;
        }

        var raw = element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : element.GetRawText().Trim('"');

        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }

    private static Dictionary<string, string>? ReadStringMap(JsonElement content, string name)
    {
        if (!content.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (propertyName, value) in element.EnumerateObject()
                     .Where(property => property.Value.ValueKind == JsonValueKind.String)
                     .Select(property => (property.Name, Value: property.Value.GetString()))
                     .Where(pair => pair.Value is not null))
        {
            map[propertyName] = value!;
        }

        return map.Count == 0 ? null : map;
    }
}

public sealed class CheckInTarget
{
    public string ParticipantId { get; set; } = "";
    public string? Scope { get; set; }
    public string? ThreadId { get; set; }
    public string? Authorization { get; set; }
    public string? Origin { get; set; }
    public DateTime? LastSeenAt { get; set; }
}
