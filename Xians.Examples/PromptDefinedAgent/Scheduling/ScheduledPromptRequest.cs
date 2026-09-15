using System.Text;

namespace PromptDefinedAgent.Scheduling;

public sealed record ScheduledPromptRequest(
    string Prompt,
    Dictionary<string, string> Parameters,
    string ParticipantId,
    string? Scope)
{
    public string ToAgentPrompt()
    {
        var result = new StringBuilder("Execute this scheduled task now. Do not create another schedule.\n\n")
            .AppendLine(Prompt);
        if (Parameters.Count == 0) return result.ToString();

        result.AppendLine().AppendLine("Parameters:");
        foreach (var parameter in Parameters) result.Append("- ").Append(parameter.Key).Append(": ").AppendLine(parameter.Value);
        return result.ToString();
    }
}
