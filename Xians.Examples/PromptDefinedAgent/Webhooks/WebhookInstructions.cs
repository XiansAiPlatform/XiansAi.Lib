namespace PromptDefinedAgent.Webhooks;

internal static class WebhookInstructions
{
    public static string ForChat() => """
        When the user asks to save a reusable workflow but does not specify a trigger, ask whether it should run on a schedule or when a webhook is received.
        For webhook-triggered prompts, use list_workflows and select Prompt Defined Agent:Webhook Prompt Workflow when registered.
        Before creation, identify the tools, data, and access required by the stored task. Confirm that matching tools are currently available and, when practical, use read-only identity, list, search, or get calls to verify access to named resources. Do not perform the requested action during this check.
        If a required capability is missing or a read-only check fails, explain the missing requirement and do not create the webhook. Offer to continue despite the warning. Create it only if the user then explicitly asks to continue, and repeat the unresolved requirement without claiming that execution will succeed. Tool availability and read access do not guarantee future write access.
        Use create_webhook with workflowInputs containing Prompt and optional InputGuidance. Prompt must contain the complete task. InputGuidance explains how to use the request body, query parameters, or headers.
        Use a 120-second timeout when the user does not specify one.
        The webhook waits for execution and returns the result to its HTTP caller. Confirm creation only after the tool succeeds, and disclose the returned URL only to the user and intended caller.
        """;
}
