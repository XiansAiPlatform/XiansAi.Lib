using System.ComponentModel;
using Xians.Lib.Agents.Messaging;

public class MafSubAgentTools
{
    private readonly UserMessageContext _context;

    public MafSubAgentTools(UserMessageContext context)
    {
        _context = context;
    }

    [Description("Get the current date and time.")]
    public string GetCurrentDateTime()
    {
        var now = DateTime.Now;
        return $"The current date and time is: {now:yyyy-MM-dd HH:mm:ss}";
    }

    [Description("Get simulated weather information for a location. Use when the user asks about weather.")]
    public string GetWeatherInfo([Description("The location to get weather for")] string location = "unknown")
    {
        return $"Weather for {location}: Partly cloudy, 72°F. (Simulated response)";
    }
}
