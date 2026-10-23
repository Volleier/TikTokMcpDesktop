using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace TikTokMcpDesktop.Core.Logging;

public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Error
}

public sealed record LogEntry(DateTimeOffset Timestamp, LogLevel Level, string Message);

public static class AppLog
{
    private static readonly ConcurrentQueue<LogEntry> Entries = new();
    private static readonly object FileLock = new();
    private static string? _logFilePath;
    private static readonly Regex SecretPattern = new(
        @"(Bearer\s+)[A-Za-z0-9\-._~+/]+=*|(access_token|refresh_token|api[_-]?key|code_verifier|client_secret)([""'=:\s]+)([^\s""'&,}]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static event Action<LogEntry>? OnEntry;

    public static void Configure(string logFilePath)
    {
        _logFilePath = logFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);
    }

    public static IReadOnlyList<LogEntry> Snapshot(int max = 200)
    {
        return Entries.Reverse().Take(max).Reverse().ToList();
    }

    public static void Info(string message) => Write(LogLevel.Info, message);
    public static void Warn(string message) => Write(LogLevel.Warn, message);
    public static void Error(string message) => Write(LogLevel.Error, message);
    public static void Debug(string message) => Write(LogLevel.Debug, message);

    public static string Redact(string input) =>
        SecretPattern.Replace(input, m =>
        {
            if (m.Groups[1].Success)
                return m.Groups[1].Value + "***";
            return m.Groups[2].Value + m.Groups[3].Value + "***";
        });

    private static void Write(LogLevel level, string message)
    {
        var entry = new LogEntry(DateTimeOffset.Now, level, Redact(message));
        Entries.Enqueue(entry);
        while (Entries.Count > 1000 && Entries.TryDequeue(out _)) { }

        OnEntry?.Invoke(entry);

        if (_logFilePath is null) return;
        lock (FileLock)
        {
            File.AppendAllText(_logFilePath, $"{entry.Timestamp:O} [{entry.Level}] {entry.Message}{Environment.NewLine}");
        }
    }
}
