using TikTokMcpDesktop.Core.Dashboards;
using TikTokMcpDesktop.Core.Feishu;
using TikTokMcpDesktop.Core.Jobs;
using TikTokMcpDesktop.Core.Logging;
using TikTokMcpDesktop.Core.Mcp;
using TikTokMcpDesktop.Core.OAuth;
using TikTokMcpDesktop.Core.OpenAI;
using TikTokMcpDesktop.Core.Store;
using TikTokMcpDesktop.Core.Tables;

namespace TikTokMcpDesktop.Core;

/// <summary>Composition root shared by WPF shell and LocalApi host.</summary>
public sealed class AppServices : IAsyncDisposable
{
    public AppPaths Paths { get; }
    public SecretStore Secrets { get; }
    public ConfigStore Config { get; }
    public TokenStore Tokens { get; }
    public DataStore Db { get; }
    public OAuthClient OAuth { get; }
    public McpClient Mcp { get; }
    public OpenAiClient OpenAi { get; }
    public ChatAgent Chat { get; }
    public TableIngestService Ingest { get; }
    public DashboardService Dashboards { get; }
    public FeishuClient FeishuHttp { get; }
    public FeishuPushService Feishu { get; }
    public PullJob Pull { get; }
    public PullScheduler Scheduler { get; }

    public static AppServices? Current { get; private set; }

    public AppServices(string? root = null)
    {
        Paths = new AppPaths(root);
        AppLog.Configure(Paths.LogPath);
        Secrets = new SecretStore(Paths);
        Config = new ConfigStore(Paths, Secrets);
        Tokens = new TokenStore(Secrets);
        Db = new DataStore(Paths);
        OAuth = new OAuthClient(Tokens);
        Mcp = new McpClient(OAuth);
        OpenAi = new OpenAiClient();
        Chat = new ChatAgent(OpenAi, Mcp, Config);
        Ingest = new TableIngestService(Db);
        Dashboards = new DashboardService(Db);
        FeishuHttp = new FeishuClient();
        Feishu = new FeishuPushService(Config, Secrets, Dashboards, FeishuHttp);
        Pull = new PullJob(Mcp, Config, Db, Ingest, Paths, Feishu);
        Scheduler = new PullScheduler(Pull, Config);
        Current = this;
        AppLog.Info($"AppServices ready at {Paths.Root}");
    }

    public async ValueTask DisposeAsync()
    {
        await Scheduler.DisposeAsync();
        Db.Dispose();
        if (ReferenceEquals(Current, this)) Current = null;
    }
}
