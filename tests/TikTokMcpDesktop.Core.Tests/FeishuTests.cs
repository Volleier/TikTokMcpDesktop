using System.Net;
using System.Text;
using System.Text.Json;
using TikTokMcpDesktop.Core.Dashboards;
using TikTokMcpDesktop.Core.Feishu;
using TikTokMcpDesktop.Core.Store;
using TikTokMcpDesktop.Core.Tables;

namespace TikTokMcpDesktop.Core.Tests;

public class FeishuTests : IDisposable
{
    private readonly string _root;
    private readonly AppPaths _paths;

    public FeishuTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ttmcp-feishu-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _paths = new AppPaths(_root);
    }

    [Fact]
    public void CardBuilder_Contains_Metrics()
    {
        var json = FeishuCardBuilder.BuildAdsSummaryCard(
            12.5, 1000, 20, 3,
            [("系列A", 8.0), ("系列B", 4.5)],
            "同步完成");
        using var doc = JsonDocument.Parse(json);
        var text = doc.RootElement.GetProperty("elements")[0].GetProperty("text").GetProperty("content").GetString();
        Assert.Contains("12.5", text);
        Assert.Contains("系列A", text);
        Assert.Contains("同步完成", text);
    }

    [Fact]
    public async Task Client_Gets_Token_And_Sends_Message()
    {
        var handler = new StubHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("tenant_access_token"))
            {
                return JsonResponse("""{"code":0,"tenant_access_token":"t-test","expire":7200}""");
            }

            if (path.Contains("/im/v1/messages"))
            {
                Assert.Equal("Bearer t-test", req.Headers.Authorization?.ToString());
                return JsonResponse("""{"code":0,"data":{"message_id":"om_123"}}""");
            }

            return JsonResponse("""{"code":1,"msg":"unexpected"}""", HttpStatusCode.BadRequest);
        });

        var client = new FeishuClient(new HttpClient(handler));
        var token = await client.GetTenantAccessTokenAsync("cli_x", "secret");
        Assert.Equal("t-test", token);
        var mid = await client.SendMessageAsync(token, "chat_id", "oc_1", "text", """{"text":"hi"}""");
        Assert.Equal("om_123", mid);
    }

    [Fact]
    public async Task PushService_Reports_Not_Configured()
    {
        var secrets = new SecretStore(_paths);
        var config = new ConfigStore(_paths, secrets);
        using var db = new DataStore(_paths);
        var dash = new DashboardService(db);
        var push = new FeishuPushService(config, secrets, dash);
        var result = await push.PushAdsSummaryAsync();
        Assert.False(result.Success);
        Assert.Contains("未启用", result.Message);
    }

    [Fact]
    public async Task PushService_Sends_When_Configured()
    {
        var secrets = new SecretStore(_paths);
        secrets.Update(p => p.FeishuAppSecret = "sec");
        var config = new ConfigStore(_paths, secrets);
        var settings = config.Load();
        settings.FeishuPushEnabled = true;
        settings.FeishuAppId = "cli_demo";
        settings.FeishuReceiveIdType = "chat_id";
        settings.FeishuReceiveId = "oc_demo";
        config.Save(settings);

        using var db = new DataStore(_paths);
        var ingest = new TableIngestService(db);
        DemoDataSeeder.Seed(ingest);
        var dash = new DashboardService(db);

        var handler = new StubHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("tenant_access_token"))
                return JsonResponse("""{"code":0,"tenant_access_token":"t-ok","expire":7200}""");
            return JsonResponse("""{"code":0,"data":{"message_id":"om_push"}}""");
        });
        var client = new FeishuClient(new HttpClient(handler));
        var push = new FeishuPushService(config, secrets, dash, client);
        Assert.True(push.IsConfigured());
        var result = await push.PushAdsSummaryAsync("unit-test");
        Assert.True(result.Success, result.Message);
        Assert.Equal("om_push", result.MessageId);
    }

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { /* ignore */ }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_respond(request));
    }
}
