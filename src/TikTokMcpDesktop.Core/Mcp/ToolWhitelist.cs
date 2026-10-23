namespace TikTokMcpDesktop.Core.Mcp;

/// <summary>
/// Aligns with AIEcomPlex TikTokAdsToolCatalog: explicit read allowlist + hard deny writes.
/// Agent / pull paths only use allowed read tools.
/// </summary>
public static class ToolWhitelist
{
    /// <summary>Names mirrored from AIEcomPlex ALLOWED_TOOLS (read subset + metadata).</summary>
    private static readonly HashSet<string> AllowedReadExact = new(StringComparer.OrdinalIgnoreCase)
    {
        // metadata
        "tool_list",
        "tool_get",
        "auth_advertiser_get",
        // aggregate / report
        "report_integrated_get",
        "workspace_ads_aggregate",
        // detail reads
        "campaign_get",
        "smart_plus_campaign_get",
        "adgroup_get",
        "smart_plus_adgroup_get",
        "ad_get",
        "smart_plus_ad_get",
        "advertiser_info_get",
        "advertiser_get"
    };

    private static readonly string[] WriteDenySubstrings =
    [
        "_create", "create_", "_update", "update_", "_delete", "delete_",
        "_status", "status_update", "remove", "upload",
        "modify", "write", "push", "bind", "unbind"
    ];

    private static readonly string[] PreferredPullExact =
    [
        "auth_advertiser_get",
        "advertiser_info_get",
        "report_integrated_get",
        "campaign_get",
        "smart_plus_campaign_get"
    ];

    public static bool IsWriteTool(string toolName)
    {
        var n = toolName.ToLowerInvariant();
        // Explicit create/update/status family from AIEcomPlex write set
        if (n.Contains("campaign_create") || n.Contains("adgroup_create") || n.Contains("ad_create") ||
            n.Contains("campaign_update") || n.Contains("adgroup_update") || n.Contains("ad_update") ||
            n.Contains("status_update") || n.Contains("smart_plus") && (n.Contains("create") || n.Contains("update")))
            return true;
        return WriteDenySubstrings.Any(s => n.Contains(s));
    }

    public static bool IsAllowedForModel(string toolName)
    {
        if (IsWriteTool(toolName)) return false;
        if (AllowedReadExact.Contains(toolName)) return true;
        // Layer MCP may expose names not in flat catalog; allow safe get/list patterns only
        var n = toolName.ToLowerInvariant();
        return (n.EndsWith("_get") || n.EndsWith("_list") || n.StartsWith("auth_") || n.Contains("report"))
               && !IsWriteTool(toolName);
    }

    public static bool IsPreferredForPull(string toolName)
    {
        if (!IsAllowedForModel(toolName)) return false;
        if (PreferredPullExact.Any(p => p.Equals(toolName, StringComparison.OrdinalIgnoreCase)))
            return true;
        var n = toolName.ToLowerInvariant();
        return n.Contains("advertiser") || n.Contains("report") || n.Contains("campaign_get");
    }

    public static IReadOnlyList<McpTool> FilterForModel(IEnumerable<McpTool> tools) =>
        tools.Where(t => IsAllowedForModel(t.Name)).ToList();

    public static IReadOnlyList<McpTool> FilterForPull(IEnumerable<McpTool> tools)
    {
        var all = tools.Where(t => IsAllowedForModel(t.Name)).ToList();
        var preferred = all.Where(t => IsPreferredForPull(t.Name)).ToList();
        if (preferred.Count > 0)
            return preferred.Take(8).ToList();
        return all.Take(3).ToList();
    }
}
