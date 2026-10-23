using System.Globalization;
using System.Text.Json;
using TikTokMcpDesktop.Core.Store;
using TikTokMcpDesktop.Core.Tables;

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
                new() { Id = "kpi_spend", Type = "kpi", Title = "消耗合计", Metric = "spend_sum", ColSpan = 1 },
                new() { Id = "kpi_impressions", Type = "kpi", Title = "展示合计", Metric = "impressions_sum", ColSpan = 1 },
                new() { Id = "kpi_clicks", Type = "kpi", Title = "点击合计", Metric = "clicks_sum", ColSpan = 1 },
                new() { Id = "kpi_conv", Type = "kpi", Title = "转化合计", Metric = "conversion_sum", ColSpan = 1 },
                new() { Id = "pie_objective", Type = "chart-pie", Title = "消耗占比 · 投放目标", Metric = "spend_by_objective", ColSpan = 1 },
                new() { Id = "pie_campaign", Type = "chart-pie", Title = "消耗占比 · 广告系列", Metric = "spend_by_campaign", ColSpan = 1 },
                new() { Id = "line_spend", Type = "chart-line", Title = "每日消耗趋势", Metric = "spend_by_day", ColSpan = 2 },
                new() { Id = "table_report", Type = "data-table", Title = "综合报表预览", Metric = "report_preview", ColSpan = 2 }
            ]
        },
        new()
        {
            Id = "structure",
            Name = "投放结构",
            Widgets =
            [
                new() { Id = "kpi_adv", Type = "kpi", Title = "广告主", Metric = "advertiser_count", ColSpan = 1 },
                new() { Id = "kpi_camp", Type = "kpi", Title = "广告系列", Metric = "campaign_count", ColSpan = 1 },
                new() { Id = "kpi_ag", Type = "kpi", Title = "广告组", Metric = "adgroup_count", ColSpan = 1 },
                new() { Id = "kpi_ad", Type = "kpi", Title = "广告", Metric = "ad_count", ColSpan = 1 },
                new() { Id = "tree_hierarchy", Type = "chart-tree", Title = "账户树 · 广告主 → 系列 → 组 → 广告", Metric = "account_tree", ColSpan = 2 },
                new() { Id = "bar_status", Type = "chart-bar", Title = "广告系列状态分布", Metric = "campaign_status_bar", ColSpan = 1 },
                new() { Id = "pie_format", Type = "chart-pie", Title = "广告版式分布", Metric = "ad_format_pie", ColSpan = 1 },
                new() { Id = "table_campaigns", Type = "data-table", Title = "广告系列明细", Metric = "campaign_preview", ColSpan = 2 }
            ]
        },
        new()
        {
            Id = "performance",
            Name = "效果分析",
            Widgets =
            [
                new() { Id = "kpi_ctr", Type = "kpi", Title = "平均 CTR%", Metric = "avg_ctr", ColSpan = 1 },
                new() { Id = "kpi_cpc", Type = "kpi", Title = "平均 CPC", Metric = "avg_cpc", ColSpan = 1 },
                new() { Id = "kpi_cpa", Type = "kpi", Title = "平均 CPA", Metric = "avg_cpa", ColSpan = 1 },
                new() { Id = "kpi_roas_proxy", Type = "kpi", Title = "转化 / 千次展示", Metric = "conv_per_mille", ColSpan = 1 },
                new() { Id = "bar_spend_campaign", Type = "chart-bar", Title = "系列消耗对比", Metric = "spend_by_campaign", ColSpan = 2 },
                new() { Id = "line_clicks", Type = "chart-line", Title = "每日点击趋势", Metric = "clicks_by_day", ColSpan = 1 },
                new() { Id = "pie_adv_spend", Type = "chart-pie", Title = "广告主消耗占比", Metric = "spend_by_advertiser", ColSpan = 1 },
                new() { Id = "table_top", Type = "data-table", Title = "系列效果排行", Metric = "campaign_perf_table", ColSpan = 2 }
            ]
        },
        new()
        {
            Id = "accounts",
            Name = "账户与数据表",
            Widgets =
            [
                new() { Id = "kpi_tables", Type = "kpi", Title = "数据表数", Metric = "table_count", ColSpan = 1 },
                new() { Id = "kpi_records", Type = "kpi", Title = "总记录数", Metric = "record_count", ColSpan = 1 },
                new() { Id = "kpi_tools", Type = "kpi", Title = "最近同步工具数", Metric = "last_tool_count", ColSpan = 1 },
                new() { Id = "kpi_sync", Type = "kpi", Title = "最近同步状态", Metric = "last_sync_status", ColSpan = 1 },
                new() { Id = "pie_tables", Type = "chart-pie", Title = "各表记录占比", Metric = "records_by_table", ColSpan = 1 },
                new() { Id = "bar_tables", Type = "chart-bar", Title = "各表行数", Metric = "records_by_table", ColSpan = 1 },
                new() { Id = "table_preview", Type = "data-table", Title = "数据表一览", Metric = "tables", ColSpan = 2 },
                new() { Id = "table_accounts", Type = "data-table", Title = "广告主资料", Metric = "account_preview", ColSpan = 2 }
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
            Description = $"{p.Widgets.Count} 个组件 · MCP 只读数据"
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
            Message = empty ? "暂无数据，请先在「同步」页拉取广告数据，或启动 DevHost 加载演示样本。" : null
        };

        foreach (var w in layout.Widgets)
            data.Widgets[w.Id] = BuildWidget(w, tables, from, to);

        return data;
    }

    private object? BuildWidget(DashboardWidget w, IReadOnlyList<DataTableMeta> tables, DateTimeOffset? from, DateTimeOffset? to)
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
                name = t.Name,
                sourceTool = t.SourceTool,
                lastSyncedAt = t.LastSyncedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            }).ToList(),
            "spend_sum" => new { value = SumField("spend"), unit = "CNY" },
            "impressions_sum" => new { value = SumField("impressions"), unit = "" },
            "clicks_sum" => new { value = SumField("clicks"), unit = "" },
            "conversion_sum" => new { value = SumField("conversion"), unit = "" },
            "avg_ctr" => new { value = AvgField("ctr"), unit = "%" },
            "avg_cpc" => new { value = AvgField("cpc"), unit = "" },
            "avg_cpa" => new { value = AvgField("cpa"), unit = "" },
            "conv_per_mille" => new
            {
                value = Math.Round(SafeDiv(SumField("conversion"), SumField("impressions")) * 1000, 2),
                unit = ""
            },
            "spend_by_day" => AggregateReportBy("stat_time_day", "spend"),
            "clicks_by_day" => AggregateReportBy("stat_time_day", "clicks"),
            "spend_by_objective" => AggregateReportBy("objective_type", "spend"),
            "spend_by_campaign" => AggregateReportBy("campaign_name", "spend"),
            "spend_by_advertiser" => AggregateReportBy("advertiser_id", "spend"),
            "sync_by_day" => BuildSyncByDay(from, to),
            "advertiser_count" => new { value = CountRows("advertiser_info_get", "auth_advertiser_get"), unit = "" },
            "campaign_count" => new { value = CountRows("campaign_get", "smart_plus_campaign_get"), unit = "" },
            "adgroup_count" => new { value = CountRows("adgroup_get", "smart_plus_adgroup_get"), unit = "" },
            "ad_count" => new { value = CountRows("ad_get", "smart_plus_ad_get"), unit = "" },
            "account_tree" => BuildAccountTree(),
            "campaign_status_bar" => CountDistinctField("campaign_get", "operation_status"),
            "ad_format_pie" => CountDistinctField("ad_get", "ad_format"),
            "campaign_preview" => PreviewTool("campaign_get", 20),
            "report_preview" => PreviewTool("report_integrated_get", 12),
            "campaign_perf_table" => BuildCampaignPerfTable(),
            "account_table_count" => new { value = tables.Count(t => IsAccountish(t.Name) || IsAccountish(t.SourceTool)), unit = "张" },
            "account_record_count" => new
            {
                value = tables.Where(t => IsAccountish(t.Name) || IsAccountish(t.SourceTool))
                    .Sum(t => _db.QueryRecords(t.Id, null, 1, null, null, null, null, null).Total),
                unit = "条"
            },
            "account_preview" => PreviewTool("advertiser_info_get", 20),
            _ => null
        };
    }

    private static bool IsAccountish(string name) =>
        name.Contains("advertiser", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("account", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("广告主", StringComparison.OrdinalIgnoreCase);

    private object BuildRecordsByTable(IReadOnlyList<DataTableMeta> tables)
    {
        var labels = new List<string>();
        var values = new List<double>();
        foreach (var t in tables)
        {
            labels.Add(t.Name);
            values.Add(_db.QueryRecords(t.Id, null, 1, null, null, null, null, null).Total);
        }
        return new { labels, values };
    }

    private int CountRows(params string[] tools)
    {
        foreach (var tool in tools)
        {
            var t = FindTable(tool);
            if (t is null) continue;
            return _db.QueryRecords(t.Id, null, 1, null, null, null, null, null).Total;
        }
        return 0;
    }

    private DataTableMeta? FindTable(string tool) =>
        _db.ListTables().FirstOrDefault(t => t.SourceTool.Equals(tool, StringComparison.OrdinalIgnoreCase));

    private List<Dictionary<string, object?>> LoadRows(string tool, int limit = 5000)
    {
        var t = FindTable(tool);
        if (t is null) return [];
        var detail = _db.GetTable(t.Id);
        return _db.QueryRecords(t.Id, null, limit, null, null, null, null, null)
            .Items.Select(r =>
            {
                var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                if (detail is not null)
                {
                    foreach (var f in detail.Fields)
                    {
                        r.Values.TryGetValue(f.Id, out var v);
                        map[f.Name] = v;
                    }
                }
                else
                {
                    foreach (var (k, v) in r.Values)
                    {
                        var name = k.Contains(':') ? k[(k.IndexOf(':') + 1)..] : k;
                        map[name] = v;
                    }
                }
                return map;
            }).ToList();
    }

    private List<object> PreviewTool(string tool, int limit)
    {
        return LoadRows(tool, limit).Select(r => (object)NormalizeRow(r)).ToList();
    }

    private static Dictionary<string, object?> NormalizeRow(Dictionary<string, object?> row)
    {
        var map = new Dictionary<string, object?>();
        foreach (var (k, v) in row)
            map[k] = Unwrap(v);
        return map;
    }

    private object AggregateReportBy(string groupField, string metricField)
    {
        var rows = LoadRows("report_integrated_get");
        var buckets = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var key = StringOf(row, groupField);
            if (string.IsNullOrWhiteSpace(key)) key = "(空)";
            buckets[key] = buckets.GetValueOrDefault(key) + NumberOf(row, metricField);
        }
        var ordered = buckets.OrderByDescending(kv => kv.Value).Take(12).ToList();
        return new
        {
            labels = ordered.Select(kv => kv.Key).ToList(),
            values = ordered.Select(kv => Math.Round(kv.Value, 2)).ToList()
        };
    }

    private object CountDistinctField(string tool, string field)
    {
        var rows = LoadRows(tool);
        var buckets = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var key = StringOf(row, field);
            if (string.IsNullOrWhiteSpace(key)) key = "(空)";
            buckets[key] = buckets.GetValueOrDefault(key) + 1;
        }
        return new
        {
            labels = buckets.Keys.ToList(),
            values = buckets.Values.ToList()
        };
    }

    private object BuildAccountTree()
    {
        var advertisers = LoadRows("advertiser_info_get");
        if (advertisers.Count == 0) advertisers = LoadRows("auth_advertiser_get");
        var campaigns = LoadRows("campaign_get");
        var adgroups = LoadRows("adgroup_get");
        var ads = LoadRows("ad_get");

        var rootChildren = new List<object>();
        foreach (var adv in advertisers)
        {
            var advId = StringOf(adv, "advertiser_id");
            var advName = FirstNonEmpty(StringOf(adv, "name"), StringOf(adv, "advertiser_name"), advId);
            var campNodes = new List<object>();
            foreach (var camp in campaigns.Where(c => StringOf(c, "advertiser_id") == advId))
            {
                var campId = StringOf(camp, "campaign_id");
                var campName = FirstNonEmpty(StringOf(camp, "campaign_name"), campId);
                var agNodes = new List<object>();
                foreach (var ag in adgroups.Where(a => StringOf(a, "campaign_id") == campId))
                {
                    var agId = StringOf(ag, "adgroup_id");
                    var agName = FirstNonEmpty(StringOf(ag, "adgroup_name"), agId);
                    var adNodes = ads.Where(a => StringOf(a, "adgroup_id") == agId)
                        .Select(a => (object)new
                        {
                            name = FirstNonEmpty(StringOf(a, "ad_name"), StringOf(a, "ad_id")),
                            value = 1
                        }).ToList();
                    agNodes.Add(new { name = agName, children = adNodes });
                }
                campNodes.Add(new { name = campName, children = agNodes });
            }
            rootChildren.Add(new { name = advName, children = campNodes });
        }

        return new
        {
            name = "TikTok Ads",
            children = rootChildren
        };
    }

    private List<object> BuildCampaignPerfTable()
    {
        var rows = LoadRows("report_integrated_get");
        var map = new Dictionary<string, (string Name, string Objective, double Spend, double Imp, double Clicks, double Conv)>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var id = StringOf(row, "campaign_id");
            if (string.IsNullOrWhiteSpace(id)) continue;
            var cur = map.GetValueOrDefault(id);
            map[id] = (
                FirstNonEmpty(StringOf(row, "campaign_name"), id),
                StringOf(row, "objective_type"),
                cur.Spend + NumberOf(row, "spend"),
                cur.Imp + NumberOf(row, "impressions"),
                cur.Clicks + NumberOf(row, "clicks"),
                cur.Conv + NumberOf(row, "conversion")
            );
        }

        return map.Values
            .OrderByDescending(x => x.Spend)
            .Select(x => (object)new Dictionary<string, object?>
            {
                ["campaign"] = x.Name,
                ["objective"] = x.Objective,
                ["spend"] = Math.Round(x.Spend, 2),
                ["impressions"] = (long)x.Imp,
                ["clicks"] = (long)x.Clicks,
                ["conversion"] = (long)x.Conv,
                ["ctr"] = Math.Round(SafeDiv(x.Clicks, x.Imp) * 100, 2),
                ["cpa"] = Math.Round(SafeDiv(x.Spend, x.Conv), 2)
            }).ToList();
    }

    private double SumField(string field)
    {
        // Prefer report table; fall back to scanning all tables for matching field names.
        var report = LoadRows("report_integrated_get");
        if (report.Count > 0)
            return Math.Round(report.Sum(r => NumberOf(r, field)), 2);
        return SumNumericFields([field, "stat_cost", "cost", "cash_spend", "show_cnt", "show"]);
    }

    private double AvgField(string field)
    {
        var report = LoadRows("report_integrated_get");
        if (report.Count == 0) return 0;
        return Math.Round(report.Average(r => NumberOf(r, field)), 2);
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
                sum += ToDouble(v);
            }
        }
        return Math.Round(sum, 2);
    }

    private object BuildSyncByDay(DateTimeOffset? from, DateTimeOffset? to)
    {
        var labels = new List<string>();
        var values = new List<double>();
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

    private int CountAllRecords()
    {
        lock (_db.SyncRoot)
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM data_records";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
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

    private static string StringOf(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var v) || v is null) return "";
        return Unwrap(v)?.ToString() ?? "";
    }

    private static double NumberOf(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var v) || v is null) return 0;
        return ToDouble(v);
    }

    private static object? Unwrap(object? v)
    {
        if (v is JsonElement je) return JsonElementToObject(je);
        return v;
    }

    private static object? JsonElementToObject(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => el.GetRawText()
    };

    private static double ToDouble(object? v)
    {
        v = Unwrap(v);
        if (v is null) return 0;
        if (v is double d) return d;
        if (v is float f) return f;
        if (v is decimal m) return (double)m;
        if (v is long l) return l;
        if (v is int i) return i;
        if (v is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Number) return je.GetDouble();
            if (je.ValueKind == JsonValueKind.String &&
                double.TryParse(je.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }
        return double.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : 0;
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

    private static double SafeDiv(double a, double b) => b == 0 ? 0 : a / b;
}
