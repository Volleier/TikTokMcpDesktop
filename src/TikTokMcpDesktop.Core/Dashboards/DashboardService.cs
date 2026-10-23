using System.Globalization;
using System.Text.Json;
using TikTokMcpDesktop.Core.Store;

namespace TikTokMcpDesktop.Core.Dashboards;

public sealed class DashboardService
{
    private readonly DataStore _db;

    private static readonly List<DashboardLayout> Presets =
    [
        new()
        {
            Id = "overview",
            Name = "广告概览",
            Widgets =
            [
                new() { Id = "kpi_tables", Type = "kpi", Title = "数据表数", Metric = "table_count", ColSpan = 1 },
                new() { Id = "kpi_records", Type = "kpi", Title = "总记录数", Metric = "record_count", ColSpan = 1 },
                new() { Id = "kpi_tools", Type = "kpi", Title = "最近同步工具数", Metric = "last_tool_count", ColSpan = 1 },
                new() { Id = "kpi_sync", Type = "kpi", Title = "最近同步状态", Metric = "last_sync_status", ColSpan = 1 },
                new() { Id = "pie_tables", Type = "chart-pie", Title = "各表记录占比", Metric = "records_by_table", ColSpan = 2 },
                new() { Id = "table_preview", Type = "data-table", Title = "数据表一览", Metric = "tables", ColSpan = 2 }
            ]
        },
        new()
        {
            Id = "trend",
            Name = "消耗趋势",
            Widgets =
            [
                new() { Id = "kpi_spend", Type = "kpi", Title = "估算消耗字段合计", Metric = "spend_sum", ColSpan = 1 },
                new() { Id = "kpi_impressions", Type = "kpi", Title = "估算展示合计", Metric = "impressions_sum", ColSpan = 1 },
                new() { Id = "line_sync", Type = "chart-line", Title = "同步记录趋势（按日）", Metric = "sync_by_day", ColSpan = 2 },
                new() { Id = "bar_tables", Type = "chart-bar", Title = "各表行数", Metric = "records_by_table", ColSpan = 2 }
            ]
        },
        new()
        {
            Id = "accounts",
            Name = "账户明细",
            Widgets =
            [
                new() { Id = "kpi_account_tables", Type = "kpi", Title = "账户相关表", Metric = "account_table_count", ColSpan = 1 },
                new() { Id = "kpi_account_rows", Type = "kpi", Title = "账户相关行", Metric = "account_record_count", ColSpan = 1 },
                new() { Id = "table_accounts", Type = "data-table", Title = "账户/广告主相关数据预览", Metric = "account_preview", ColSpan = 2 }
            ]
        }
    ];

    public DashboardService(DataStore db)
    {
        _db = db;
    }

    public IReadOnlyList<DashboardSummary> List() =>
        Presets.Select(p => new DashboardSummary
        {
            Id = p.Id,
            Name = p.Name,
            Description = $"{p.Widgets.Count} 个组件"
        }).ToList();

    public DashboardLayout? GetLayout(string id) =>
        Presets.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public DashboardData GetData(string id, DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        var layout = GetLayout(id) ?? throw new InvalidOperationException("看板不存在");
        var tables = _db.ListTables();
        var empty = tables.Count == 0;
        var data = new DashboardData
        {
            Id = id,
            Empty = empty,
            Message = empty ? "暂无数据，请先在「同步」页拉取广告数据。" : null
        };

        foreach (var w in layout.Widgets)
        {
            data.Widgets[w.Id] = BuildWidget(w, tables, from, to);
        }

        return data;
    }

    private object? BuildWidget(DashboardWidget w, IReadOnlyList<Tables.DataTableMeta> tables, DateTimeOffset? from, DateTimeOffset? to)
    {
        return w.Metric switch
        {
            "table_count" => new { value = tables.Count, unit = "张" },
            "record_count" => new { value = CountAllRecords(), unit = "条" },
            "last_tool_count" => new { value = GetLatestInt("tool_count"), unit = "" },
            "last_sync_status" => new { value = GetLatestStatus(), unit = "" },
            "records_by_table" => BuildRecordsByTable(tables),
            "tables" => tables.Select(t => new
            {
                id = t.Id,
                name = t.Name,
                sourceTool = t.SourceTool,
                lastSyncedAt = t.LastSyncedAt?.ToString("O")
            }).ToList(),
            "spend_sum" => new { value = SumNumericFields(["spend", "cost", "stat_cost", "cash_spend"]), unit = "" },
            "impressions_sum" => new { value = SumNumericFields(["impressions", "show_cnt", "show"]), unit = "" },
            "sync_by_day" => BuildSyncByDay(from, to),
            "account_table_count" => new { value = tables.Count(t => IsAccountish(t.Name) || IsAccountish(t.SourceTool)), unit = "张" },
            "account_record_count" => new
            {
                value = tables.Where(t => IsAccountish(t.Name) || IsAccountish(t.SourceTool))
                    .Sum(t => _db.QueryRecords(t.Id, null, 1, null, null, null, null, null).Total),
                unit = "条"
            },
            "account_preview" => BuildAccountPreview(tables),
            _ => null
        };
    }

    private static bool IsAccountish(string name) =>
        name.Contains("advertiser", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("account", StringComparison.OrdinalIgnoreCase);

    private object BuildRecordsByTable(IReadOnlyList<Tables.DataTableMeta> tables)
    {
        var labels = new List<string>();
        var values = new List<int>();
        foreach (var t in tables)
        {
            labels.Add(t.Name);
            values.Add(_db.QueryRecords(t.Id, null, 1, null, null, null, null, null).Total);
        }
        return new { labels, values };
    }

    private List<object> BuildAccountPreview(IReadOnlyList<Tables.DataTableMeta> tables)
    {
        var target = tables.FirstOrDefault(t => IsAccountish(t.Name) || IsAccountish(t.SourceTool))
                     ?? tables.FirstOrDefault();
        if (target is null) return new List<object>();
        var page = _db.QueryRecords(target.Id, null, 20, null, null, null, null, null);
        return page.Items.Select(r => (object)r.Values).ToList();
    }

    private int CountAllRecords()
    {
        lock (_db.SyncRoot)
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM data_records";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    private double SumNumericFields(string[] fieldNames)
    {
        double sum = 0;
        foreach (var table in _db.ListTables())
        {
            var detail = _db.GetTable(table.Id);
            if (detail is null) continue;
            var match = detail.Fields.FirstOrDefault(f =>
                fieldNames.Any(n => f.Name.Contains(n, StringComparison.OrdinalIgnoreCase)));
            if (match is null) continue;

            var page = _db.QueryRecords(table.Id, null, 5000, null, null, null, null, null);
            foreach (var rec in page.Items)
            {
                if (!rec.Values.TryGetValue(match.Id, out var v) || v is null) continue;
                if (v is JsonElement je)
                {
                    if (je.ValueKind == JsonValueKind.Number) sum += je.GetDouble();
                    else if (je.ValueKind == JsonValueKind.String &&
                             double.TryParse(je.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                        sum += d;
                }
                else if (v is IConvertible)
                {
                    try { sum += Convert.ToDouble(v, CultureInfo.InvariantCulture); } catch { /* ignore */ }
                }
                else if (double.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                {
                    sum += parsed;
                }
            }
        }
        return Math.Round(sum, 2);
    }

    private object BuildSyncByDay(DateTimeOffset? from, DateTimeOffset? to)
    {
        var labels = new List<string>();
        var values = new List<int>();
        lock (_db.SyncRoot)
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = """
                SELECT substr(started_at,1,10) AS day, COUNT(*) 
                FROM sync_runs
                GROUP BY day
                ORDER BY day
                LIMIT 30
                """;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var day = reader.GetString(0);
                if (from is not null && DateTimeOffset.TryParse(day, out var d) && d < from) continue;
                if (to is not null && DateTimeOffset.TryParse(day, out var d2) && d2 > to) continue;
                labels.Add(day);
                values.Add(reader.GetInt32(1));
            }
        }
        return new { labels, values };
    }

    private int GetLatestInt(string field)
    {
        var latest = _db.GetLatestSync();
        if (latest is null) return 0;
        var json = JsonSerializer.Serialize(latest);
        using var doc = JsonDocument.Parse(json);
        var prop = field switch
        {
            "tool_count" => "toolCount",
            "record_count" => "recordCount",
            _ => field
        };
        return doc.RootElement.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.Number
            ? el.GetInt32()
            : 0;
    }

    private string GetLatestStatus()
    {
        var latest = _db.GetLatestSync();
        if (latest is null) return "无";
        var json = JsonSerializer.Serialize(latest);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("status", out var el) ? el.GetString() ?? "无" : "无";
    }
}
