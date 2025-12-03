using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.SemanticKernel;
using TimeZoneConverter;
using XiansAi.Flow.Router;
using XiansAi.Models;

namespace XiansAi.Flow.Router.Plugins;

/// <summary>
/// A plugin that provides date and time functionalities. 
/// When defining a plugin, name it with Plugin at the end.
/// </summary>
internal class DatePlugin : PluginBase<DatePlugin>
{
    private const string DateFormatWithOffset = "yyyy-MM-dd HH:mm:ss zzz";
    private static Timezone _configuredTimeZone = Timezone.HostEnvironment;

    /// <summary>
    /// Gets all the kernel functions defined in the DatePlugin class with timezone configuration.
    /// </summary>
    /// <param name="options">Router options containing timezone configuration.</param>
    /// <returns>A collection of kernel functions.</returns>
    public static IEnumerable<KernelFunction> GetFunctions(RouterOptions options)
    {
        _configuredTimeZone = options.TimeZone;

        return PluginBase<DatePlugin>.GetFunctions();
    }

    [Capability("Get the current UTC date and time.")]
    public static string GetUtcNow()
    {
        return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
    }

    [Capability("Get the server's local date and time with offset. Uses the configured timezone if set, otherwise uses the server's local timezone.")]
    public static string GetServerLocalTime()
    {
        if (_configuredTimeZone != Timezone.HostEnvironment)
        {
            var tz = _configuredTimeZone.GetTimeZoneInfo();
            return FormatWithOffset(tz);
        }

        return DateTimeOffset.Now.ToString(DateFormatWithOffset);
    }

    [Capability("Convert UTC to a specific timezone. If timezoneId is not provided and a timezone is configured, uses the configured timezone. Otherwise, you can specify any valid IANA timezone ID to convert to.")]
    [Parameter("timezoneId", "Optional IANA timezone ID. Example: 'Asia/Colombo', 'Pacific/Auckland', 'Europe/London'. If not provided, uses the configured timezone (if set) or server local timezone.")]
    public static string ConvertUtcToZone(string? timezoneId = null)
    {
        var tzInfo = ResolveTimeZone(timezoneId);
        return FormatWithOffset(tzInfo);
    }

    [Capability("Get the configured timezone IANA ID if one is set, otherwise returns the host environment timezone IANA ID. This is the preferred/default timezone for date/time operations.")]
    public static string? GetConfiguredTimezone()
    {
        if (_configuredTimeZone != Timezone.HostEnvironment)
        {
            return _configuredTimeZone.GetIanaId();
        }

        // Return the host environment timezone IANA ID
        var localTz = TimeZoneInfo.Local;
        var localId = localTz.Id;
        
        // On Linux/macOS, TimeZoneInfo.Local.Id is already an IANA ID (contains "/")
        // On Windows, it's a Windows timezone ID (e.g., "Eastern Standard Time")
        if (localId.Contains('/'))
        {
            // Already in IANA format (Linux/macOS)
            return localId;
        }
        
        // Windows: convert Windows timezone ID to IANA
        try
        {
            return TZConvert.WindowsToIana(localId);
        }
        catch
        {
            // Fallback to the Windows timezone ID if conversion fails
            return localId;
        }
    }

    [Capability("Get the list of all available timezone IDs.")]
    public static List<string> GetAllTimezones()
    {
        return TimezoneHelper.GetAllTimezones()
            .Select(t => t.IanaId)
            .ToList();
    }

    private static string FormatWithOffset(TimeZoneInfo timeZoneInfo)
    {
        var converted = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZoneInfo);
        return converted.ToString(DateFormatWithOffset);
    }

    private static TimeZoneInfo ResolveTimeZone(string? timezoneId)
    {
        // If a specific timezone is requested, use it (allows conversions to any timezone)
        if (!string.IsNullOrWhiteSpace(timezoneId))
        {
            if (TimezoneHelper.TryParseIanaId(timezoneId, out var tzEnum) && tzEnum != Timezone.HostEnvironment)
            {
                return tzEnum.GetTimeZoneInfo();
            }

            try
            {
                return TZConvert.GetTimeZoneInfo(timezoneId);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Timezone '{timezoneId}' is invalid or unsupported.", nameof(timezoneId), ex);
            }
        }

        // If no timezone specified, use configured timezone as default (if set)
        if (_configuredTimeZone != Timezone.HostEnvironment)
        {
            return _configuredTimeZone.GetTimeZoneInfo();
        }

        // Fallback to server local timezone
        return TimeZoneInfo.Local;
    }
}