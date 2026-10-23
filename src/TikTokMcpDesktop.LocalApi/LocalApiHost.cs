using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using TikTokMcpDesktop.Core;
using TikTokMcpDesktop.Core.Logging;

namespace TikTokMcpDesktop.LocalApi;

/// <summary>
/// Starts Kestrel on 127.0.0.1 with an ephemeral port for embedding in the WPF process.
/// </summary>
public sealed class LocalApiHost : IAsyncDisposable
{
    private WebApplication? _app;
    public int Port { get; private set; }
    public string BaseUrl => $"http://127.0.0.1:{Port}";

    public async Task StartAsync(AppServices services, CancellationToken ct = default)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = services.Paths.Root,
            Args = Array.Empty<string>()
        });

        // Ephemeral port must bind IP explicitly — ListenLocalhost(0) is unsupported.
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(services);

        _app = builder.Build();
        MapRoutes(_app, services);

        await _app.StartAsync(ct);

        var server = _app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        var addr = addresses?.FirstOrDefault() ?? throw new InvalidOperationException("No server address");
        var normalized = addr.Replace("://+", "://127.0.0.1", StringComparison.Ordinal)
                             .Replace("://*", "://127.0.0.1", StringComparison.Ordinal);
        Port = new Uri(normalized).Port;
        AppLog.Info($"LocalApi listening at {BaseUrl}");
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is null) return;
        await _app.StopAsync();
        await _app.DisposeAsync();
        _app = null;
    }

    private static void MapRoutes(WebApplication app, AppServices services)
    {
        var webRoot = services.Paths.WebUiDir;

        app.Use(async (ctx, next) =>
        {
            var remote = ctx.Connection.RemoteIpAddress;
            if (remote is not null &&
                !System.Net.IPAddress.IsLoopback(remote) &&
                !remote.Equals(System.Net.IPAddress.IPv6Loopback))
            {
                ctx.Response.StatusCode = 403;
                await ctx.Response.WriteAsync("loopback only");
                return;
            }
            await next();
        });

        if (Directory.Exists(webRoot))
        {
            var provider = new PhysicalFileProvider(webRoot);
            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = provider });
            app.UseStaticFiles(new StaticFileOptions { FileProvider = provider });
        }

        app.MapGet("/api/health", () => Results.Ok(new { ok = true, time = DateTimeOffset.Now }));
        app.MapGet("/api/tables", () => Results.Ok(services.Db.ListTables()));
        app.MapGet("/api/tables/{id}", (string id) =>
        {
            var t = services.Db.GetTable(id);
            return t is null ? Results.NotFound() : Results.Ok(t);
        });
        app.MapGet("/api/tables/{id}/records", (
            string id, string? cursor, int? pageSize,
            string? filterField, string? filterOp, string? filterValue,
            string? sortField, string? sortDir) =>
            Results.Ok(services.Db.QueryRecords(id, cursor, pageSize ?? 50, filterField, filterOp, filterValue, sortField, sortDir)));
        app.MapPost("/api/tables/{id}/export/csv", (string id) =>
        {
            try
            {
                var path = services.Ingest.ExportCsv(id, services.Paths.ExportsDir);
                return Results.File(File.ReadAllBytes(path), "text/csv", Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
        app.MapGet("/api/dashboards", () => Results.Ok(services.Dashboards.List()));
        app.MapGet("/api/dashboards/{id}", (string id) =>
        {
            var layout = services.Dashboards.GetLayout(id);
            return layout is null ? Results.NotFound() : Results.Ok(layout);
        });
        app.MapGet("/api/dashboards/{id}/data", (string id, DateTimeOffset? from, DateTimeOffset? to) =>
        {
            try { return Results.Ok(services.Dashboards.GetData(id, from, to)); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        });
        app.MapGet("/api/sync/latest", () => Results.Ok(services.Db.GetLatestSync() ?? new { }));
        app.MapGet("/api/sync/logs", () => Results.Ok(services.Db.GetSyncLogs()));

        app.MapGet("/api/feishu/status", () =>
        {
            var s = services.Config.Load();
            return Results.Ok(new
            {
                enabled = s.FeishuPushEnabled,
                configured = services.Feishu.IsConfigured(s),
                appIdSet = !string.IsNullOrWhiteSpace(s.FeishuAppId),
                receiveIdType = s.FeishuReceiveIdType,
                receiveIdSet = !string.IsNullOrWhiteSpace(s.FeishuReceiveId),
                pushAfterSync = s.FeishuPushAfterSync
            });
        });
        app.MapPost("/api/feishu/push", async (CancellationToken ct) =>
        {
            try
            {
                var result = await services.Feishu.PushAdsSummaryAsync("手动/云端触发推送", ct);
                return result.Success
                    ? Results.Ok(result)
                    : Results.BadRequest(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { success = false, message = ex.Message });
            }
        });

        app.MapGet("/api/events", async (HttpContext ctx, CancellationToken ct) =>
        {
            ctx.Response.Headers.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            await ctx.Response.Body.FlushAsync(ct);

            void Handler()
            {
                _ = SafeWriteEventAsync(ctx, "data-changed");
            }

            services.Pull.DataChanged += Handler;
            try
            {
                await WriteEventAsync(ctx, "hello", ct);
                while (!ct.IsCancellationRequested)
                    await Task.Delay(15000, ct);
            }
            catch (OperationCanceledException) { }
            finally
            {
                services.Pull.DataChanged -= Handler;
            }
        });

        app.MapFallback(async ctx =>
        {
            var index = Path.Combine(webRoot, "index.html");
            ctx.Response.ContentType = "text/html; charset=utf-8";
            if (File.Exists(index))
                await ctx.Response.SendFileAsync(index);
            else
                await ctx.Response.WriteAsync("""
                    <html><body style="font-family:sans-serif;padding:2rem">
                    <h2>Web UI not built</h2>
                    <p>Run <code>npm install &amp;&amp; npm run build</code> in <code>web/ui</code>.</p>
                    <p>API: <a href="/api/health">/api/health</a></p>
                    </body></html>
                    """);
        });
    }

    private static async Task WriteEventAsync(HttpContext ctx, string evt, CancellationToken ct)
    {
        var payload = $"event: {evt}\ndata: {{\"ts\":\"{DateTimeOffset.UtcNow:O}\"}}\n\n";
        var bytes = Encoding.UTF8.GetBytes(payload);
        await ctx.Response.Body.WriteAsync(bytes, ct);
        await ctx.Response.Body.FlushAsync(ct);
    }

    private static async Task SafeWriteEventAsync(HttpContext ctx, string evt)
    {
        try
        {
            if (ctx.RequestAborted.IsCancellationRequested) return;
            await WriteEventAsync(ctx, evt, ctx.RequestAborted);
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            AppLog.Debug($"SSE write failed: {ex.Message}");
        }
    }
}
