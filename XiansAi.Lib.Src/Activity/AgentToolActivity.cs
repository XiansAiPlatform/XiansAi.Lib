using System.Net.Sockets;
using System.Reflection;
using Microsoft.Extensions.Logging;
using XiansAi.Models;
using XiansAi.Docker;

namespace XiansAi.Activity;


public abstract class AgentToolActivity : InstructionActivity
{
    private readonly ILogger _logger;
    public AgentToolActivity() : base()
    {
        _logger = Globals.LogFactory.CreateLogger<AgentToolActivity>();
    }

    public IList<AgentToolInfo> GetAgentTools(AgentToolType? type = null)
    {
        if (CurrentActivityInterfaceType == null) {
            _logger.LogError($"[{GetType().Name}] CurrentActivityInterfaceType is null.");
            throw new InvalidOperationException($"[{GetType().Name}] CurrentActivityInterfaceType is null.");
        }
        var attributes = CurrentActivityInterfaceType.GetCustomAttributes<AgentToolAttribute>();

        var agentTools = new List<AgentToolInfo>();
        foreach (var attribute in attributes) {
            if (attribute.Name == null) {
                _logger.LogError($"[{GetType().Name}] AgentToolAttribute.Name is missing.");
                throw new InvalidOperationException($"[{GetType().Name}] AgentToolAttribute.Name is missing.");
            }
            if (type != null && attribute.Type != type) {
                continue;
            }
            agentTools.Add(new AgentToolInfo { Name = attribute.Name, Type = attribute.Type });
        }
        return agentTools;
    }

    public override FlowActivityHistory? GetCurrentActivity()
    {
        var activity = base.GetCurrentActivity();
        if (activity != null) {
            activity.AgentToolNames = GetAgentTools().Select(agent => agent.ToString()).ToList();
        }
        return activity;
    }

}

public class AgentToolInfo
{
    public required string Name { get; set; }
    public required AgentToolType Type { get; set; }

    public override string ToString()
    {
        return $" {Type} [{Name}]";
    }
}
