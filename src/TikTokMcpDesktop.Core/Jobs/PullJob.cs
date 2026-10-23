using TikTokMcpDesktop.Core.Config;
using TikTokMcpDesktop.Core.Logging;
using TikTokMcpDesktop.Core.Mcp;
using TikTokMcpDesktop.Core.Store;
using TikTokMcpDesktop.Core.Tables;

namespace TikTokMcpDesktop.Core.Jobs;

public sealed class PullResult
{
    public bool Success { get; set; }
    public string RunId { get; set; } = "";
    public int ToolCount { get; set; }
    public int RecordCount { get; set; }
    public string? Message { get; set; }
}

public sealed class PullJob
{
    private readonly McpClient _mcp;
    private readonly ConfigStore _config;
    private readonly DataStore _db;
    private readonly TableIngestService _ingest;
    private readonly AppPaths _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public event Action? DataChanged;

    public PullJob(McpClient mcp, ConfigStore config, DataStore db, TableIngestService ingest, AppPaths paths)
    {
        _mcp = mcp;
        _config = config;
        _db = db;
        _ingest = ingest;
        _paths = paths;
    }

    public async Task<PullResult> RunAsync(CancellationToken ct = default)
    {
        if (!await _gate.WaitAsync(0, ct))
            return new PullResult { Success = false, Message = "同步正在进行中" };

        var runId = Guid.NewGuid().ToString("N");
        var started = DateTimeOffset.Now;
        _db.InsertSyncRun(runId, started, "running");
        AppLog.Info($"Pull started {runId}");

        try
        {
            var settings = _config.Load();
            var tools = await _mcp.ListToolsAsync(settings, ct);
            var selected = ToolWhitelist.FilterForPull(tools);
            if (selected.Count == 0)
                throw new InvalidOperationException("没有可用的只读拉数工具。");

            var pullDir = Path.Combine(_paths.PullsDir, DateTime.Now.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(pullDir);

            var totalRecords = 0;
            var toolCount = 0;
            foreach (var tool in selected)
            {
                ct.ThrowIfCancellationRequested();
                _db.AddSyncLog(runId, "info", $"Calling {tool.Name}");
                try
                {
                    // Prefer empty args; many list tools work with defaults
                    var result = await _mcp.CallToolAsync(settings, tool.Name, new { }, ct);
                    var (_, count) = _ingest.IngestToolResult(tool.Name, result, pullDir);
                    totalRecords += count;
                    toolCount++;
                    _db.AddSyncLog(runId, "info", $"{tool.Name} -> {count} rows");
                }
                catch (Exception ex)
                {
                    _db.AddSyncLog(runId, "warn", $"{tool.Name} failed: {ex.Message}");
                    AppLog.Warn($"Pull tool {tool.Name}: {ex.Message}");
                }

                await Task.Delay(250, ct); // soft rate limit
            }

            var msg = $"同步完成：{toolCount} 个工具，{totalRecords} 条记录";
            _db.FinishSyncRun(runId, DateTimeOffset.Now, "success", toolCount, totalRecords, msg);
            AppLog.Info(msg);
            DataChanged?.Invoke();
            return new PullResult
            {
                Success = true,
                RunId = runId,
                ToolCount = toolCount,
                RecordCount = totalRecords,
                Message = msg
            };
        }
        catch (Exception ex)
        {
            _db.FinishSyncRun(runId, DateTimeOffset.Now, "error", 0, 0, ex.Message);
            _db.AddSyncLog(runId, "error", ex.Message);
            AppLog.Error($"Pull failed: {ex.Message}");
            return new PullResult { Success = false, RunId = runId, Message = ex.Message };
        }
        finally
        {
            _gate.Release();
        }
    }
}
