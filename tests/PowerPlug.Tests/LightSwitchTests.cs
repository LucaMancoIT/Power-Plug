using PowerPlug.Core.Settings;
using PowerPlug.Modules.LightSwitch;
using Xunit;

namespace PowerPlug.Tests;

// Tests for Light Switch: the pure schedule logic (which theme at which time, next switch time,
// reading times from text) and the module behaviour with a fake Windows theme API.
public class LightSwitchTests
{
    private static readonly TimeSpan H7 = TimeSpan.FromHours(7), H19 = TimeSpan.FromHours(19);

    [Theory]
    [InlineData(6, 59, Theme.Dark)]
    [InlineData(7, 0, Theme.Light)]
    [InlineData(12, 0, Theme.Light)]
    [InlineData(18, 59, Theme.Light)]
    [InlineData(19, 0, Theme.Dark)]
    [InlineData(23, 30, Theme.Dark)]
    public void Resolves_day_schedule(int h, int m, Theme expected) =>
        Assert.Equal(expected, ThemeSchedule.Resolve(new TimeSpan(h, m, 0), H7, H19));

    [Theory] // night worker: light from 22:00, dark from 06:00 (schedule wraps midnight)
    [InlineData(23, 0, Theme.Light)]
    [InlineData(3, 0, Theme.Light)]
    [InlineData(12, 0, Theme.Dark)]
    public void Resolves_schedule_wrapping_midnight(int h, int m, Theme expected) =>
        Assert.Equal(expected, ThemeSchedule.Resolve(new TimeSpan(h, m, 0), TimeSpan.FromHours(22), TimeSpan.FromHours(6)));

    [Fact]
    public void Equal_times_mean_always_light() =>
        Assert.Equal(Theme.Light, ThemeSchedule.Resolve(TimeSpan.FromHours(3), H7, H7));

    [Fact]
    public void Next_change_is_today_or_tomorrow()
    {
        var day = new DateTime(2026, 11, 12);
        Assert.Equal(day.AddHours(19), ThemeSchedule.NextChange(day.AddHours(12), H7, H19));
        Assert.Equal(day.AddDays(1).AddHours(7), ThemeSchedule.NextChange(day.AddHours(20), H7, H19));
        Assert.Equal(day.AddHours(19), ThemeSchedule.NextChange(day.AddHours(7), H7, H19)); // exactly at a boundary -> next one
    }

    [Theory]
    [InlineData("07:30", 7, 30)]
    [InlineData("7:05", 7, 5)]
    public void Parses_valid_times(string text, int h, int m) =>
        Assert.Equal(new TimeSpan(h, m, 0), ThemeSchedule.ParseOrDefault(text, TimeSpan.Zero));

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("25:00")]
    [InlineData(null)]
    public void Invalid_times_fall_back(string? text) =>
        Assert.Equal(H7, ThemeSchedule.ParseOrDefault(text, H7));

    // Fake Windows: stores the current theme and records every change request.
    private sealed class Api(Theme current) : IThemeApi
    {
        public Theme Current = current;
        public readonly List<(Theme Theme, bool Apps, bool System)> Sets = new();
        public bool Watching, Unwatched;
        public Theme GetCurrent() => Current;
        public void Set(Theme t, bool apps, bool system) { Sets.Add((t, apps, system)); Current = t; }
        public IDisposable WatchClockChanges(Action onChange) { Watching = true; return new Unwatch(this); }
        private sealed class Unwatch(Api a) : IDisposable { public void Dispose() => a.Unwatched = true; }
    }

    private static LightSwitchModule Module(Api api, DateTime now, LightSwitchSettings? s = null) =>
        new(api, () => s ?? new LightSwitchSettings(), () => now);

    [Fact]
    public async Task Start_applies_theme_for_current_time_using_settings_flags()
    {
        var api = new Api(Theme.Dark);
        var m = Module(api, new DateTime(2026, 11, 12, 12, 0, 0), new LightSwitchSettings { ApplyToSystem = false });
        await m.StartAsync(new FakeContext(), CancellationToken.None);
        Assert.Equal((Theme.Light, true, false), api.Sets.Single());
        await m.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Does_not_touch_theme_when_already_correct()
    {
        var api = new Api(Theme.Light);
        var m = Module(api, new DateTime(2026, 11, 12, 12, 0, 0));
        await m.StartAsync(new FakeContext(), CancellationToken.None);
        m.Evaluate();
        Assert.Empty(api.Sets);
        await m.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Stop_releases_watcher_and_later_evaluations_do_nothing()
    {
        var api = new Api(Theme.Dark);
        var m = Module(api, new DateTime(2026, 11, 12, 12, 0, 0));
        await m.StartAsync(new FakeContext(), CancellationToken.None);
        await m.StopAsync(CancellationToken.None);
        api.Current = Theme.Dark; api.Sets.Clear();
        m.Evaluate();
        Assert.True(api.Unwatched);
        Assert.Empty(api.Sets);
    }
}
