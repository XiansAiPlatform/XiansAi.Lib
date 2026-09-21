using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace PromptDefinedAgent.Messaging;

internal static class ToolCallTracker
{
    public static async Task<string> RunAsync(IAsyncEnumerable<AgentRunResponseUpdate> updates, Func<object, string, Task> sendToolEvent)
    {
        var response = new StringBuilder();
        var calls = new Dictionary<string, string>();
        await foreach (var update in updates)
        {
            response.Append(update.Text);
            foreach (var content in update.Contents)
            {
                switch (content)
                {
                    case TextContent text when string.IsNullOrEmpty(update.Text):
                        response.Append(text.Text);
                        break;
                    case FunctionCallContent call:
                        calls[call.CallId] = call.Name;
                        await sendToolEvent(
                            new { tool = call.Name, callId = call.CallId, status = "calling" },
                            $"Calling {DisplayName(call.Name)}");
                        break;
                    case FunctionResultContent result:
                        calls.Remove(result.CallId, out var name);
                        await sendToolEvent(
                            new { tool = name, callId = result.CallId, status = "result_received" },
                            $"Result received from {DisplayName(name ?? "tool")}");
                        break;
                }
            }
        }
        return response.ToString();
    }

    internal static string DisplayName(string name)
    {
        var words = Regex.Replace(name, "([A-Z]+)([A-Z][a-z])", "$1 $2");
        words = Regex.Replace(words, "([a-z0-9])([A-Z])", "$1 $2");
        words = Regex.Replace(words, "[_\\-\\s]+", " ").Trim();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words);
    }
}
