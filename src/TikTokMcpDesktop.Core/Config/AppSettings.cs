namespace TikTokMcpDesktop.Core.Config;

public enum McpVariant
{
    Layer,
    Flat
}

public enum TokenRequestFormat
{
    Json,
    Form
}

public enum ScheduleMode
{
    /// <summary>Every N minutes.</summary>
    Interval,
    /// <summary>Once per day at local HH:mm.</summary>
    DailyAt
}

/// <summary>
/// Aligns with AIEcomPlex <c>aiecomplex.tiktok.ads</c> / TikTokAdsProperties (Open MCP, not Marketing API).
/// </summary>
public sealed class AppSettings
{
    public string OpenAiBaseUrl { get; set; } = "https://api.openai.com/v1";
    public string OpenAiModel { get; set; } = "gpt-4o-mini";

    /// <summary>MCP client_id. If <see cref="AppId"/> is set, AppId is preferred as client (same as AIEcomPlex).</summary>
    public string ClientId { get; set; } = "";

    /// <summary>Optional; when set, used as client_id and also sent as app_id on authorize URL.</summary>
    public string AppId { get; set; } = "";

    /// <summary>Optional; Open MCP + PKCE usually does not need it. Sent as JSON field "secret" when present.</summary>
    public string AppSecret { get; set; } = "";

    public McpVariant McpVariant { get; set; } = McpVariant.Layer;
    public TokenRequestFormat TokenRequestFormat { get; set; } = TokenRequestFormat.Json;
    public int SyncIntervalMinutes { get; set; } = 60;
    public bool AutoSyncEnabled { get; set; }
    public ScheduleMode ScheduleMode { get; set; } = ScheduleMode.Interval;
    /// <summary>Local time for <see cref="ScheduleMode.DailyAt"/>, format HH:mm (e.g. 09:30).</summary>
    public string DailyAtLocalTime { get; set; } = "09:00";

    /// <summary>Enable Feishu application-bot push after successful MCP sync.</summary>
    public bool FeishuPushEnabled { get; set; }
    /// <summary>Feishu app_id from developer console.</summary>
    public string FeishuAppId { get; set; } = "";
    /// <summary>open_id / chat_id / user_id / email — see Feishu im/v1/messages receive_id_type.</summary>
    public string FeishuReceiveIdType { get; set; } = "chat_id";
    /// <summary>Target user or chat id for bot messages.</summary>
    public string FeishuReceiveId { get; set; } = "";
    /// <summary>When true, PushJob auto-pushes a summary card after a successful pull.</summary>
    public bool FeishuPushAfterSync { get; set; } = true;

    public int OAuthCallbackPort { get; set; } = 17865;

    /// <summary>Override authorize URL; empty = default TT4B portal.</summary>
    public string? AuthorizeUrlOverride { get; set; }

    /// <summary>Override MCP URL; empty = variant default. Token URL derived as {mcp}/oauth/token.</summary>
    public string? McpUrlOverride { get; set; }

    public string EffectiveClientId =>
        !string.IsNullOrWhiteSpace(AppId) ? AppId.Trim() : ClientId.Trim();

    public string GetAuthorizeUrl() =>
        string.IsNullOrWhiteSpace(AuthorizeUrlOverride)
            ? TikTokEndpoints.AuthorizeUrl
            : AuthorizeUrlOverride!.Trim();

    public string GetMcpUrl() =>
        string.IsNullOrWhiteSpace(McpUrlOverride)
            ? TikTokEndpoints.GetMcpUrl(McpVariant)
            : McpUrlOverride!.Trim().TrimEnd('/');

    public string GetTokenUrl() => $"{GetMcpUrl()}/oauth/token";

    public string GetResource() => GetMcpUrl();
}

public static class TikTokEndpoints
{
    public const string AuthorizeUrl = "https://business-api.tiktok.com/portal/mcp-tt4b-authorize";
    public const string Scope = "mcp:tt4b";
    public const string FlatMcpUrl = "https://business-api.tiktok.com/open_mcp/tt-ads-mcp-flat";
    public const string LayerMcpUrl = "https://business-api.tiktok.com/open_mcp/tt-ads-mcp-layer";

    public static string GetMcpUrl(McpVariant variant) =>
        variant == McpVariant.Flat ? FlatMcpUrl : LayerMcpUrl;

    public static string GetTokenUrl(McpVariant variant) =>
        $"{GetMcpUrl(variant).TrimEnd('/')}/oauth/token";

    public static string GetRegisterUrl(McpVariant variant) =>
        $"{GetMcpUrl(variant).TrimEnd('/')}/oauth/register";
}
