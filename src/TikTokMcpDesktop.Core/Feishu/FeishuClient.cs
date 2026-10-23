using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TikTokMcpDesktop.Core.Logging;

namespace TikTokMcpDesktop.Core.Feishu;

/// <summary>
/// Feishu/Lark application bot client.
/// Docs: tenant_access_token_internal + POST /open-apis/im/v1/messages
/// </summary>
public sealed class FeishuClient
{
    private readonly HttpClient _http;
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

    public FeishuClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    public async Task<string> GetTenantAccessTokenAsync(string appId, string appSecret, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(_cachedToken) && DateTimeOffset.UtcNow < _tokenExpiresAt.AddMinutes(-5))
            return _cachedToken!;

        var url = "https://open.feishu.cn/open-apis/auth/v3/tenant_access_token/internal";
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { app_id = appId, app_secret = appSecret }),
                Encoding.UTF8,
                "application/json")
        };

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var code = root.TryGetProperty("code", out var c) ? c.GetInt32() : -1;
        if (code != 0)
        {
            var msg = root.TryGetProperty("msg", out var m) ? m.GetString() : body;
            AppLog.Error($"Feishu tenant_access_token failed: {AppLog.Redact(msg ?? "")}");
            throw new InvalidOperationException($"飞书获取 tenant_access_token 失败: {msg}");
        }

        _cachedToken = root.GetProperty("tenant_access_token").GetString()
                       ?? throw new InvalidOperationException("飞书未返回 tenant_access_token");
        var expire = root.TryGetProperty("expire", out var ex) ? ex.GetInt32() : 7200;
        _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expire);
        return _cachedToken;
    }

    public async Task<string> SendMessageAsync(
        string tenantAccessToken,
        string receiveIdType,
        string receiveId,
        string msgType,
        string contentJson,
        string? uuid = null,
        CancellationToken ct = default)
    {
        var url = $"https://open.feishu.cn/open-apis/im/v1/messages?receive_id_type={Uri.EscapeDataString(receiveIdType)}";
        var payload = new Dictionary<string, object?>
        {
            ["receive_id"] = receiveId,
            ["msg_type"] = msgType,
            ["content"] = contentJson
        };
        if (!string.IsNullOrWhiteSpace(uuid))
            payload["uuid"] = uuid;

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tenantAccessToken);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var code = root.TryGetProperty("code", out var c) ? c.GetInt32() : -1;
        if (code != 0)
        {
            var msg = root.TryGetProperty("msg", out var m) ? m.GetString() : body;
            AppLog.Error($"Feishu send message failed: {AppLog.Redact(msg ?? "")}");
            throw new InvalidOperationException($"飞书发送消息失败: {msg}");
        }

        var messageId = root.TryGetProperty("data", out var data) && data.TryGetProperty("message_id", out var mid)
            ? mid.GetString()
            : null;
        AppLog.Info($"Feishu message sent message_id={messageId ?? "?"}");
        return messageId ?? "";
    }

    public void InvalidateTokenCache()
    {
        _cachedToken = null;
        _tokenExpiresAt = DateTimeOffset.MinValue;
    }
}
