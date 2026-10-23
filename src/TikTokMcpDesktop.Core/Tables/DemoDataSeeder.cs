using System.Text.Json;
using TikTokMcpDesktop.Core.Store;

namespace TikTokMcpDesktop.Core.Tables;

/// <summary>
/// Offline demo payloads shaped like TikTok Open MCP read tools
/// (advertiser / campaign / adgroup / ad / report_integrated_get).
/// </summary>
public static class DemoDataSeeder
{
    public static bool NeedsSeed(DataStore db)
    {
        var tables = db.ListTables();
        if (tables.Count == 0) return true;
        var tools = tables.Select(t => t.SourceTool).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return !tools.Contains("campaign_get") || !tools.Contains("report_integrated_get");
    }

    public static int Seed(TableIngestService ingest, string? pullDir = null)
    {
        var count = 0;
        foreach (var (tool, json) in Payloads())
        {
            using var doc = JsonDocument.Parse(json);
            var (_, n) = ingest.IngestToolResult(tool, doc.RootElement, pullDir);
            count += n;
        }
        return count;
    }

    private static IEnumerable<(string Tool, string Json)> Payloads()
    {
        yield return ("auth_advertiser_get", """
            {
              "list": [
                {"advertiser_id":"712340001","advertiser_name":"星河美妆旗舰店","status":"STATUS_ENABLE"},
                {"advertiser_id":"712340002","advertiser_name":"北岛数码旗舰店","status":"STATUS_ENABLE"},
                {"advertiser_id":"712340003","advertiser_name":"南岸生活方式","status":"STATUS_DISABLE"}
              ]
            }
            """);

        yield return ("advertiser_info_get", """
            {
              "list": [
                {"advertiser_id":"712340001","name":"星河美妆旗舰店","currency":"CNY","timezone":"Asia/Shanghai","company":"星河贸易","industry":"Beauty","balance":186420.55,"status":"STATUS_ENABLE"},
                {"advertiser_id":"712340002","name":"北岛数码旗舰店","currency":"CNY","timezone":"Asia/Shanghai","company":"北岛科技","industry":"Electronics","balance":92410.00,"status":"STATUS_ENABLE"},
                {"advertiser_id":"712340003","name":"南岸生活方式","currency":"CNY","timezone":"Asia/Shanghai","company":"南岸文化","industry":"Lifestyle","balance":12880.20,"status":"STATUS_DISABLE"}
              ]
            }
            """);

        yield return ("campaign_get", """
            {
              "list": [
                {"campaign_id":"c_1001","advertiser_id":"712340001","campaign_name":"双11-爆款种草","objective_type":"CONVERSIONS","budget_mode":"BUDGET_MODE_DAY","budget":5000,"operation_status":"ENABLE","secondary_status":"CAMPAIGN_STATUS_ENABLE"},
                {"campaign_id":"c_1002","advertiser_id":"712340001","campaign_name":"新品试用-流量测试","objective_type":"TRAFFIC","budget_mode":"BUDGET_MODE_TOTAL","budget":12000,"operation_status":"ENABLE","secondary_status":"CAMPAIGN_STATUS_ENABLE"},
                {"campaign_id":"c_2001","advertiser_id":"712340002","campaign_name":"手机配件-ROI","objective_type":"PRODUCT_SALES","budget_mode":"BUDGET_MODE_DAY","budget":8000,"operation_status":"ENABLE","secondary_status":"CAMPAIGN_STATUS_ENABLE"},
                {"campaign_id":"c_2002","advertiser_id":"712340002","campaign_name":"品牌认知-视频","objective_type":"REACH","budget_mode":"BUDGET_MODE_DAY","budget":3000,"operation_status":"DISABLE","secondary_status":"CAMPAIGN_STATUS_DISABLE"},
                {"campaign_id":"c_3001","advertiser_id":"712340003","campaign_name":"生活方式-线索","objective_type":"LEAD_GENERATION","budget_mode":"BUDGET_MODE_DAY","budget":2000,"operation_status":"ENABLE","secondary_status":"CAMPAIGN_STATUS_ENABLE"}
              ]
            }
            """);

        yield return ("adgroup_get", """
            {
              "list": [
                {"adgroup_id":"ag_11","campaign_id":"c_1001","advertiser_id":"712340001","adgroup_name":"兴趣人群-美妆","placement_type":"PLACEMENT_TYPE_AUTOMATIC","budget":2500,"bid_type":"BID_TYPE_NO_BID","operation_status":"ENABLE"},
                {"adgroup_id":"ag_12","campaign_id":"c_1001","advertiser_id":"712340001","adgroup_name":"再营销-加购未购","placement_type":"PLACEMENT_TYPE_NORMAL","budget":2500,"bid_type":"BID_TYPE_CUSTOM","operation_status":"ENABLE"},
                {"adgroup_id":"ag_21","campaign_id":"c_1002","advertiser_id":"712340001","adgroup_name":"冷启动-宽定向","placement_type":"PLACEMENT_TYPE_AUTOMATIC","budget":6000,"bid_type":"BID_TYPE_NO_BID","operation_status":"ENABLE"},
                {"adgroup_id":"ag_31","campaign_id":"c_2001","advertiser_id":"712340002","adgroup_name":"数码配件-购买","placement_type":"PLACEMENT_TYPE_NORMAL","budget":5000,"bid_type":"BID_TYPE_CUSTOM","operation_status":"ENABLE"},
                {"adgroup_id":"ag_32","campaign_id":"c_2001","advertiser_id":"712340002","adgroup_name":"相似人群扩展","placement_type":"PLACEMENT_TYPE_AUTOMATIC","budget":3000,"bid_type":"BID_TYPE_NO_BID","operation_status":"ENABLE"},
                {"adgroup_id":"ag_41","campaign_id":"c_2002","advertiser_id":"712340002","adgroup_name":"品牌曝光-全国","placement_type":"PLACEMENT_TYPE_NORMAL","budget":3000,"bid_type":"BID_TYPE_NO_BID","operation_status":"DISABLE"},
                {"adgroup_id":"ag_51","campaign_id":"c_3001","advertiser_id":"712340003","adgroup_name":"表单线索-一线城市","placement_type":"PLACEMENT_TYPE_NORMAL","budget":2000,"bid_type":"BID_TYPE_CUSTOM","operation_status":"ENABLE"}
              ]
            }
            """);

        yield return ("ad_get", """
            {
              "list": [
                {"ad_id":"ad_111","adgroup_id":"ag_11","campaign_id":"c_1001","advertiser_id":"712340001","ad_name":"口红种草-竖版15s","ad_format":"SINGLE_VIDEO","operation_status":"ENABLE","identity_type":"CUSTOMIZED_USER"},
                {"ad_id":"ad_112","adgroup_id":"ag_11","campaign_id":"c_1001","advertiser_id":"712340001","ad_name":"护肤套装-轮播","ad_format":"CAROUSEL_ADS","operation_status":"ENABLE","identity_type":"CUSTOMIZED_USER"},
                {"ad_id":"ad_121","adgroup_id":"ag_12","campaign_id":"c_1001","advertiser_id":"712340001","ad_name":"加购召回-优惠券","ad_format":"SINGLE_IMAGE","operation_status":"ENABLE","identity_type":"AUTH_CODE"},
                {"ad_id":"ad_211","adgroup_id":"ag_21","campaign_id":"c_1002","advertiser_id":"712340001","ad_name":"新品开箱-30s","ad_format":"SINGLE_VIDEO","operation_status":"ENABLE","identity_type":"CUSTOMIZED_USER"},
                {"ad_id":"ad_311","adgroup_id":"ag_31","campaign_id":"c_2001","advertiser_id":"712340002","ad_name":"耳机卖点-对比","ad_format":"SINGLE_VIDEO","operation_status":"ENABLE","identity_type":"TT_USER"},
                {"ad_id":"ad_312","adgroup_id":"ag_31","campaign_id":"c_2001","advertiser_id":"712340002","ad_name":"充电器-图文","ad_format":"SINGLE_IMAGE","operation_status":"DISABLE","identity_type":"TT_USER"},
                {"ad_id":"ad_321","adgroup_id":"ag_32","campaign_id":"c_2001","advertiser_id":"712340002","ad_name":"配件合集-轮播","ad_format":"CAROUSEL_ADS","operation_status":"ENABLE","identity_type":"TT_USER"},
                {"ad_id":"ad_411","adgroup_id":"ag_41","campaign_id":"c_2002","advertiser_id":"712340002","ad_name":"品牌片-60s","ad_format":"SINGLE_VIDEO","operation_status":"DISABLE","identity_type":"TT_USER"},
                {"ad_id":"ad_511","adgroup_id":"ag_51","campaign_id":"c_3001","advertiser_id":"712340003","ad_name":"线索表单-生活方式","ad_format":"SINGLE_VIDEO","operation_status":"ENABLE","identity_type":"CUSTOMIZED_USER"}
              ]
            }
            """);

        // Integrated report rows — mirrors report_integrated_get dimensions/metrics commonly used in ads analytics.
        yield return ("report_integrated_get", """
            {
              "list": [
                {"stat_time_day":"2026-10-17","advertiser_id":"712340001","campaign_id":"c_1001","campaign_name":"双11-爆款种草","objective_type":"CONVERSIONS","spend":1820.40,"impressions":420000,"clicks":9600,"conversion":186,"ctr":2.29,"cpc":0.19,"cpa":9.79},
                {"stat_time_day":"2026-10-18","advertiser_id":"712340001","campaign_id":"c_1001","campaign_name":"双11-爆款种草","objective_type":"CONVERSIONS","spend":2105.10,"impressions":480000,"clicks":11200,"conversion":221,"ctr":2.33,"cpc":0.19,"cpa":9.53},
                {"stat_time_day":"2026-10-19","advertiser_id":"712340001","campaign_id":"c_1001","campaign_name":"双11-爆款种草","objective_type":"CONVERSIONS","spend":1988.00,"impressions":455000,"clicks":10450,"conversion":205,"ctr":2.30,"cpc":0.19,"cpa":9.70},
                {"stat_time_day":"2026-10-17","advertiser_id":"712340001","campaign_id":"c_1002","campaign_name":"新品试用-流量测试","objective_type":"TRAFFIC","spend":640.00,"impressions":210000,"clicks":8900,"conversion":42,"ctr":4.24,"cpc":0.07,"cpa":15.24},
                {"stat_time_day":"2026-10-18","advertiser_id":"712340001","campaign_id":"c_1002","campaign_name":"新品试用-流量测试","objective_type":"TRAFFIC","spend":710.50,"impressions":235000,"clicks":9800,"conversion":48,"ctr":4.17,"cpc":0.07,"cpa":14.80},
                {"stat_time_day":"2026-10-19","advertiser_id":"712340001","campaign_id":"c_1002","campaign_name":"新品试用-流量测试","objective_type":"TRAFFIC","spend":690.20,"impressions":228000,"clicks":9400,"conversion":45,"ctr":4.12,"cpc":0.07,"cpa":15.34},
                {"stat_time_day":"2026-10-17","advertiser_id":"712340002","campaign_id":"c_2001","campaign_name":"手机配件-ROI","objective_type":"PRODUCT_SALES","spend":3120.80,"impressions":610000,"clicks":14200,"conversion":268,"ctr":2.33,"cpc":0.22,"cpa":11.64},
                {"stat_time_day":"2026-10-18","advertiser_id":"712340002","campaign_id":"c_2001","campaign_name":"手机配件-ROI","objective_type":"PRODUCT_SALES","spend":3455.60,"impressions":655000,"clicks":15100,"conversion":295,"ctr":2.31,"cpc":0.23,"cpa":11.71},
                {"stat_time_day":"2026-10-19","advertiser_id":"712340002","campaign_id":"c_2001","campaign_name":"手机配件-ROI","objective_type":"PRODUCT_SALES","spend":3290.00,"impressions":640000,"clicks":14880,"conversion":280,"ctr":2.33,"cpc":0.22,"cpa":11.75},
                {"stat_time_day":"2026-10-17","advertiser_id":"712340002","campaign_id":"c_2002","campaign_name":"品牌认知-视频","objective_type":"REACH","spend":880.00,"impressions":900000,"clicks":4200,"conversion":12,"ctr":0.47,"cpc":0.21,"cpa":73.33},
                {"stat_time_day":"2026-10-18","advertiser_id":"712340002","campaign_id":"c_2002","campaign_name":"品牌认知-视频","objective_type":"REACH","spend":920.40,"impressions":940000,"clicks":4500,"conversion":14,"ctr":0.48,"cpc":0.20,"cpa":65.74},
                {"stat_time_day":"2026-10-19","advertiser_id":"712340003","campaign_id":"c_3001","campaign_name":"生活方式-线索","objective_type":"LEAD_GENERATION","spend":510.30,"impressions":120000,"clicks":3600,"conversion":96,"ctr":3.00,"cpc":0.14,"cpa":5.32},
                {"stat_time_day":"2026-10-20","advertiser_id":"712340001","campaign_id":"c_1001","campaign_name":"双11-爆款种草","objective_type":"CONVERSIONS","spend":2240.00,"impressions":505000,"clicks":11880,"conversion":238,"ctr":2.35,"cpc":0.19,"cpa":9.41},
                {"stat_time_day":"2026-10-20","advertiser_id":"712340002","campaign_id":"c_2001","campaign_name":"手机配件-ROI","objective_type":"PRODUCT_SALES","spend":3610.20,"impressions":680000,"clicks":15900,"conversion":312,"ctr":2.34,"cpc":0.23,"cpa":11.57},
                {"stat_time_day":"2026-10-21","advertiser_id":"712340001","campaign_id":"c_1001","campaign_name":"双11-爆款种草","objective_type":"CONVERSIONS","spend":2366.80,"impressions":530000,"clicks":12440,"conversion":251,"ctr":2.35,"cpc":0.19,"cpa":9.43},
                {"stat_time_day":"2026-10-21","advertiser_id":"712340002","campaign_id":"c_2001","campaign_name":"手机配件-ROI","objective_type":"PRODUCT_SALES","spend":3788.50,"impressions":705000,"clicks":16420,"conversion":328,"ctr":2.33,"cpc":0.23,"cpa":11.55},
                {"stat_time_day":"2026-10-22","advertiser_id":"712340001","campaign_id":"c_1001","campaign_name":"双11-爆款种草","objective_type":"CONVERSIONS","spend":2410.00,"impressions":548000,"clicks":12900,"conversion":260,"ctr":2.35,"cpc":0.19,"cpa":9.27},
                {"stat_time_day":"2026-10-22","advertiser_id":"712340003","campaign_id":"c_3001","campaign_name":"生活方式-线索","objective_type":"LEAD_GENERATION","spend":560.00,"impressions":132000,"clicks":3900,"conversion":108,"ctr":2.95,"cpc":0.14,"cpa":5.19}
              ]
            }
            """);
    }
}
