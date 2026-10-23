using TikTokMcpDesktop.Core;
using TikTokMcpDesktop.Core.Tables;
using TikTokMcpDesktop.LocalApi;

// Cross-platform host for LocalApi + web UI (useful on Linux CI where WPF cannot run).
var root = Directory.GetCurrentDirectory();
while (root is not null && !File.Exists(Path.Combine(root, "TikTokMcpDesktop.sln")))
    root = Directory.GetParent(root)?.FullName;
root ??= Directory.GetCurrentDirectory();

await using var services = new AppServices(root);

// Seed TikTok Open MCP-shaped demo data when empty / incomplete so tables & charts are browsable offline.
if (DemoDataSeeder.NeedsSeed(services.Db))
{
    var pullDir = Path.Combine(services.Paths.PullsDir, DateTime.Now.ToString("yyyy-MM-dd"));
    var n = DemoDataSeeder.Seed(services.Ingest, pullDir);
    Console.WriteLine($"Seeded demo MCP tables ({n} rows): advertiser / campaign / adgroup / ad / report.");
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
