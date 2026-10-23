using System.Text.Json;
using Microsoft.Data.Sqlite;
using TikTokMcpDesktop.Core.Logging;
using TikTokMcpDesktop.Core.Tables;

namespace TikTokMcpDesktop.Core.Store;

public sealed class DataStore : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _conn;
    private readonly object _lock = new();

    public DataStore(AppPaths paths)
    {
        _dbPath = paths.DbPath;
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        _conn = new SqliteConnection($"Data Source={_dbPath}");
        _conn.Open();
        InitSchema();
    }

    private void InitSchema()
    {
        Execute("""
            CREATE TABLE IF NOT EXISTS sync_runs (
              id TEXT PRIMARY KEY,
              started_at TEXT NOT NULL,
              finished_at TEXT,
              status TEXT NOT NULL,
              tool_count INTEGER NOT NULL DEFAULT 0,
              record_count INTEGER NOT NULL DEFAULT 0,
              message TEXT
            );
            CREATE TABLE IF NOT EXISTS sync_logs (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              run_id TEXT NOT NULL,
              at TEXT NOT NULL,
              level TEXT NOT NULL,
              message TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS data_tables (
              id TEXT PRIMARY KEY,
              name TEXT NOT NULL,
              source_tool TEXT NOT NULL,
              last_synced_at TEXT
            );
            CREATE TABLE IF NOT EXISTS data_fields (
              id TEXT PRIMARY KEY,
              table_id TEXT NOT NULL,
              name TEXT NOT NULL,
              type TEXT NOT NULL,
              width INTEGER NOT NULL DEFAULT 140,
              visible INTEGER NOT NULL DEFAULT 1,
              sort INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS data_records (
              id TEXT PRIMARY KEY,
              table_id TEXT NOT NULL,
              values_json TEXT NOT NULL,
              updated_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_records_table ON data_records(table_id);
            CREATE INDEX IF NOT EXISTS idx_fields_table ON data_fields(table_id);
            """);
    }

    public void Execute(string sql)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }

    public SqliteConnection Connection => _conn;
    public object SyncRoot => _lock;

    public void UpsertTable(DataTableMeta table, IReadOnlyList<DataField> fields, IReadOnlyList<DataRecord> records)
    {
        lock (_lock)
        {
            using var tx = _conn.BeginTransaction();

            using (var delRec = _conn.CreateCommand())
            {
                delRec.Transaction = tx;
                delRec.CommandText = "DELETE FROM data_records WHERE table_id=$t";
                delRec.Parameters.AddWithValue("$t", table.Id);
                delRec.ExecuteNonQuery();
            }

            using (var delFields = _conn.CreateCommand())
            {
                delFields.Transaction = tx;
                delFields.CommandText = "DELETE FROM data_fields WHERE table_id=$t";
                delFields.Parameters.AddWithValue("$t", table.Id);
                delFields.ExecuteNonQuery();
            }

            using (var upsert = _conn.CreateCommand())
            {
                upsert.Transaction = tx;
                upsert.CommandText = """
                    INSERT INTO data_tables(id,name,source_tool,last_synced_at)
                    VALUES($id,$name,$tool,$synced)
                    ON CONFLICT(id) DO UPDATE SET
                      name=excluded.name,
                      source_tool=excluded.source_tool,
                      last_synced_at=excluded.last_synced_at
                    """;
                upsert.Parameters.AddWithValue("$id", table.Id);
                upsert.Parameters.AddWithValue("$name", table.Name);
                upsert.Parameters.AddWithValue("$tool", table.SourceTool);
                upsert.Parameters.AddWithValue("$synced", table.LastSyncedAt?.ToString("O") ?? (object)DBNull.Value);
                upsert.ExecuteNonQuery();
            }

            foreach (var f in fields)
            {
                using var cmd = _conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO data_fields(id,table_id,name,type,width,visible,sort)
                    VALUES($id,$tid,$name,$type,$w,$v,$s)
                    """;
                cmd.Parameters.AddWithValue("$id", f.Id);
                cmd.Parameters.AddWithValue("$tid", f.TableId);
                cmd.Parameters.AddWithValue("$name", f.Name);
                cmd.Parameters.AddWithValue("$type", f.Type);
                cmd.Parameters.AddWithValue("$w", f.Width);
                cmd.Parameters.AddWithValue("$v", f.Visible ? 1 : 0);
                cmd.Parameters.AddWithValue("$s", f.Sort);
                cmd.ExecuteNonQuery();
            }

            foreach (var r in records)
            {
                using var cmd = _conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO data_records(id,table_id,values_json,updated_at)
                    VALUES($id,$tid,$vals,$upd)
                    """;
                cmd.Parameters.AddWithValue("$id", r.Id);
                cmd.Parameters.AddWithValue("$tid", r.TableId);
                cmd.Parameters.AddWithValue("$vals", JsonSerializer.Serialize(r.Values));
                cmd.Parameters.AddWithValue("$upd", r.UpdatedAt.ToString("O"));
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        AppLog.Info($"Upserted table {table.Name}: {fields.Count} fields, {records.Count} records");
    }

    public IReadOnlyList<DataTableMeta> ListTables()
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id,name,source_tool,last_synced_at FROM data_tables ORDER BY name";
            using var reader = cmd.ExecuteReader();
            var list = new List<DataTableMeta>();
            while (reader.Read())
            {
                list.Add(new DataTableMeta
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    SourceTool = reader.GetString(2),
                    LastSyncedAt = reader.IsDBNull(3) ? null : DateTimeOffset.Parse(reader.GetString(3))
                });
            }
            return list;
        }
    }

    public DataTableDetail? GetTable(string tableId)
    {
        lock (_lock)
        {
            DataTableMeta? meta = null;
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id,name,source_tool,last_synced_at FROM data_tables WHERE id=$id";
                cmd.Parameters.AddWithValue("$id", tableId);
                using var reader = cmd.ExecuteReader();
                if (!reader.Read()) return null;
                meta = new DataTableMeta
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    SourceTool = reader.GetString(2),
                    LastSyncedAt = reader.IsDBNull(3) ? null : DateTimeOffset.Parse(reader.GetString(3))
                };
            }

            var fields = new List<DataField>();
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id,table_id,name,type,width,visible,sort FROM data_fields WHERE table_id=$id ORDER BY sort,name";
                cmd.Parameters.AddWithValue("$id", tableId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    fields.Add(new DataField
                    {
                        Id = reader.GetString(0),
                        TableId = reader.GetString(1),
                        Name = reader.GetString(2),
                        Type = reader.GetString(3),
                        Width = reader.GetInt32(4),
                        Visible = reader.GetInt32(5) == 1,
                        Sort = reader.GetInt32(6)
                    });
                }
            }

            return new DataTableDetail
            {
                Table = meta!,
                Fields = fields,
                ActiveView = new DataViewConfig
                {
                    Id = "default",
                    TableId = tableId,
                    Name = "Grid",
                    Type = "grid",
                    VisibleFieldIds = fields.Where(f => f.Visible).Select(f => f.Id).ToList()
                }
            };
        }
    }

    public RecordPage QueryRecords(string tableId, string? cursor, int pageSize, string? filterField, string? filterOp, string? filterValue, string? sortField, string? sortDir)
    {
        lock (_lock)
        {
            pageSize = Math.Clamp(pageSize, 1, 500);
            var offset = 0;
            if (!string.IsNullOrEmpty(cursor) && int.TryParse(cursor, out var c))
                offset = Math.Max(0, c);

            var where = "table_id=$tid";
            if (!string.IsNullOrWhiteSpace(filterField) && !string.IsNullOrWhiteSpace(filterOp))
            {
                // JSON extract filter — keep simple for MVP
                where += filterOp switch
                {
                    "isEmpty" => $" AND (json_extract(values_json, '$.{EscapeJsonKey(filterField)}') IS NULL OR json_extract(values_json, '$.{EscapeJsonKey(filterField)}') = '')",
                    "isNotEmpty" => $" AND json_extract(values_json, '$.{EscapeJsonKey(filterField)}') IS NOT NULL AND json_extract(values_json, '$.{EscapeJsonKey(filterField)}') != ''",
                    "contains" => $" AND instr(lower(coalesce(json_extract(values_json, '$.{EscapeJsonKey(filterField)}'),'')), lower($fval)) > 0",
                    "equals" => $" AND cast(json_extract(values_json, '$.{EscapeJsonKey(filterField)}') as text) = $fval",
                    "notEquals" => $" AND cast(json_extract(values_json, '$.{EscapeJsonKey(filterField)}') as text) != $fval",
                    "greaterThan" => $" AND cast(json_extract(values_json, '$.{EscapeJsonKey(filterField)}') as real) > cast($fval as real)",
                    "lessThan" => $" AND cast(json_extract(values_json, '$.{EscapeJsonKey(filterField)}') as real) < cast($fval as real)",
                    _ => ""
                };
            }

            var order = "updated_at DESC, id";
            if (!string.IsNullOrWhiteSpace(sortField))
            {
                var dir = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
                order = $"json_extract(values_json, '$.{EscapeJsonKey(sortField)}') {dir}, id";
            }

            int total;
            using (var countCmd = _conn.CreateCommand())
            {
                countCmd.CommandText = $"SELECT COUNT(*) FROM data_records WHERE {where}";
                countCmd.Parameters.AddWithValue("$tid", tableId);
                if (where.Contains("$fval"))
                    countCmd.Parameters.AddWithValue("$fval", filterValue ?? "");
                total = Convert.ToInt32(countCmd.ExecuteScalar());
            }

            var items = new List<DataRecord>();
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = $"""
                    SELECT id, table_id, values_json, updated_at
                    FROM data_records
                    WHERE {where}
                    ORDER BY {order}
                    LIMIT $limit OFFSET $offset
                    """;
                cmd.Parameters.AddWithValue("$tid", tableId);
                cmd.Parameters.AddWithValue("$limit", pageSize);
                cmd.Parameters.AddWithValue("$offset", offset);
                if (where.Contains("$fval"))
                    cmd.Parameters.AddWithValue("$fval", filterValue ?? "");

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var vals = JsonSerializer.Deserialize<Dictionary<string, object?>>(reader.GetString(2))
                               ?? new Dictionary<string, object?>();
                    items.Add(new DataRecord
                    {
                        Id = reader.GetString(0),
                        TableId = reader.GetString(1),
                        Values = vals,
                        UpdatedAt = DateTimeOffset.Parse(reader.GetString(3))
                    });
                }
            }

            var next = offset + items.Count;
            return new RecordPage
            {
                Items = items,
                Total = total,
                NextCursor = next < total ? next.ToString() : null,
                HasMore = next < total
            };
        }
    }

    public void InsertSyncRun(string id, DateTimeOffset started, string status)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT INTO sync_runs(id,started_at,status) VALUES($id,$s,$st)";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$s", started.ToString("O"));
            cmd.Parameters.AddWithValue("$st", status);
            cmd.ExecuteNonQuery();
        }
    }

    public void FinishSyncRun(string id, DateTimeOffset finished, string status, int toolCount, int recordCount, string? message)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                UPDATE sync_runs SET finished_at=$f, status=$st, tool_count=$tc, record_count=$rc, message=$m
                WHERE id=$id
                """;
            cmd.Parameters.AddWithValue("$f", finished.ToString("O"));
            cmd.Parameters.AddWithValue("$st", status);
            cmd.Parameters.AddWithValue("$tc", toolCount);
            cmd.Parameters.AddWithValue("$rc", recordCount);
            cmd.Parameters.AddWithValue("$m", (object?)message ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    public void AddSyncLog(string runId, string level, string message)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT INTO sync_logs(run_id,at,level,message) VALUES($r,$a,$l,$m)";
            cmd.Parameters.AddWithValue("$r", runId);
            cmd.Parameters.AddWithValue("$a", DateTimeOffset.Now.ToString("O"));
            cmd.Parameters.AddWithValue("$l", level);
            cmd.Parameters.AddWithValue("$m", AppLog.Redact(message));
            cmd.ExecuteNonQuery();
        }
    }

    public object? GetLatestSync()
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id,started_at,finished_at,status,tool_count,record_count,message FROM sync_runs ORDER BY started_at DESC LIMIT 1";
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return new
            {
                id = reader.GetString(0),
                startedAt = reader.GetString(1),
                finishedAt = reader.IsDBNull(2) ? null : reader.GetString(2),
                status = reader.GetString(3),
                toolCount = reader.GetInt32(4),
                recordCount = reader.GetInt32(5),
                message = reader.IsDBNull(6) ? null : reader.GetString(6)
            };
        }
    }

    public IReadOnlyList<object> GetSyncLogs(int limit = 100)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT at,level,message FROM sync_logs ORDER BY id DESC LIMIT $n";
            cmd.Parameters.AddWithValue("$n", limit);
            using var reader = cmd.ExecuteReader();
            var list = new List<object>();
            while (reader.Read())
            {
                list.Add(new { at = reader.GetString(0), level = reader.GetString(1), message = reader.GetString(2) });
            }
            list.Reverse();
            return list;
        }
    }

    private static string EscapeJsonKey(string key)
    {
        // field ids are generated by us; still strip quotes
        return key.Replace("'", "").Replace("\"", "").Replace(";", "");
    }

    public void Dispose()
    {
        _conn.Dispose();
    }
}
