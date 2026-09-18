using Temporalio.Activities;

namespace PromptDefinedAgent.Scheduling;

public sealed class ScheduledPromptActivities(PromptAgent promptAgent)
{
    [Activity]
    public Task<string> ExecuteAsync(ScheduledPromptRequest request) =>
        promptAgent.RunScheduledAsync(request.ToAgentPrompt());
}
