using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace PromptDefinedAgent.Mcp;

internal sealed record XiansToolContext(
    string TenantId,
    string AgentName,
    string ActivationName,
    string ParticipantId,
    string? Scope);

internal sealed class XiansContextFunction(
    AIFunction inner,
    IReadOnlyDictionary<string, object?> bindings,
    XiansToolContext context)
    : DelegatingAIFunction(inner)
{
    public override JsonElement JsonSchema { get; } = HideBindings(inner.JsonSchema, bindings.Keys);

    public static AITool Bind(AITool tool, XiansToolContext context)
    {
        if (tool is not AIFunction function) return tool;
        var bindings = new Dictionary<string, object?>();
        switch (function.Name)
        {
            case "list_agents":
                bindings["tenantId"] = context.TenantId;
                break;
            case "list_activations":
                bindings["tenantId"] = context.TenantId;
                bindings["agentName"] = context.AgentName;
                break;
            case "list_workflows":
            case "list_schedules":
            case "create_schedule":
            case "update_schedule_timing":
            case "delete_schedule":
            case "pause_schedule":
            case "resume_schedule":
            case "list_data_types":
            case "list_data_records":
            case "save_data_record":
            case "delete_data_record":
            case "delete_data_records":
                bindings["target"] = new
                {
                    tenantId = context.TenantId,
                    agentName = context.AgentName,
                    activationName = context.ActivationName
                };
                break;
            default:
                return tool;
        }
        return new XiansContextFunction(function, bindings, context);
    }

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var bound = new AIFunctionArguments(arguments) { Services = arguments.Services };
        foreach (var binding in bindings) bound[binding.Key] = binding.Value;
        if (Name == "create_schedule") BindScheduledPromptDelivery(bound);
        return base.InvokeCoreAsync(bound, cancellationToken);
    }

    private void BindScheduledPromptDelivery(AIFunctionArguments arguments)
    {
        if (!arguments.TryGetValue("workflowType", out var workflowType) ||
            GetString(workflowType) != "Prompt Defined Agent:Scheduled Prompt Workflow") return;
        if (string.IsNullOrWhiteSpace(context.ParticipantId))
            throw new InvalidOperationException("Scheduled prompt delivery requires a participant.");
        if (!arguments.TryGetValue("arguments", out var value))
            throw new InvalidOperationException("Scheduled prompt arguments are required.");

        var inputs = JsonSerializer.SerializeToNode(value) as JsonArray;
        if (inputs is not { Count: 1 } || inputs[0] is not JsonObject input)
            throw new InvalidOperationException("Scheduled prompts require exactly one object argument.");
        input["ParticipantId"] = context.ParticipantId;
        input["Scope"] = context.Scope;
        arguments["arguments"] = inputs.Select(input => JsonSerializer.SerializeToElement(input)).ToArray();
    }

    private static string? GetString(object? value) => value switch
    {
        string text => text,
        JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
        _ => null
    };

    private static JsonElement HideBindings(JsonElement schema, IEnumerable<string> names)
    {
        var result = JsonNode.Parse(schema.GetRawText()) as JsonObject
            ?? throw new InvalidOperationException("Tool schema must be a JSON object.");
        var properties = result["properties"] as JsonObject
            ?? throw new InvalidOperationException("Tool schema must define object properties.");
        foreach (var name in names)
        {
            if (!properties.Remove(name))
                throw new InvalidOperationException($"Tool schema does not define the bound property '{name}'.");
            if (result["required"] is JsonArray required)
            {
                foreach (var item in required.Where(item => item?.GetValue<string>() == name).ToArray())
                    required.Remove(item);
            }
        }
        return JsonSerializer.SerializeToElement(result);
    }
}
