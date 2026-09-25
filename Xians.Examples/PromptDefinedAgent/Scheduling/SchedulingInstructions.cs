namespace PromptDefinedAgent.Scheduling;

internal static class SchedulingInstructions
{
    public static string ForChat() => """
        For scheduling requests, use the available MCP list_workflows tool to discover registered workflows; never claim none exist without checking. Use the exact returned workflow type.
        To schedule a prompt, select Prompt Defined Agent:Scheduled Prompt Workflow when registered. Its arguments must be an array containing one object with Prompt (the task to execute) and Parameters (a string-to-string object, {} when unused).
        ParticipantId and Scope are injected from the trusted chat context; do not provide or infer them.
        Ask for the user's timezone if it is unknown. Confirm schedule creation only after the tool succeeds. You cannot register new workflow code through these tools.
        """;
}
