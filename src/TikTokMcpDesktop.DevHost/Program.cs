using System.Text.Json;
using TikTokMcpDesktop.Core;
using TikTokMcpDesktop.LocalApi;

// Cross-platform host for LocalApi + web UI (useful on Linux CI where WPF cannot run).
var root = Directory.GetCurrentDirectory();
while (root is not null && !File.Exists(Path.Combine(root, "TikTokMcpDesktop.sln")))
    root = Directory.GetParent(root)?.FullName;
root ??= Directory.GetCurrentDirectory();

await using var services = new AppServices(root);

// Seed demo rows if empty so tables/dashboards are browsable offline.
if (services.Db.ListTables().Count == 0)
{
    using var doc = JsonDocument.Parse("""
        {
          "list": [
            {"advertiser_id":"1001","name":"Demo Advertiser A","spend":120.5,"impressions":9000,"clicks":120},
            {"advertiser_id":"1002","name":"Demo Advertiser B","spend":88.0,"impressions":5400,"clicks":77},
            {"advertiser_id":"1003","name":"Demo Advertiser C","spend":210.25,"impressions":15000,"clicks":301}
          ]
        }
        """);
    services.Ingest.IngestToolResult("advertiser_info_get", doc.RootElement, Path.Combine(services.Paths.PullsDir, DateTime.Now.ToString("yyyy-MM-dd")));
    Console.WriteLine("Seeded demo advertiser table.");
}

await using var host = new LocalApiHost();
await host.StartAsync(services);
Console.WriteLine($"LocalApi: {host.BaseUrl}");
Console.WriteLine($"Tables:   {host.BaseUrl}/#/tables");
Console.WriteLine($"Dashboards: {host.BaseUrl}/#/dashboards");
Console.WriteLine("Press Ctrl+C to stop.");

var tcs = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; tcs.TrySetResult(); };
await tcs.Task;
