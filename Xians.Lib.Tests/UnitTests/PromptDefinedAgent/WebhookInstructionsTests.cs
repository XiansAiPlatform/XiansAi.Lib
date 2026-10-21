using PromptDefinedAgent.Webhooks;

namespace Xians.Lib.Tests.UnitTests.PromptDefinedAgent;

public class WebhookInstructionsTests
{
    private readonly string _instructions = WebhookInstructions.ForChat();

    [Fact]
    public void RequiresReadOnlyFeasibilityCheckBeforeCreation()
    {
        Assert.Contains("Prompt Defined Agent:Webhook Prompt Workflow", _instructions);
        Assert.Contains("Before creation", _instructions);
        Assert.Contains("read-only", _instructions);
        Assert.Contains("Do not perform the requested action", _instructions);
    }

    [Fact]
    public void BlocksMissingCapabilitiesWithoutExplicitOverride()
    {
        Assert.Contains("do not create the webhook", _instructions);
        Assert.Contains("only if the user then explicitly asks to continue", _instructions);
        Assert.Contains("without claiming that execution will succeed", _instructions);
    }
}
