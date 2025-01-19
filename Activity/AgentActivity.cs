using System.Net.Sockets;
using System.Reflection;
using Microsoft.Extensions.Logging;
using XiansAi.Models;
using XiansAi.System;

namespace XiansAi.Activity;


public abstract class AgentActivity : InstructionActivity
{
    private readonly ILogger _logger;
    public AgentActivity() : base()
    {
        _logger = Globals.LogFactory.CreateLogger<AgentActivity>();
    }

    public IList<AgentInfo> GetAgents(AgentType? type = null)
    {
        if (CurrentActivityInterfaceType == null) {
            _logger.LogError($"[{GetType().Name}] CurrentActivityInterfaceType is null.");
            throw new InvalidOperationException($"[{GetType().Name}] CurrentActivityInterfaceType is null.");
        }
        var attributes = CurrentActivityInterfaceType.GetCustomAttributes<AgentAttribute>();

        var agents = new List<AgentInfo>();
        foreach (var attribute in attributes) {
            if (attribute.Name == null) {
                _logger.LogError($"[{GetType().Name}] AgentAttribute.Name is missing.");
                throw new InvalidOperationException($"[{GetType().Name}] AgentAttribute.Name is missing.");
            }
            if (type != null && attribute.Type != type) {
                continue;
            }
            agents.Add(new AgentInfo { Name = attribute.Name, Type = attribute.Type });
        }
        return agents;
    }

    public override FlowActivity? GetCurrentActivity()
    {
        var activity = base.GetCurrentActivity();
        if (activity != null) {
            activity.AgentNames = GetAgents().Select(agent => agent.ToString()).ToList();
        }
        return activity;
    }

}

public class AgentInfo
{
    public required string Name { get; set; }
    public required AgentType Type { get; set; }

    public override string ToString()
    {
        return $" {Type} [{Name}]";
    }
}
