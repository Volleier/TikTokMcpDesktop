namespace TikTokMcpDesktop.Core.Tables;

public sealed class DataTableMeta
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string SourceTool { get; set; } = "";
    public DateTimeOffset? LastSyncedAt { get; set; }
}

public sealed class DataField
{
    public string Id { get; set; } = "";
    public string TableId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "text";
    public int Width { get; set; } = 140;
    public bool Visible { get; set; } = true;
    public int Sort { get; set; }
}

public sealed class DataRecord
{
    public string Id { get; set; } = "";
    public string TableId { get; set; } = "";
    public Dictionary<string, object?> Values { get; set; } = new();
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DataViewConfig
{
    public string Id { get; set; } = "default";
    public string TableId { get; set; } = "";
    public string Name { get; set; } = "Grid";
    public string Type { get; set; } = "grid";
    public List<string> VisibleFieldIds { get; set; } = new();
    public List<object> Filters { get; set; } = new();
    public List<object> Sorts { get; set; } = new();
}

public sealed class DataTableDetail
{
    public DataTableMeta Table { get; set; } = new();
    public List<DataField> Fields { get; set; } = new();
    public DataViewConfig ActiveView { get; set; } = new();
}

public sealed class RecordPage
{
    public IReadOnlyList<DataRecord> Items { get; set; } = Array.Empty<DataRecord>();
    public int Total { get; set; }
    public string? NextCursor { get; set; }
    public bool HasMore { get; set; }
}
