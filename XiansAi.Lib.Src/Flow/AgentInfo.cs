public class AgentInfo
{
    public AgentInfo(string name, string? description = null, string? svgIcon = null)
    {
        Name = name;
        Description = description;
        SvgIcon = svgIcon;
    }

    public string Name { get; set; }
    public string? Description { get; set; }
    public string? SvgIcon { get; set; }
}
