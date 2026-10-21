namespace PromptDefinedAgent.Webhooks;

internal static class WebhookToolPolicy
{
    private static readonly HashSet<string> TriggerManagementTools = new(StringComparer.Ordinal)
    {
        "list_workflows",
        "list_schedules",
        "create_schedule",
        "update_schedule_timing",
        "pause_schedule",
        "resume_schedule",
        "delete_schedule",
        "list_webhooks",
        "create_webhook",
        "update_webhook",
        "delete_webhook"
    };

    public static bool Allows(string toolName) => !TriggerManagementTools.Contains(toolName);
}
