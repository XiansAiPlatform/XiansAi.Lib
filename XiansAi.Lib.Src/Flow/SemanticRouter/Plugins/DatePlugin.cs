namespace XiansAi.Flow.Router.Plugins;

/// <summary>
/// A plugin that provides date and time functionalities. 
/// When defining a plugin, name it with Plugin at the end.
/// </summary>
internal class DatePlugin : PluginBase<DatePlugin>
{
    [Capability("Get the current UTC date and time.")]
    public static string GetUtcNow()
    {
        return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
    }

    [Capability("Get the server's local date and time with offset.")]
    public static string GetServerLocalTime()
    {
        return DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz");
    }

    [Capability("Convert UTC to a specific timezone.")]
    [Parameter("timezoneId", "IANA/Windows timezone ID. Example: 'Asia/Colombo', 'Pacific/Auckland', 'Europe/London'.")]
    public static string ConvertUtcToZone(string timezoneId)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        var converted = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
        return converted.ToString("yyyy-MM-dd HH:mm:ss zzz");
    }

    [Capability("Get the list of all available timezone IDs.")]
    public static List<string> GetAllTimezones()
    {
        return TimeZoneInfo.GetSystemTimeZones().Select(t => t.Id).ToList();
    }
}