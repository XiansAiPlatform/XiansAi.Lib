using System.ComponentModel;

namespace Xians.Agent.Sample;

/// <summary>
/// Tools available to the MAF Agent for enhanced functionality.
/// </summary>
internal static class MafAgentTools
{
    /// <summary>
    /// Gets the current weather for a given location.
    /// </summary>
    [Description("Get the weather for a given location.")]
    public static string GetWeather([Description("The location to get the weather for.")] string location)
        => $"The weather in {location} is cloudy with a high of 15°C.";

    /// <summary>
    /// Gets the current time in a specified timezone.
    /// </summary>
    [Description("Get the current time in a specific timezone.")]
    public static string GetCurrentTime(
        [Description("The timezone identifier (e.g., 'UTC', 'America/New_York', 'Europe/London')")] string timezone = "UTC")
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            var currentTime = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
            return $"The current time in {timezone} is {currentTime:yyyy-MM-dd HH:mm:ss}";
        }
        catch
        {
            return $"Unable to find timezone '{timezone}'. Using UTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
        }
    }

    /// <summary>
    /// Searches for information about a topic.
    /// </summary>
    [Description("Search for information about a specific topic.")]
    public static string SearchInformation([Description("The topic to search for")] string topic)
        => $"Here are search results for '{topic}': [This is a mock search result. In production, this would connect to a real search service.]";

    /// <summary>
    /// Conducts research on a company and returns detailed information.
    /// </summary>
    [Description("Research a company and get detailed information about it.")]
    public static string ResearchCompany([Description("The company name or website URL to research")] string companyIdentifier)
    {
        return $@"Company Research Report for: {companyIdentifier}
                    
            Company Name: {companyIdentifier} Inc.
            Industry: Technology & Software Services
            Founded: 2015
            Headquarters: San Francisco, CA
            Employee Count: ~500-1000
            Annual Revenue: $50M - $100M (estimated)

            Key Products/Services:
            - Enterprise software solutions
            - Cloud-based platforms
            - AI-powered analytics tools

            Recent News:
            - Secured Series B funding ($30M) - 6 months ago
            - Launched new product line in Q3
            - Expanded to European market

            Financial Health: Stable, showing consistent growth
            Market Position: Mid-sized player with strong regional presence
            Competitive Advantage: Innovative technology stack, strong customer retention

            [This is mock data. In production, this would integrate with real company research APIs and databases.]";
    }
}

