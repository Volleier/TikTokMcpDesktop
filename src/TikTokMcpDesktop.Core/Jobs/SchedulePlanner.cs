using System.Globalization;
using TikTokMcpDesktop.Core.Config;

namespace TikTokMcpDesktop.Core.Jobs;

/// <summary>Pure schedule math for auto sync (interval or daily local time).</summary>
public static class SchedulePlanner
{
    public static DateTimeOffset ComputeNextRun(AppSettings settings, DateTimeOffset nowLocal, DateTimeOffset? lastRunLocal = null)
    {
        if (!settings.AutoSyncEnabled)
            return DateTimeOffset.MaxValue;

        return settings.ScheduleMode switch
        {
            ScheduleMode.DailyAt => NextDailyAt(settings.DailyAtLocalTime, nowLocal),
            _ => NextInterval(settings.SyncIntervalMinutes, nowLocal, lastRunLocal)
        };
    }

    public static TimeSpan DelayUntilNext(AppSettings settings, DateTimeOffset nowLocal, DateTimeOffset? lastRunLocal = null)
    {
        var next = ComputeNextRun(settings, nowLocal, lastRunLocal);
        if (next == DateTimeOffset.MaxValue)
            return Timeout.InfiniteTimeSpan;
        var delay = next - nowLocal;
        return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
    }

    private static DateTimeOffset NextInterval(int minutes, DateTimeOffset now, DateTimeOffset? lastRun)
    {
        minutes = Math.Clamp(minutes, 1, 24 * 60);
        if (lastRun is null)
        {
            // First schedule after enable: wait one full interval (avoid surprise immediate pull on every restart).
            // Callers that want "run soon" can pass lastRun = now - interval.
            return now.AddMinutes(minutes);
        }

        var due = lastRun.Value.AddMinutes(minutes);
        return due <= now ? now : due;
    }

    internal static DateTimeOffset NextDailyAt(string? hhmm, DateTimeOffset nowLocal)
    {
        if (!TryParseDailyAt(hhmm, out var hour, out var minute))
        {
            hour = 9;
            minute = 0;
        }

        var today = new DateTimeOffset(nowLocal.Year, nowLocal.Month, nowLocal.Day, hour, minute, 0, nowLocal.Offset);
        return nowLocal < today ? today : today.AddDays(1);
    }

    public static bool TryParseDailyAt(string? hhmm, out int hour, out int minute)
    {
        hour = 0;
        minute = 0;
        if (string.IsNullOrWhiteSpace(hhmm)) return false;
        var parts = hhmm.Trim().Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out hour)) return false;
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minute)) return false;
        return hour is >= 0 and <= 23 && minute is >= 0 and <= 59;
    }

    /// <summary>Normalize to HH:mm; falls back to 09:00 when invalid.</summary>
    public static string NormalizeDailyAt(string? hhmm)
    {
        if (!TryParseDailyAt(hhmm, out var hour, out var minute))
            return "09:00";
        return $"{hour:D2}:{minute:D2}";
    }

    public static string Describe(AppSettings settings, DateTimeOffset? nextRun, DateTimeOffset? lastRun)
    {
        if (!settings.AutoSyncEnabled)
            return "定时调度：已关闭";

        var mode = settings.ScheduleMode == ScheduleMode.DailyAt
            ? $"每日 {settings.DailyAtLocalTime}（本地时区）"
            : $"每 {Math.Clamp(settings.SyncIntervalMinutes, 1, 1440)} 分钟";

        var next = nextRun is null || nextRun == DateTimeOffset.MaxValue
            ? "下次：—"
            : $"下次：{nextRun:yyyy-MM-dd HH:mm:ss}";
        var last = lastRun is null ? "上次：—" : $"上次：{lastRun:yyyy-MM-dd HH:mm:ss}";
        return $"定时调度：开启 · {mode} · {next} · {last}";
    }
}
