namespace TikTokMcpDesktop.Core.Mcp;

/// <summary>
/// Hard deny-list for write/mutation tools; allowlist for tools exposed to the model and pull job.
/// Names are matched case-insensitively; substring patterns catch variants from flat/layer MCP.
/// </summary>
public static class ToolWhitelist
{
    private static readonly string[] WriteDenySubstrings =
    [
        "create", "update", "delete", "remove", "upload", "set_", "_set",
        "modify", "write", "push", "send", "cancel", "pause", "enable", "disable",
        "approve", "reject", "assign", "bind", "unbind"
    ];

    private static readonly string[] PreferredPullPatterns =
    [
        "advertiser_info",
        "advertiser_get",
        "get_advertiser",
        "campaign_get",
        "adgroup_get",
        "ad_get",
        "report",
        "integrated_report",
        "auction_report",
        "basic_report",
        "gmv_max",
        "account"
    ];

    public static bool IsWriteTool(string toolName)
    {
        var n = toolName.ToLowerInvariant();
        return WriteDenySubstrings.Any(s => n.Contains(s));
    }

    public static bool IsAllowedForModel(string toolName) => !IsWriteTool(toolName);

    public static bool IsPreferredForPull(string toolName)
    {
        if (IsWriteTool(toolName)) return false;
        var n = toolName.ToLowerInvariant();
        return PreferredPullPatterns.Any(p => n.Contains(p));
    }

    public static IReadOnlyList<McpTool> FilterForModel(IEnumerable<McpTool> tools) =>
        tools.Where(t => IsAllowedForModel(t.Name)).ToList();

    public static IReadOnlyList<McpTool> FilterForPull(IEnumerable<McpTool> tools)
    {
        var all = tools.Where(t => !IsWriteTool(t.Name)).ToList();
        var preferred = all.Where(t => IsPreferredForPull(t.Name)).ToList();
        if (preferred.Count > 0)
            return preferred.Take(8).ToList();
        return all.Take(3).ToList();
    }
}
