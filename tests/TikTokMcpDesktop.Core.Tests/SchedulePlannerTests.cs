using TikTokMcpDesktop.Core.Config;
using TikTokMcpDesktop.Core.Jobs;

namespace TikTokMcpDesktop.Core.Tests;

public class SchedulePlannerTests
{
    [Fact]
    public void DailyAt_Before_Slot_Uses_Today()
    {
        var now = new DateTimeOffset(2026, 10, 23, 8, 0, 0, TimeSpan.FromHours(8));
        var next = SchedulePlanner.NextDailyAt("09:30", now);
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 9, 30, 0, TimeSpan.FromHours(8)), next);
    }

    [Fact]
    public void DailyAt_After_Slot_Uses_Tomorrow()
    {
        var now = new DateTimeOffset(2026, 10, 23, 10, 0, 0, TimeSpan.FromHours(8));
        var next = SchedulePlanner.NextDailyAt("09:30", now);
        Assert.Equal(new DateTimeOffset(2026, 10, 24, 9, 30, 0, TimeSpan.FromHours(8)), next);
    }

    [Fact]
    public void Interval_From_LastRun()
    {
        var settings = new AppSettings
        {
            AutoSyncEnabled = true,
            ScheduleMode = ScheduleMode.Interval,
            SyncIntervalMinutes = 60
        };
        var now = new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
        var last = now.AddMinutes(-10);
        var next = SchedulePlanner.ComputeNextRun(settings, now, last);
        Assert.Equal(last.AddMinutes(60), next);
    }

    [Fact]
    public void Disabled_Yields_No_Next()
    {
        var settings = new AppSettings { AutoSyncEnabled = false };
        var next = SchedulePlanner.ComputeNextRun(settings, DateTimeOffset.Now, null);
        Assert.Equal(DateTimeOffset.MaxValue, next);
    }

    [Theory]
    [InlineData("9:05", true)]
    [InlineData("09:05", true)]
    [InlineData("25:00", false)]
    [InlineData("09:60", false)]
    [InlineData("", false)]
    [InlineData("9", false)]
    public void ParseDailyAt(string input, bool ok)
    {
        Assert.Equal(ok, SchedulePlanner.TryParseDailyAt(input, out _, out _));
    }

    [Fact]
    public void NormalizeDailyAt_Pads_Hour()
    {
        Assert.Equal("09:05", SchedulePlanner.NormalizeDailyAt("9:05"));
        Assert.Equal("09:00", SchedulePlanner.NormalizeDailyAt("bad"));
    }
}
