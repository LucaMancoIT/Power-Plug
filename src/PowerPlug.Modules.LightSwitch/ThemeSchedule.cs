using System.Globalization;

namespace PowerPlug.Modules.LightSwitch;

public enum Theme { Light, Dark }

/// <summary>Pure scheduling logic (no I/O): which theme applies when, and when it changes next.</summary>
public static class ThemeSchedule
{
    /// <summary>Works for schedules that wrap past midnight. Equal times mean "always light".</summary>
    public static Theme Resolve(TimeSpan timeOfDay, TimeSpan lightFrom, TimeSpan darkFrom)
    {
        if (lightFrom == darkFrom) return Theme.Light;
        return lightFrom < darkFrom
            ? (timeOfDay >= lightFrom && timeOfDay < darkFrom ? Theme.Light : Theme.Dark)
            : (timeOfDay >= darkFrom && timeOfDay < lightFrom ? Theme.Dark : Theme.Light);
    }

    public static DateTime NextChange(DateTime now, TimeSpan lightFrom, TimeSpan darkFrom) =>
        new[] { lightFrom, darkFrom }
            .Select(t => { var d = now.Date + t; return d > now ? d : d.AddDays(1); })
            .Min();

    /// <summary>Parses "HH:mm"; invalid input falls back so a typo can never break the module.</summary>
    public static TimeSpan ParseOrDefault(string? text, TimeSpan fallback) =>
        TimeSpan.TryParseExact(text, @"h\:mm", CultureInfo.InvariantCulture, out var t)
        && t >= TimeSpan.Zero && t < TimeSpan.FromDays(1) ? t : fallback;
}
