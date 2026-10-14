namespace PromptDefinedAgent.Webhooks;

internal static class WebhookInstructions
{
    public static string ForChat() => """
        When the user asks to save a reusable workflow but does not specify a trigger, ask whether it should run on a schedule or when a webhook is received.
        For webhook-triggered prompts, use list_workflows and select Prompt Defined Agent:Prompt Webhook Workflow when registered.
        Use create_webhook with workflowInputs containing Prompt and optional InputGuidance. Prompt must contain the complete task. InputGuidance explains how to use the request body, query parameters, or headers.
        Use a 120-second timeout when the user does not specify one.
        The webhook waits for execution and returns the result to its HTTP caller. Confirm creation only after the tool succeeds, and disclose the returned URL only to the user and intended caller.
        """;
}
