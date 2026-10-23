using TikTokMcpDesktop.Core.Logging;
using TikTokMcpDesktop.Core.Store;

namespace TikTokMcpDesktop.Core.Jobs;

public sealed class PullScheduler : IAsyncDisposable
{
    private readonly PullJob _job;
    private readonly ConfigStore _config;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public PullScheduler(PullJob job, ConfigStore config)
    {
        _job = job;
        _config = config;
    }

    public void Start()
    {
        Stop();
        _cts = new CancellationTokenSource();
        _loop = RunLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        if (_cts is null) return;
        _cts.Cancel();
        _cts.Dispose();
        _cts = null;
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var settings = _config.Load();
                var minutes = Math.Clamp(settings.SyncIntervalMinutes, 5, 24 * 60);
                await Task.Delay(TimeSpan.FromMinutes(minutes), ct);
                if (!_config.Load().AutoSyncEnabled) continue;
                AppLog.Info("Scheduled pull triggered");
                await _job.RunAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Scheduler error: {ex.Message}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        Stop();
        if (_loop is not null)
        {
            try { await _loop; } catch { /* ignore */ }
        }
    }
}
