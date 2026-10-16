using PromptDefinedAgent.Webhooks;
using Xians.Lib.Agents.Messaging;

namespace Xians.Lib.Tests.UnitTests.PromptDefinedAgent;

public class WebhookPromptTests
{
    [Fact]
    public void FromBuildsPromptFromConfiguredTaskAndRequestData()
    {
        var context = new TestWebhookContext
        {
            Webhook = new WebhookMessage { Payload = "{\"ticket\":42}" },
            Metadata = new Dictionary<string, string>
            {
                ["Input:Prompt"] = "Summarize the ticket",
                ["Input:InputGuidance"] = "Use the ticket field",
                ["Query:source"] = "support",
                ["Content-Type"] = "application/json"
            }
        };

        var result = WebhookPrompt.From(context).ToAgentPrompt();

        Assert.Contains("Summarize the ticket", result);
        Assert.Contains("Use the ticket field", result);
        Assert.Contains("{\"ticket\":42}", result);
        Assert.Contains("source: support", result);
        Assert.Contains("Content-Type: application/json", result);
        Assert.DoesNotContain("Input:Prompt", result);
    }

    [Fact]
    public void FromRequiresConfiguredPrompt()
    {
        var context = new TestWebhookContext { Metadata = [] };

        Assert.Throws<InvalidOperationException>(() => WebhookPrompt.From(context));
    }

    [Theory]
    [InlineData("list_workflows")]
    [InlineData("list_schedules")]
    [InlineData("create_schedule")]
    [InlineData("update_schedule_timing")]
    [InlineData("pause_schedule")]
    [InlineData("resume_schedule")]
    [InlineData("delete_schedule")]
    [InlineData("list_webhooks")]
    [InlineData("create_webhook")]
    [InlineData("update_webhook")]
    [InlineData("delete_webhook")]
    public void WebhookExecutionExcludesTriggerManagementTools(string toolName)
    {
        Assert.False(WebhookToolPolicy.Allows(toolName));
    }

    [Theory]
    [InlineData("search_repositories")]
    [InlineData("save_data_record")]
    public void WebhookExecutionAllowsTaskTools(string toolName)
    {
        Assert.True(WebhookToolPolicy.Allows(toolName));
    }

    private sealed class TestWebhookContext : WebhookContext;
}
