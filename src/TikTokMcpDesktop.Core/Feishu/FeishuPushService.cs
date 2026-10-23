using System.Text.Json;
using TikTokMcpDesktop.Core.Config;
using TikTokMcpDesktop.Core.Dashboards;
using TikTokMcpDesktop.Core.Logging;
using TikTokMcpDesktop.Core.Store;

namespace TikTokMcpDesktop.Core.Feishu;

public sealed class FeishuPushResult
{
    public bool Success { get; init; }
    public string? MessageId { get; init; }
    public string Message { get; init; } = "";
}

/// <summary>
/// Pushes TikTok Ads MCP summary to Feishu as an application bot.
/// Hybrid model: TikTok auth stays on desktop; Feishu push uses app identity (tenant_access_token).
/// </summary>
public sealed class FeishuPushService
{
    private readonly ConfigStore _config;
    private readonly SecretStore _secrets;
    private readonly DashboardService _dashboards;
    private readonly FeishuClient _client;

    public FeishuPushService(
        ConfigStore config,
        SecretStore secrets,
        DashboardService dashboards,
        FeishuClient? client = null)
    {
        _config = config;
        _secrets = secrets;
        _dashboards = dashboards;
        _client = client ?? new FeishuClient();
    }

    public bool IsConfigured(AppSettings? settings = null)
    {
        settings ??= _config.Load();
        var secret = _secrets.Load().FeishuAppSecret;
        return settings.FeishuPushEnabled
               && !string.IsNullOrWhiteSpace(settings.FeishuAppId)
               && !string.IsNullOrWhiteSpace(secret)
               && !string.IsNullOrWhiteSpace(settings.FeishuReceiveId);
    }

    public async Task<FeishuPushResult> PushAdsSummaryAsync(string? syncMessage = null, CancellationToken ct = default)
    {
        var settings = _config.Load();
        if (!settings.FeishuPushEnabled)
            return new FeishuPushResult { Success = false, Message = "飞书推送未启用" };

        var appId = settings.FeishuAppId.Trim();
        var appSecret = _secrets.Load().FeishuAppSecret?.Trim();
        var receiveId = settings.FeishuReceiveId.Trim();
        var receiveIdType = string.IsNullOrWhiteSpace(settings.FeishuReceiveIdType)
            ? "chat_id"
            : settings.FeishuReceiveIdType.Trim();

        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(appSecret) || string.IsNullOrWhiteSpace(receiveId))
            return new FeishuPushResult { Success = false, Message = "请完整配置飞书 App ID / App Secret / 接收方 ID" };

        try
        {
            var (spend, impressions, clicks, conversions, top) = ExtractMetrics();
            var cardJson = FeishuCardBuilder.BuildAdsSummaryCard(
                spend, impressions, clicks, conversions, top, syncMessage);

            var token = await _client.GetTenantAccessTokenAsync(appId, appSecret, ct);
            var messageId = await _client.SendMessageAsync(
                token,
                receiveIdType,
                receiveId,
                "interactive",
                cardJson,
                uuid: Guid.NewGuid().ToString("N")[..32],
                ct);

            return new FeishuPushResult
            {
                Success = true,
                MessageId = messageId,
                Message = $"已推送到飞书（{receiveIdType}:{receiveId}）"
            };
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Feishu push failed: {ex.Message}");
            // Fallback to plain text once if interactive card rejected
            if (ex.Message.Contains("card", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("interactive", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var (spend, impressions, clicks, conversions, _) = ExtractMetrics();
                    var token = await _client.GetTenantAccessTokenAsync(appId, appSecret!, ct);
                    var messageId = await _client.SendMessageAsync(
                        token,
                        receiveIdType,
                        receiveId,
                        "text",
                        FeishuCardBuilder.BuildTextFallback(spend, impressions, clicks, conversions),
                        uuid: Guid.NewGuid().ToString("N")[..32],
                        ct);
                    return new FeishuPushResult
                    {
                        Success = true,
                        MessageId = messageId,
                        Message = "卡片被拒，已改用文本推送成功"
                    };
                }
                catch (Exception ex2)
                {
                    return new FeishuPushResult { Success = false, Message = ex2.Message };
                }
            }

            return new FeishuPushResult { Success = false, Message = ex.Message };
        }
    }

    private (double Spend, double Imp, double Clicks, double Conv, List<(string Name, double Spend)> Top)
        ExtractMetrics()
    {
        var data = _dashboards.GetData("overview");
        double Num(string widgetId)
        {
            if (!data.Widgets.TryGetValue(widgetId, out var obj) || obj is null) return 0;
            var json = JsonSerializer.Serialize(obj);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetDouble()
                : 0;
        }

        var top = new List<(string, double)>();
        if (data.Widgets.TryGetValue("pie_campaign", out var pie) && pie is not null)
        {
            var json = JsonSerializer.Serialize(pie);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("labels", out var labels) && root.TryGetProperty("values", out var values)
                && labels.ValueKind == JsonValueKind.Array && values.ValueKind == JsonValueKind.Array)
            {
                var n = Math.Min(labels.GetArrayLength(), values.GetArrayLength());
                for (var i = 0; i < n && i < 5; i++)
                {
                    var name = labels[i].GetString() ?? $"系列{i + 1}";
                    var spend = values[i].ValueKind == JsonValueKind.Number ? values[i].GetDouble() : 0;
                    top.Add((name, spend));
                }
            }
        }

        return (Num("kpi_spend"), Num("kpi_impressions"), Num("kpi_clicks"), Num("kpi_conv"), top);
    }
}
