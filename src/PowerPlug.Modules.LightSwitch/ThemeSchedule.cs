using System.Globalization;

namespace PowerPlug.Modules.LightSwitch;

/// <summary>The two Windows color themes.</summary>
public enum Theme { Light, Dark }

/// <summary>
/// Pure scheduling logic with no side effects: which theme applies at which time,
/// and when the next change is due. Because nothing here touches Windows, it is easy to test.
/// </summary>
public static class ThemeSchedule
{
    /// <summary>
    /// Returns the theme that applies at <paramref name="timeOfDay"/>.
    /// Also works for schedules that cross midnight (for example light from 22:00, dark from 06:00).
    /// If both times are equal, the theme is always light.
    /// </summary>
    public static Theme Resolve(TimeSpan timeOfDay, TimeSpan lightFrom, TimeSpan darkFrom)
    {
        if (lightFrom == darkFrom) return Theme.Light;
        return lightFrom < darkFrom
            // Normal day schedule: light between the two times, dark outside.
            ? (timeOfDay >= lightFrom && timeOfDay < darkFrom ? Theme.Light : Theme.Dark)
            // Schedule crossing midnight: dark between the two times, light outside.
            : (timeOfDay >= darkFrom && timeOfDay < lightFrom ? Theme.Dark : Theme.Light);
    }

    /// <summary>
    /// The next moment (today or tomorrow) at which the theme should switch.
    /// A time that is exactly "now" counts as already passed.
    /// </summary>
    public static DateTime NextChange(DateTime now, TimeSpan lightFrom, TimeSpan darkFrom) =>
        new[] { lightFrom, darkFrom }
            .Select(t => { var d = now.Date + t; return d > now ? d : d.AddDays(1); }) // already passed today -> tomorrow
            .Min(); // whichever comes first

    /// <summary>
    /// Reads a time written as "HH:mm". If the text is missing or invalid, the fallback is used,
    /// so a typo in the settings file can never break the module.
    /// </summary>
    public static TimeSpan ParseOrDefault(string? text, TimeSpan fallback) =>
        TimeSpan.TryParseExact(text, @"h\:mm", CultureInfo.InvariantCulture, out var t)
        && t >= TimeSpan.Zero && t < TimeSpan.FromDays(1) ? t : fallback;
}
