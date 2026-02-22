using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Xians.Lib.Agents.Messaging;

 /// <summary>
/// Elegant console narrator for Sentra Labs demos.
/// Handles structured and streaming output with color and pacing.
/// </summary>
public static class Tracker
{
   
    /// <summary>
    /// Streams an agent run, sends reasoning/tools to context (SendReasoningAsync, SendToolExecAsync), and returns the accumulated response text.
    /// </summary>
    public static async Task<string> StreamAgentAndReturnTextAsync(IAsyncEnumerable<AgentRunResponseUpdate> updates, UserMessageContext context)
    {
        var fullText = new StringBuilder();

        await foreach (var update in updates)
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                fullText.Append(update.Text);
                continue;
            }

            if (update.Contents is not { Count: > 0 })
                continue;

            foreach (var content in update.Contents)
            {
                switch (content)
                {
                    case TextReasoningContent reasoning:
                        await context.SendReasoningAsync(reasoning.Text);
                        break;
                    case FunctionCallContent call:
                        string argText = call.Arguments is null
                            ? ""
                            : string.Join(" ",
                                call.Arguments.Select(kv => $"{kv.Key}={FormatArg(kv.Value)}"));
                        await context.SendToolExecAsync($"[Tool Call] {call.Name}({argText})");
                        break;
                    case FunctionResultContent result:
                        string preview = ToPreview(result.Result);
                        await context.SendToolExecAsync($"[Tool Result] → {preview}");
                        break;
                    case TextContent text:
                        fullText.Append(text.Text);
                        break;
                    case DataContent data:
                        await context.SendReasoningAsync($"[Data] {ToPreview(data.Data)}");
                        break;
                    case ErrorContent err:
                        await context.SendReasoningAsync($"[Error] {err.Message}");
                        break;
                    case HostedFileContent file:
                        await context.SendReasoningAsync($"[HostedFile] {file.FileId}");
                        break;
                    case HostedVectorStoreContent vs:
                        await context.SendReasoningAsync($"[HostedVectorStore] {vs.VectorStoreId}");
                        break;
                    case UriContent uri:
                        await context.SendReasoningAsync($"[Uri] {uri.Uri}");
                        break;
                    case UsageContent usage when usage.Details is not null:
                        await context.SendReasoningAsync($"[Usage] Total: {usage.Details.TotalTokenCount} tokens");
                        break;
                    default:
                        break;
                }
            }
        }

        return fullText.ToString();
    }

    private static string FormatArg(object? value)
    {
        if (value is null) return "null";
        return value switch
        {
            string s => s,
            bool b => b.ToString().ToLowerInvariant(),
            int or long or double or float or decimal =>
                Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
            _ => JsonSerializer.Serialize(value)
        };
    }

    private static string ToPreview(object? resultObj, int max = 240)
    {
        if (resultObj is null) return "(null)";
        string s = resultObj switch
        {
            string str => str,
            BinaryData bd => bd.ToString(),
            _ => JsonSerializer.Serialize(resultObj)
        };
        return s.Length > max ? s[..max] + "…" : s;
    }
}
