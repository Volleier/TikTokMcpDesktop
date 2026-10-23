using TikTokMcpDesktop.Core.Config;
using TikTokMcpDesktop.Core.Logging;
using TikTokMcpDesktop.Core.Store;

namespace TikTokMcpDesktop.Core.Jobs;

public sealed class SchedulerStatus
{
    public bool Enabled { get; init; }
    public bool IsLoopRunning { get; init; }
    public bool IsJobRunning { get; init; }
    public ScheduleMode Mode { get; init; }
    public DateTimeOffset? LastRunAt { get; init; }
    public DateTimeOffset? NextRunAt { get; init; }
    public string Summary { get; init; } = "";
}

/// <summary>
/// Background auto-sync scheduler: interval or daily-at-local-time.
/// Survives settings changes via <see cref="Restart"/>.
/// </summary>
public sealed class PullScheduler : IAsyncDisposable
{
    private readonly PullJob _job;
    private readonly ConfigStore _config;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private DateTimeOffset? _lastRunAt;
    private DateTimeOffset? _nextRunAt;
    private bool _jobRunning;

    public event Action<SchedulerStatus>? StatusChanged;

    public PullScheduler(PullJob job, ConfigStore config)
    {
        _job = job;
        _config = config;
    }

    public SchedulerStatus GetStatus()
    {
        var settings = _config.Load();
        lock (_gate)
        {
            return BuildStatus(settings, _cts is not null);
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            StopUnlocked();
            _cts = new CancellationTokenSource();
            _loop = RunLoopAsync(_cts.Token);
        }
        Publish();
        AppLog.Info("PullScheduler started");
    }

    public void Restart() => Start();

    public void Stop()
    {
        lock (_gate)
        {
            StopUnlocked();
        }
        Publish();
        AppLog.Info("PullScheduler stopped");
    }

    /// <summary>Force next interval countdown from now (e.g. after manual sync).</summary>
    public void MarkRunCompleted(DateTimeOffset? when = null)
    {
        lock (_gate)
        {
            _lastRunAt = when ?? DateTimeOffset.Now;
            var settings = _config.Load();
            _nextRunAt = SchedulePlanner.ComputeNextRun(settings, DateTimeOffset.Now, _lastRunAt);
        }
        Publish();
    }

    private void StopUnlocked()
    {
        if (_cts is null) return;
        try { _cts.Cancel(); } catch { /* ignore */ }
        _cts.Dispose();
        _cts = null;
        _nextRunAt = null;
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var settings = _config.Load();
                if (!settings.AutoSyncEnabled)
                {
                    lock (_gate) { _nextRunAt = null; }
                    Publish();
                    // Idle poll so enabling later is picked up without full restart
                    await Task.Delay(TimeSpan.FromSeconds(15), ct);
                    continue;
                }

                DateTimeOffset? last;
                lock (_gate) { last = _lastRunAt; }
                var now = DateTimeOffset.Now;
                var delay = SchedulePlanner.DelayUntilNext(settings, now, last);
                var next = SchedulePlanner.ComputeNextRun(settings, now, last);
                lock (_gate) { _nextRunAt = next; }
                Publish();

                if (delay == Timeout.InfiniteTimeSpan)
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), ct);
                    continue;
                }

                // Cap single wait so settings changes apply within 30s
                var slice = delay > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : delay;
                if (slice > TimeSpan.Zero)
                    await Task.Delay(slice, ct);

                // Re-evaluate after wake
                settings = _config.Load();
                if (!settings.AutoSyncEnabled) continue;

                now = DateTimeOffset.Now;
                lock (_gate) { last = _lastRunAt; }
                next = SchedulePlanner.ComputeNextRun(settings, now, last);
                if (next > now.AddSeconds(2))
                {
                    lock (_gate) { _nextRunAt = next; }
                    Publish();
                    continue;
                }

                AppLog.Info($"Scheduled pull triggered ({settings.ScheduleMode})");
                lock (_gate) { _jobRunning = true; }
                Publish();
                try
                {
                    var result = await _job.RunAsync(ct);
                    AppLog.Info(result.Message ?? (result.Success ? "scheduled pull ok" : "scheduled pull failed"));
                }
                finally
                {
                    lock (_gate)
                    {
                        _jobRunning = false;
                        _lastRunAt = DateTimeOffset.Now;
                        _nextRunAt = SchedulePlanner.ComputeNextRun(_config.Load(), DateTimeOffset.Now, _lastRunAt);
                    }
                    Publish();
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Scheduler error: {ex.Message}");
                try { await Task.Delay(TimeSpan.FromSeconds(10), ct); } catch { /* ignore */ }
            }
        }
    }

    private void Publish()
    {
        try
        {
            StatusChanged?.Invoke(GetStatus());
        }
        catch (Exception ex)
        {
            AppLog.Debug($"StatusChanged handler error: {ex.Message}");
        }
    }

    private SchedulerStatus BuildStatus(AppSettings settings, bool loopRunning)
    {
        return new SchedulerStatus
        {
            Enabled = settings.AutoSyncEnabled,
            IsLoopRunning = loopRunning,
            IsJobRunning = _jobRunning,
            Mode = settings.ScheduleMode,
            LastRunAt = _lastRunAt,
            NextRunAt = settings.AutoSyncEnabled ? _nextRunAt : null,
            Summary = SchedulePlanner.Describe(settings, _nextRunAt, _lastRunAt)
        };
    }

    public async ValueTask DisposeAsync()
    {
        Task? loop;
        lock (_gate)
        {
            StopUnlocked();
            loop = _loop;
            _loop = null;
        }
        if (loop is not null)
        {
            try { await loop; } catch { /* ignore */ }
        }
    }
}
