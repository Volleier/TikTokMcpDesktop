using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using TikTokMcpDesktop.Core.Config;
using TikTokMcpDesktop.Core.Logging;
using TikTokMcpDesktop.Core.Store;

namespace TikTokMcpDesktop.Core.OAuth;

public sealed class ReauthRequiredException : Exception
{
    public ReauthRequiredException(string message) : base(message) { }
}

/// <summary>
/// TikTok for Business Open MCP OAuth2 + PKCE.
/// Default path: Dynamic Client Registration (no preconfigured client_id), then authorize + JSON token exchange.
/// Optional fixed client_id/app_id still supported when filled in settings.
/// </summary>
public sealed class OAuthClient
{
    private readonly TokenStore _tokenStore;
    private readonly HttpClient _http;

    public OAuthClient(TokenStore tokenStore, HttpClient? http = null)
    {
        _tokenStore = tokenStore;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    public async Task<OAuthTokenSet> AuthorizeInteractiveAsync(
        AppSettings settings,
        CancellationToken ct = default)
    {
        var variant = settings.McpVariant;
        var resource = settings.GetResource();
        var redirectUri = $"http://127.0.0.1:{settings.OAuthCallbackPort}/callback";

        var clientId = await EnsureClientIdAsync(settings, redirectUri, ct);
        var verifier = Pkce.CreateVerifier(32);
        var challenge = Pkce.CreateChallenge(verifier);
        var state = Pkce.CreateState();

        var authUrl =
            $"{settings.GetAuthorizeUrl()}" +
            $"?client_id={Uri.EscapeDataString(clientId)}" +
            $"&app_id={Uri.EscapeDataString(clientId)}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&response_type=code" +
            $"&scope={Uri.EscapeDataString(TikTokEndpoints.Scope)}" +
            $"&resource={Uri.EscapeDataString(resource)}" +
            $"&code_challenge={Uri.EscapeDataString(challenge)}" +
            $"&code_challenge_method=S256";

        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{settings.OAuthCallbackPort}/");
        listener.Start();
        AppLog.Info($"OAuth listening on {redirectUri} (variant={variant}, client={RedactId(clientId)})");

        try
        {
            OpenBrowser(authUrl);
            var context = await listener.GetContextAsync().WaitAsync(ct);
            var req = context.Request;
            var code = FirstNonEmpty(req.QueryString["code"], req.QueryString["auth_code"]);
            var returnedState = req.QueryString["state"];
            var error = req.QueryString["error"];

            var html = "<html><body style='font-family:sans-serif;padding:2rem'><h2>授权完成</h2><p>可以关闭此窗口，返回 TikTok MCP Desktop。</p></body></html>";
            if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code) || returnedState != state)
            {
                html = $"<html><body style='font-family:sans-serif;padding:2rem'><h2>授权失败</h2><p>{WebUtility.HtmlEncode(error ?? "invalid state/code")}</p></body></html>";
            }

            var buffer = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, ct);
            context.Response.Close();

            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException($"授权失败: {error}");
            if (string.IsNullOrEmpty(code) || returnedState != state)
                throw new InvalidOperationException("授权回调无效（state/code 校验失败）。");

            var token = await ExchangeCodeAsync(settings, clientId, code, redirectUri, verifier, resource, ct);
            _tokenStore.Save(variant, token);
            AppLog.Info($"OAuth token saved for variant {variant}; advertisers={token.AdvertiserIds.Count}");
            return token;
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// Prefer settings client_id/app_id; otherwise reuse or create DCR client for this MCP variant.
    /// </summary>
    public async Task<string> EnsureClientIdAsync(AppSettings settings, string redirectUri, CancellationToken ct = default)
    {
        var existing = _tokenStore.ResolveClientId(settings);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            // If DCR client was registered for a different redirect, re-register
            var dcr = _tokenStore.GetDynamicClient(settings.McpVariant);
            if (!string.IsNullOrWhiteSpace(settings.EffectiveClientId) ||
                dcr is null ||
                string.Equals(dcr.RedirectUri, redirectUri, StringComparison.Ordinal))
            {
                return existing!;
            }
        }

        AppLog.Info("No client_id configured — Dynamic Client Registration…");
        var registered = await RegisterDynamicClientAsync(settings, redirectUri, ct);
        _tokenStore.SaveDynamicClient(settings.McpVariant, registered);
        AppLog.Info($"DCR client_id issued for {settings.McpVariant}: {RedactId(registered.ClientId)}");
        return registered.ClientId;
    }

    public async Task<DynamicClientRegistration> RegisterDynamicClientAsync(
        AppSettings settings,
        string redirectUri,
        CancellationToken ct = default)
    {
        var url = $"{settings.GetMcpUrl().TrimEnd('/')}/oauth/register";
        var body = new Dictionary<string, object?>
        {
            ["client_name"] = "TikTok MCP Desktop",
            ["redirect_uris"] = new[] { redirectUri },
            ["grant_types"] = new[] { "authorization_code", "refresh_token" },
            ["response_types"] = new[] { "code" },
            ["token_endpoint_auth_method"] = "none",
            ["scope"] = TikTokEndpoints.Scope
        };

        using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync(url, content, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            AppLog.Error($"DCR register HTTP {(int)resp.StatusCode}: {AppLog.Redact(text)}");
            throw new InvalidOperationException($"动态客户端注册失败 HTTP {(int)resp.StatusCode}");
        }

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            root = data;

        var clientId = root.TryGetProperty("client_id", out var cid) ? cid.GetString() : null;
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("DCR 响应缺少 client_id");

        return new DynamicClientRegistration
        {
            ClientId = clientId!,
            RedirectUri = redirectUri,
            RegisteredAt = DateTimeOffset.UtcNow
        };
    }

    public async Task<string> EnsureAccessTokenAsync(AppSettings settings, CancellationToken ct = default)
    {
        var variant = settings.McpVariant;
        var token = _tokenStore.Get(variant);
        if (token is null || string.IsNullOrEmpty(token.AccessToken))
            throw new ReauthRequiredException("尚未授权 TikTok Open MCP，请先完成授权。");

        if (!_tokenStore.IsExpiringSoon(variant, TimeSpan.FromMinutes(3)))
            return token.AccessToken;

        if (string.IsNullOrEmpty(token.RefreshToken))
            throw new ReauthRequiredException("access_token 已过期且无 refresh_token，请重新授权。");

        var clientId = _tokenStore.ResolveClientId(settings)
                       ?? throw new ReauthRequiredException("缺少 client_id（请重新授权以完成动态注册）。");

        try
        {
            var refreshed = await RefreshAsync(settings, clientId, token.RefreshToken, ct);
            if (refreshed.AdvertiserIds.Count == 0 && token.AdvertiserIds.Count > 0)
                refreshed.AdvertiserIds = token.AdvertiserIds;
            _tokenStore.Save(variant, refreshed);
            AppLog.Info("Access token refreshed");
            return refreshed.AccessToken;
        }
        catch (ReauthRequiredException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Refresh failed: {ex.Message}");
            throw new ReauthRequiredException("授权过期，请重连（refresh_token 失效）。");
        }
    }

    public async Task<OAuthTokenSet> ExchangeCodeAsync(
        AppSettings settings,
        string clientId,
        string code,
        string redirectUri,
        string codeVerifier,
        string resource,
        CancellationToken ct = default)
    {
        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier,
            ["resource"] = resource
        };
        if (!string.IsNullOrWhiteSpace(settings.AppSecret))
            body["secret"] = settings.AppSecret;
        return await RequestTokenAsync(settings, body, ct);
    }

    public async Task<OAuthTokenSet> RefreshAsync(
        AppSettings settings,
        string clientId,
        string refreshToken,
        CancellationToken ct = default)
    {
        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = clientId,
            ["refresh_token"] = refreshToken,
            ["resource"] = settings.GetResource()
        };
        if (!string.IsNullOrWhiteSpace(settings.AppSecret))
            body["secret"] = settings.AppSecret;
        return await RequestTokenAsync(settings, body, ct);
    }

    private async Task<OAuthTokenSet> RequestTokenAsync(
        AppSettings settings,
        Dictionary<string, string> body,
        CancellationToken ct)
    {
        var url = settings.GetTokenUrl();
        HttpResponseMessage resp;

        if (settings.TokenRequestFormat == TokenRequestFormat.Form)
        {
            using var content = new FormUrlEncodedContent(body);
            resp = await _http.PostAsync(url, content, ct);
        }
        else
        {
            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            resp = await _http.PostAsync(url, content, ct);
        }

        var text = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            AppLog.Error($"Token endpoint {resp.StatusCode}: {AppLog.Redact(text)}");
            if ((int)resp.StatusCode is 401 or 403)
                throw new ReauthRequiredException("授权过期，请重连。");
            throw new InvalidOperationException($"换票失败 HTTP {(int)resp.StatusCode}");
        }

        return ParseTokenResponse(text);
    }

    internal static OAuthTokenSet ParseTokenResponse(string text)
    {
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            root = data;

        var access = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
        if (string.IsNullOrEmpty(access))
            throw new InvalidOperationException("响应缺少 access_token");

        var refresh = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "";
        var expiresIn = 3600;
        if (root.TryGetProperty("expires_in", out var ei))
        {
            if (ei.ValueKind == JsonValueKind.Number) expiresIn = ei.GetInt32();
            else if (ei.ValueKind == JsonValueKind.String && int.TryParse(ei.GetString(), out var parsed))
                expiresIn = parsed;
        }

        var advertisers = new List<string>();
        if (root.TryGetProperty("advertiser_ids", out var aids))
        {
            if (aids.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in aids.EnumerateArray())
                {
                    var id = a.ValueKind == JsonValueKind.String ? a.GetString() : a.ToString();
                    if (!string.IsNullOrWhiteSpace(id)) advertisers.Add(id!);
                }
            }
            else if (aids.ValueKind == JsonValueKind.String)
            {
                advertisers.AddRange(aids.GetString()!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        return new OAuthTokenSet
        {
            AccessToken = access!,
            RefreshToken = refresh,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, expiresIn - 30)),
            TokenType = root.TryGetProperty("token_type", out var tt) ? tt.GetString() : "Bearer",
            Scope = root.TryGetProperty("scope", out var sc) ? sc.GetString() : TikTokEndpoints.Scope,
            AdvertiserIds = advertisers
        };
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string RedactId(string id) =>
        id.Length <= 8 ? "***" : id[..4] + "…" + id[^2..];

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            else if (OperatingSystem.IsLinux())
                Process.Start("xdg-open", url);
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", url);
            else
                throw;
        }
    }
}
