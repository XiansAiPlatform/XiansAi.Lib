using System.ComponentModel;
using System.Text.RegularExpressions;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Messaging;

namespace Xians.Examples.HITLAgent;

/// <summary>
/// Tool the chat agent calls when the user asks for a HITL task.
/// The tool runs inside the chat activity, so it starts <see cref="HitlTaskWorkflow"/>
/// rather than creating the task directly.
/// </summary>
public sealed partial class HitlTaskTools
{
    private readonly UserMessageContext _context;

    public HitlTaskTools(UserMessageContext context)
    {
        _context = context;
    }

    [Description("Create a human-in-the-loop task when the user asks for a person to review, approve, reject, or decide something. Do not call this for ordinary conversation.")]
    public async Task<string> CreateHitlTask(
        [Description("Short title for the task")] string title,
        [Description("What the reviewer should decide or check")] string description,
        [Description("Optional draft text the reviewer can edit. Pass an empty string if there is no draft.")] string draft = "",
        [Description("Comma-separated actions, such as approve, reject, hold. Pass an empty string to use approve, reject.")] string actions = "",
        [Description("Optional timeout such as 30m, 2h, or 90s. Pass an empty string to wait until someone completes the task.")] string timeout = "")
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "A task title is required. Ask the user what the task should be called.";
        }

        if (!TryParseTimeout(timeout, out var timeoutSeconds, out var timeoutError))
        {
            return timeoutError;
        }

        var request = new HitlTaskRequest
        {
            ParticipantId = _context.Message.ParticipantId,
            Scope = _context.Message.Scope,
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? title.Trim() : description.Trim(),
            DraftWork = string.IsNullOrWhiteSpace(draft) ? null : draft.Trim(),
            Actions = ParseActions(actions),
            TimeoutSeconds = timeoutSeconds
        };

        if (string.IsNullOrWhiteSpace(request.DraftWork))
        {
            request.DraftWork = request.Description;
        }

        try
        {
            await _context.SendToolExecAsync(
                new { request.Title, request.Description, request.Actions },
                $"Creating HITL task: {request.Title}");

            await XiansContext.Workflows.StartAsync<HitlTaskWorkflow>(
                [request],
                uniqueKey: Guid.NewGuid().ToString(),
                executionTimeout: timeoutSeconds is int seconds
                    ? TimeSpan.FromSeconds(seconds) + TimeSpan.FromMinutes(5)
                    : null);

            return $"Opened a HITL task titled \"{request.Title}\" with actions {string.Join(", ", request.Actions)}. "
                + "The user will get another message when someone completes it.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return $"Could not open the task: {ex.Message}";
        }
    }

    private static string[] ParseActions(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ["approve", "reject"];
        }

        var actions = raw
            .Split([',', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(action => action.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return actions.Length == 0 ? ["approve", "reject"] : actions;
    }

    private static bool TryParseTimeout(string? raw, out int? seconds, out string error)
    {
        seconds = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var match = TimeoutPattern().Match(raw.Trim());
        if (!match.Success || !int.TryParse(match.Groups["amount"].Value, out var amount) || amount <= 0)
        {
            error = "Could not read the timeout. Use 30m, 2h, or 90s.";
            return false;
        }

        var multiplier = match.Groups["unit"].Value.ToLowerInvariant() switch
        {
            "m" or "min" or "mins" or "minute" or "minutes" => 60,
            "h" or "hr" or "hrs" or "hour" or "hours" => 3600,
            "d" or "day" or "days" => 86400,
            _ => 1
        };

        seconds = amount * multiplier;
        return true;
    }

    [GeneratedRegex(
        @"^(?<amount>\d+)\s*(?<unit>s|sec|secs|second|seconds|m|min|mins|minute|minutes|h|hr|hrs|hour|hours|d|day|days)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TimeoutPattern();
}
