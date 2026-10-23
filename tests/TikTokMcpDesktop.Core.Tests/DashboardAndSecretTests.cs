using System.Text.Json;
using TikTokMcpDesktop.Core.Dashboards;
using TikTokMcpDesktop.Core.Store;
using TikTokMcpDesktop.Core.Tables;

namespace TikTokMcpDesktop.Core.Tests;

public class DashboardAndSecretTests : IDisposable
{
    private readonly string _root;
    private readonly AppPaths _paths;

    public DashboardAndSecretTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ttmcp-dash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _paths = new AppPaths(_root);
    }

    [Fact]
    public void Secrets_RoundTrip()
    {
        var secrets = new SecretStore(_paths);
        secrets.Update(p =>
        {
            p.OpenAiApiKey = "sk-test-key";
            p.TokensByVariant["layer"] = new OAuthTokenSet
            {
                AccessToken = "access",
                RefreshToken = "refresh",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
            };
        });
        var loaded = secrets.Load();
        Assert.Equal("sk-test-key", loaded.OpenAiApiKey);
        Assert.Equal("access", loaded.TokensByVariant["layer"].AccessToken);
    }

    [Fact]
    public void Dashboards_Return_Preset_Data()
    {
        using var db = new DataStore(_paths);
        var ingest = new TableIngestService(db);
        using var doc = JsonDocument.Parse("""{"list":[{"advertiser_id":"x","spend":3}]}""");
        ingest.IngestToolResult("advertiser_info_get", doc.RootElement);

        var dash = new DashboardService(db);
        Assert.Equal(3, dash.List().Count);
        var data = dash.GetData("overview");
        Assert.False(data.Empty);
        Assert.True(data.Widgets.ContainsKey("kpi_tables"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { /* ignore */ }
    }
}
