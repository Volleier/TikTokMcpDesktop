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

public sealed class AppSettings
{
    public string OpenAiBaseUrl { get; set; } = "https://api.openai.com/v1";
    public string OpenAiModel { get; set; } = "gpt-4o-mini";
    /// <summary>Stored via SecretStore, not in this plain file.</summary>
    public string ClientId { get; set; } = "";
    public McpVariant McpVariant { get; set; } = McpVariant.Layer;
    public TokenRequestFormat TokenRequestFormat { get; set; } = TokenRequestFormat.Json;
    public int SyncIntervalMinutes { get; set; } = 60;
    public bool AutoSyncEnabled { get; set; }
    public int OAuthCallbackPort { get; set; } = 17865;
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
}
