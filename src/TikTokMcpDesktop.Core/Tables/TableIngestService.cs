using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TikTokMcpDesktop.Core.Store;

namespace TikTokMcpDesktop.Core.Tables;

public sealed class TableIngestService
{
    private readonly DataStore _db;

    public TableIngestService(DataStore db)
    {
        _db = db;
    }

    public (DataTableMeta table, int recordCount) IngestToolResult(string toolName, JsonElement result, string? pullDir = null)
    {
        if (pullDir is not null)
        {
            Directory.CreateDirectory(pullDir);
            var path = Path.Combine(pullDir, $"{Sanitize(toolName)}.json");
            File.WriteAllText(path, result.GetRawText());
        }

        var rows = FlattenToRows(result);
        var tableId = StableId($"table:{toolName}");
        var now = DateTimeOffset.UtcNow;

        var fieldNames = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
            foreach (var key in row.Keys)
                fieldNames.Add(key);

        if (fieldNames.Count == 0)
            fieldNames.Add("value");

        var fields = fieldNames.Select((name, i) => new DataField
        {
            Id = name,
            TableId = tableId,
            Name = name,
            Type = InferType(rows, name),
            Width = Math.Clamp(name.Length * 10 + 40, 100, 280),
            Visible = true,
            Sort = i
        }).ToList();

        var records = new List<DataRecord>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var values = new Dictionary<string, object?>();
            foreach (var f in fields)
                values[f.Id] = row.TryGetValue(f.Id, out var v) ? v : null;

            records.Add(new DataRecord
            {
                Id = StableId($"{tableId}:{i}:{JsonSerializer.Serialize(values)}"),
                TableId = tableId,
                Values = values,
                UpdatedAt = now
            });
        }

        var meta = new DataTableMeta
        {
            Id = tableId,
            Name = toolName,
            SourceTool = toolName,
            LastSyncedAt = now
        };
        _db.UpsertTable(meta, fields, records);
        return (meta, records.Count);
    }

    public string ExportCsv(string tableId, string exportDir)
    {
        var detail = _db.GetTable(tableId) ?? throw new InvalidOperationException("表不存在");
        var page = _db.QueryRecords(tableId, null, 10000, null, null, null, null, null);
        Directory.CreateDirectory(exportDir);
        var path = Path.Combine(exportDir, $"{Sanitize(detail.Table.Name)}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

        using var sw = new StreamWriter(path, false, new UTF8Encoding(true));
        var cols = detail.Fields.Where(f => f.Visible).OrderBy(f => f.Sort).ToList();
        sw.WriteLine(string.Join(",", cols.Select(c => CsvEscape(c.Name))));
        foreach (var rec in page.Items)
        {
            sw.WriteLine(string.Join(",", cols.Select(c =>
            {
                rec.Values.TryGetValue(c.Id, out var v);
                return CsvEscape(v?.ToString() ?? "");
            })));
        }
        return path;
    }

    internal static List<Dictionary<string, object?>> FlattenToRows(JsonElement result)
    {
        var rows = new List<Dictionary<string, object?>>();

        if (TryFindArray(result, out var arr))
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                    rows.Add(ObjectToRow(item));
                else
                    rows.Add(new Dictionary<string, object?> { ["value"] = JsonElementToObject(item) });
            }
            return rows;
        }

        if (result.ValueKind == JsonValueKind.Object)
        {
            // content[0].text may be MCP content wrapper
            if (result.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in content.EnumerateArray())
                {
                    if (part.ValueKind == JsonValueKind.Object &&
                        part.TryGetProperty("text", out var textEl) &&
                        textEl.ValueKind == JsonValueKind.String)
                    {
                        var text = textEl.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            try
                            {
                                using var inner = JsonDocument.Parse(text);
                                return FlattenToRows(inner.RootElement);
                            }
                            catch
                            {
                                rows.Add(new Dictionary<string, object?> { ["text"] = text });
                            }
                        }
                    }
                }
                if (rows.Count > 0) return rows;
            }

            rows.Add(ObjectToRow(result));
            return rows;
        }

        rows.Add(new Dictionary<string, object?> { ["value"] = JsonElementToObject(result) });
        return rows;
    }

    private static bool TryFindArray(JsonElement el, out JsonElement arr)
    {
        if (el.ValueKind == JsonValueKind.Array)
        {
            arr = el;
            return true;
        }
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in new[] { "list", "data", "items", "records", "rows", "result" })
            {
                if (el.TryGetProperty(name, out var child))
                {
                    if (child.ValueKind == JsonValueKind.Array)
                    {
                        arr = child;
                        return true;
                    }
                    if (child.ValueKind == JsonValueKind.Object && TryFindArray(child, out arr))
                        return true;
                }
            }
        }
        arr = default;
        return false;
    }

    private static Dictionary<string, object?> ObjectToRow(JsonElement obj)
    {
        var row = new Dictionary<string, object?>();
        foreach (var p in obj.EnumerateObject())
        {
            row[p.Name] = p.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                ? p.Value.GetRawText()
                : JsonElementToObject(p.Value);
        }
        return row;
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

    private static string InferType(List<Dictionary<string, object?>> rows, string field)
    {
        foreach (var row in rows)
        {
            if (!row.TryGetValue(field, out var v) || v is null) continue;
            if (v is bool) return "boolean";
            if (v is long or int or double or float or decimal) return "number";
            return "text";
        }
        return "text";
    }

    private static string StableId(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..24].ToLowerInvariant();
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private static string CsvEscape(string s)
    {
        if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
            return $"\"{s.Replace("\"", "\"\"")}\"";
        return s;
    }
}
