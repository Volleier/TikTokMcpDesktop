namespace TikTokMcpDesktop.Core.Tables;

/// <summary>
/// Human labels for TikTok for Business Open MCP tools we surface in tables/dashboards.
/// Source: https://business-api.tiktok.com/portal/docs/available-tools-in-tiktok-for-business-mcp-server/v1.3
/// </summary>
public static class ToolDisplayNames
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["auth_advertiser_get"] = "授权广告主",
        ["advertiser_info_get"] = "广告主资料",
        ["advertiser_balance_get"] = "广告主余额",
        ["advertiser_transaction_get"] = "广告主流水",
        ["campaign_get"] = "广告系列",
        ["smart_plus_campaign_get"] = "Smart+ 广告系列",
        ["adgroup_get"] = "广告组",
        ["smart_plus_adgroup_get"] = "Smart+ 广告组",
        ["ad_get"] = "广告",
        ["smart_plus_ad_get"] = "Smart+ 广告",
        ["report_integrated_get"] = "综合报表",
        ["report_video_performance_get"] = "视频效果报表",
        ["report_ad_benchmark_get"] = "广告 Benchmark",
        ["gmv_max_report_get"] = "GMV Max 报表",
        ["audience_insight_info_get"] = "受众洞察",
        ["dmp_custom_audience_list_get"] = "自定义受众列表",
        ["bc_get"] = "商务中心",
        ["bc_balance_get"] = "商务中心余额",
        ["bc_account_cost_get"] = "商务中心消耗"
    };

    public static string For(string toolName) =>
        Map.TryGetValue(toolName, out var label) ? label : toolName;
}
