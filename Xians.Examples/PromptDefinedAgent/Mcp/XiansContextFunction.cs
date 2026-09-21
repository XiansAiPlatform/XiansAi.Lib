using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Xians.Lib.Agents.Core;

namespace PromptDefinedAgent.Mcp;

internal sealed class XiansContextFunction(AIFunction inner, IReadOnlyDictionary<string, object?> bindings)
    : DelegatingAIFunction(inner)
{
    public override JsonElement JsonSchema { get; } = HideBindings(inner.JsonSchema, bindings.Keys);

    public static AITool Bind(AITool tool)
    {
        if (tool is not AIFunction function) return tool;
        var tenantId = XiansContext.TenantId;
        var agentName = XiansContext.CurrentAgent.Name;
        var activationName = XiansContext.SafeIdPostfix;
        if (string.IsNullOrWhiteSpace(activationName))
            throw new InvalidOperationException("Xians MCP context requires an activation.");
        var bindings = new Dictionary<string, object?>();
        switch (function.Name)
        {
            case "list_agents":
                bindings["tenantId"] = tenantId;
                break;
            case "list_activations":
                bindings["tenantId"] = tenantId;
                bindings["agentName"] = agentName;
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
                bindings["target"] = new { tenantId, agentName, activationName };
                break;
            default:
                return tool;
        }
        return new XiansContextFunction(function, bindings);
    }

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var bound = new AIFunctionArguments(arguments) { Services = arguments.Services };
        foreach (var binding in bindings) bound[binding.Key] = binding.Value;
        return base.InvokeCoreAsync(bound, cancellationToken);
    }

    private static JsonElement HideBindings(JsonElement schema, IEnumerable<string> names)
    {
        var result = JsonNode.Parse(schema.GetRawText())!.AsObject();
        foreach (var name in names)
        {
            result["properties"]?.AsObject().Remove(name);
            if (result["required"] is JsonArray required)
            {
                foreach (var item in required.Where(item => item?.GetValue<string>() == name).ToArray())
                    required.Remove(item);
            }
        }
        return JsonSerializer.SerializeToElement(result);
    }
}
