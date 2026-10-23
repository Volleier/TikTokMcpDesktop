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
        if (string.IsNullOrWhiteSpace(settings.ClientId))
            throw new InvalidOperationException("请先在设置中填写 client_id。");

        var variant = settings.McpVariant;
        var resource = TikTokEndpoints.GetMcpUrl(variant);
        var redirectUri = $"http://127.0.0.1:{settings.OAuthCallbackPort}/callback";
        var verifier = Pkce.CreateVerifier();
        var challenge = Pkce.CreateChallenge(verifier);
        var state = Pkce.CreateState();

        var authUrl =
            $"{TikTokEndpoints.AuthorizeUrl}" +
            $"?client_id={Uri.EscapeDataString(settings.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&response_type=code" +
            $"&scope={Uri.EscapeDataString(TikTokEndpoints.Scope)}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&code_challenge={Uri.EscapeDataString(challenge)}" +
            $"&code_challenge_method=S256" +
            $"&resource={Uri.EscapeDataString(resource)}";

        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{settings.OAuthCallbackPort}/");
        listener.Start();
        AppLog.Info($"OAuth listening on {redirectUri}");

        try
        {
            OpenBrowser(authUrl);
            var context = await listener.GetContextAsync().WaitAsync(ct);
            var req = context.Request;
            var code = req.QueryString["code"];
            var returnedState = req.QueryString["state"];
            var error = req.QueryString["error"];

            var html = "<html><body style='font-family:sans-serif;padding:2rem'><h2>Authorization complete</h2><p>You can close this window and return to the app.</p></body></html>";
            if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code) || returnedState != state)
            {
                html = $"<html><body style='font-family:sans-serif;padding:2rem'><h2>Authorization failed</h2><p>{WebUtility.HtmlEncode(error ?? "invalid state/code")}</p></body></html>";
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

            var token = await ExchangeCodeAsync(settings, code, redirectUri, verifier, resource, ct);
            _tokenStore.Save(variant, token);
            AppLog.Info($"OAuth token saved for variant {variant}");
            return token;
        }
        finally
        {
            listener.Stop();
        }
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

        try
        {
            var refreshed = await RefreshAsync(settings, token.RefreshToken, ct);
            _tokenStore.Save(variant, refreshed);
            AppLog.Info("Access token refreshed");
            return refreshed.AccessToken;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Refresh failed: {ex.Message}");
            throw new ReauthRequiredException("refresh_token 失效，请重新授权。");
        }
    }

    public async Task<OAuthTokenSet> ExchangeCodeAsync(
        AppSettings settings,
        string code,
        string redirectUri,
        string codeVerifier,
        string resource,
        CancellationToken ct = default)
    {
        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = settings.ClientId,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier,
            ["resource"] = resource
        };
        return await RequestTokenAsync(settings, body, ct);
    }

    public async Task<OAuthTokenSet> RefreshAsync(AppSettings settings, string refreshToken, CancellationToken ct = default)
    {
        var resource = TikTokEndpoints.GetMcpUrl(settings.McpVariant);
        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = settings.ClientId,
            ["refresh_token"] = refreshToken,
            ["resource"] = resource
        };
        return await RequestTokenAsync(settings, body, ct);
    }

    private async Task<OAuthTokenSet> RequestTokenAsync(
        AppSettings settings,
        Dictionary<string, string> body,
        CancellationToken ct)
    {
        var url = TikTokEndpoints.GetTokenUrl(settings.McpVariant);
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
            throw new InvalidOperationException($"换票失败 HTTP {(int)resp.StatusCode}");
        }

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        var access = root.GetProperty("access_token").GetString()
                     ?? throw new InvalidOperationException("响应缺少 access_token");
        var refresh = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "";
        var expiresIn = root.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600;
        var tokenType = root.TryGetProperty("token_type", out var tt) ? tt.GetString() : "Bearer";
        var scope = root.TryGetProperty("scope", out var sc) ? sc.GetString() : TikTokEndpoints.Scope;

        return new OAuthTokenSet
        {
            AccessToken = access,
            RefreshToken = refresh,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, expiresIn - 30)),
            TokenType = tokenType,
            Scope = scope
        };
    }

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
