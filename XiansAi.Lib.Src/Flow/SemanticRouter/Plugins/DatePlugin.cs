namespace Agentri.Flow.Router.Plugins;

/// <summary>
/// A plugin that provides date and time functionalities. 
/// When defining a plugin, name it with Plugin at the end.
/// </summary>
internal class DatePlugin : PluginBase<DatePlugin>
{
    /// <summary>
    /// Returns today's date in the format "yyyy-MM-dd".
    /// </summary>
    /// <returns>A string representing today's date.</returns>
    [Capability("Get the current date.")]
    public static string GetCurrentDate()
    {
        return DateTime.Now.ToString("yyyy-MM-dd");
    }

    [Capability("Get the current date and time.")]
    public static string GetCurrentDateTime()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    [Capability("Get the current time.")]
    [Parameter("format", "The format of the time to return.")]
    public static string GetCurrentTime(string format)
    {
        return DateTime.Now.ToString(format);
    }

} 