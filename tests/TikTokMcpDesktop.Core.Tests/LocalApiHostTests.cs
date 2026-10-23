using System.Net.Http.Json;
using System.Text.Json;
using TikTokMcpDesktop.Core;
using TikTokMcpDesktop.LocalApi;

namespace TikTokMcpDesktop.Core.Tests;

public class LocalApiHostTests
{
    [Fact]
    public async Task LocalApi_Serves_Tables_And_Dashboards_On_Loopback()
    {
        var root = Path.Combine(Path.GetTempPath(), "ttmcp-api-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await using var services = new AppServices(root);
        using var doc = JsonDocument.Parse("""{"list":[{"id":1,"name":"demo"}]}""");
        services.Ingest.IngestToolResult("demo_tool", doc.RootElement);

        await using var host = new LocalApiHost();
        await host.StartAsync(services);
        Assert.True(host.Port > 0);

        using var http = new HttpClient { BaseAddress = new Uri(host.BaseUrl) };
        var health = await http.GetFromJsonAsync<JsonElement>("/api/health");
        Assert.True(health.GetProperty("ok").GetBoolean());

        var tables = await http.GetFromJsonAsync<JsonElement>("/api/tables");
        Assert.Equal(JsonValueKind.Array, tables.ValueKind);
        Assert.True(tables.GetArrayLength() >= 1);

        var boards = await http.GetFromJsonAsync<JsonElement>("/api/dashboards");
        Assert.True(boards.GetArrayLength() >= 2);

        try { Directory.Delete(root, true); } catch { /* ignore */ }
    }
}
