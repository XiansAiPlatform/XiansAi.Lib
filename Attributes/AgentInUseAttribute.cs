namespace Flowmaxer.Common.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class AgentInUseAttribute: Attribute
{
    public AgentInUseAttribute(string agentId, string? agentProfile = null) {
        AgentId = agentId;
        AgentProfile = agentProfile;
    }

    public string AgentId { get; set; }
    public string? AgentProfile { get; private set; }
}
