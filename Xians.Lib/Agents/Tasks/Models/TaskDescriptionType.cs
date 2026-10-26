using System.Text.Json.Serialization;

namespace Xians.Lib.Agents.Tasks.Models;

/// <summary>
/// How a task's <see cref="TaskWorkflowRequest.Description"/> is rendered for the reviewer.
/// Serialized by name ("String", "Markdown", "Json") so the value is stable across versions.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskDescriptionType
{
    /// <summary>Plain text, shown as-is.</summary>
    String,

    /// <summary>Markdown, rendered with formatting and links.</summary>
    Markdown,

    /// <summary>A serialized JSON object or array, rendered as a collapsible tree.</summary>
    Json
}
