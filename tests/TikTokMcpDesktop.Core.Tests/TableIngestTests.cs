using System.Text.Json;
using TikTokMcpDesktop.Core.Store;
using TikTokMcpDesktop.Core.Tables;

namespace TikTokMcpDesktop.Core.Tests;

public class TableIngestTests : IDisposable
{
    private readonly string _root;
    private readonly AppPaths _paths;
    private readonly DataStore _db;
    private readonly TableIngestService _ingest;

    public TableIngestTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ttmcp-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _paths = new AppPaths(_root);
        _db = new DataStore(_paths);
        _ingest = new TableIngestService(_db);
    }

    [Fact]
    public void Ingest_List_And_Query_Filter()
    {
        using var doc = JsonDocument.Parse("""
            {"list":[{"advertiser_id":"1","name":"A","spend":10},{"advertiser_id":"2","name":"B","spend":20}]}
            """);
        var (table, count) = _ingest.IngestToolResult("advertiser_info_get", doc.RootElement);
        Assert.Equal(2, count);
        Assert.Single(_db.ListTables());

        var detail = _db.GetTable(table.Id)!;
        var nameField = detail.Fields.First(f => f.Name == "name").Id;
        var advField = detail.Fields.First(f => f.Name == "advertiser_id").Id;
        var page = _db.QueryRecords(table.Id, null, 50, nameField, "contains", "A", null, null);
        Assert.Equal(1, page.Total);
        Assert.Equal("1", page.Items[0].Values[advField]?.ToString());

        var csv = _ingest.ExportCsv(table.Id, _paths.ExportsDir);
        Assert.True(File.Exists(csv));
        var text = File.ReadAllText(csv);
        Assert.Contains("advertiser_id", text);
    }

    [Fact]
    public void Flatten_Mcp_Content_Text_Wrapper()
    {
        using var doc = JsonDocument.Parse("""
            {"content":[{"type":"text","text":"{\"list\":[{\"id\":9}]}"}]}
            """);
        var rows = TableIngestService.FlattenToRows(doc.RootElement);
        Assert.Single(rows);
        Assert.Equal(9L, Convert.ToInt64(rows[0]["id"]));
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_root, true); } catch { /* ignore */ }
    }
}
